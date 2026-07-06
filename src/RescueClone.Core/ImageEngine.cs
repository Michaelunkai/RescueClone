using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RescueClone.Core;

public sealed class ImageEngine
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RCIMG1\n");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public ImageReport Create(ImageOptions options)
    {
        if (!Directory.Exists(options.SourcePath))
            throw new DirectoryNotFoundException(options.SourcePath);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.ImagePath)) ?? ".");
        var sourceRoot = Path.GetFullPath(options.SourcePath);
        var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var output = File.Create(options.ImagePath);
        output.Write(Magic);
        WriteJson(output, new ContainerHeader(1, options.Compression.ToString(), options.Password is not null));

        var entries = new List<ImageFileEntry>();
        long originalBytes = 0;
        long storedBytes = 0;

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
            var raw = File.ReadAllBytes(file);
            var hash = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
            var stored = EncodePayload(raw, options.Compression, options.Password);
            var entry = new ImageFileEntry(relative, raw.LongLength, stored.LongLength, hash);

            WriteJson(output, entry);
            output.Write(stored);

            entries.Add(entry);
            originalBytes += raw.LongLength;
            storedBytes += stored.LongLength;
        }

        WriteJson(output, new EndMarker(true));

        var rootHash = ComputeRootHash(entries);
        return new ImageReport(options.ImagePath, entries.Count, originalBytes, storedBytes, rootHash, entries);
    }

    public ImageReport Verify(string imagePath, string? password)
    {
        var files = ReadImage(imagePath, password, restorePath: null, overwrite: false, verifyOnly: true);
        var originalBytes = files.Sum(f => f.OriginalLength);
        var storedBytes = files.Sum(f => f.StoredLength);
        return new ImageReport(imagePath, files.Count, originalBytes, storedBytes, ComputeRootHash(files), files);
    }

    public RestoreReport Restore(RestoreOptions options)
    {
        if (Directory.Exists(options.TargetPath) && Directory.EnumerateFileSystemEntries(options.TargetPath).Any() && !options.Overwrite)
            throw new IOException("Target path is not empty. Pass overwrite=true to restore into it.");

        Directory.CreateDirectory(options.TargetPath);
        var files = ReadImage(options.ImagePath, options.Password, options.TargetPath, options.Overwrite, verifyOnly: false);
        return new RestoreReport(options.ImagePath, options.TargetPath, files.Count, files.Sum(f => f.OriginalLength));
    }

    private static List<ImageFileEntry> ReadImage(string imagePath, string? password, string? restorePath, bool overwrite, bool verifyOnly)
    {
        using var input = File.OpenRead(imagePath);
        var magic = new byte[Magic.Length];
        ReadExactly(input, magic);
        if (!magic.SequenceEqual(Magic))
            throw new InvalidDataException("Not a RescueClone image.");

        var header = ReadJson<ContainerHeader>(input);
        var compression = Enum.Parse<CompressionMode>(header.Compression);
        if (header.Encrypted && string.IsNullOrEmpty(password))
            throw new InvalidDataException("Image is encrypted; a password is required.");

        var entries = new List<ImageFileEntry>();
        while (true)
        {
            var markerOrEntry = ReadRawJson(input);
            if (markerOrEntry.Contains("\"Done\"", StringComparison.Ordinal))
                break;

            var entry = JsonSerializer.Deserialize<ImageFileEntry>(markerOrEntry, JsonOptions)
                ?? throw new InvalidDataException("Invalid file entry.");
            var stored = new byte[entry.StoredLength];
            ReadExactly(input, stored);
            var raw = DecodePayload(stored, compression, header.Encrypted ? password : null);
            var hash = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
            if (!StringComparer.OrdinalIgnoreCase.Equals(hash, entry.Sha256))
                throw new InvalidDataException($"Checksum mismatch for {entry.RelativePath}.");

            if (!verifyOnly && restorePath is not null)
            {
                var target = Path.GetFullPath(Path.Combine(restorePath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                var root = Path.GetFullPath(restorePath);
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Unsafe relative path in image: {entry.RelativePath}");
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? root);
                if (File.Exists(target) && !overwrite)
                    throw new IOException($"Target file exists: {target}");
                File.WriteAllBytes(target, raw);
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static byte[] EncodePayload(byte[] raw, CompressionMode compression, string? password)
    {
        var compressed = compression == CompressionMode.None ? raw : Gzip(raw, compression);
        return password is null ? compressed : Encrypt(compressed, password);
    }

    private static byte[] DecodePayload(byte[] stored, CompressionMode compression, string? password)
    {
        var decrypted = password is null ? stored : Decrypt(stored, password);
        return compression == CompressionMode.None ? decrypted : Gunzip(decrypted);
    }

    private static byte[] Gzip(byte[] raw, CompressionMode compression)
    {
        using var buffer = new MemoryStream();
        var level = compression == CompressionMode.High ? CompressionLevel.SmallestSize : CompressionLevel.Optimal;
        using (var gzip = new GZipStream(buffer, level, leaveOpen: true))
            gzip.Write(raw);
        return buffer.ToArray();
    }

    private static byte[] Gunzip(byte[] raw)
    {
        using var input = new MemoryStream(raw);
        using var gzip = new GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Encrypt(byte[] raw, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32);
        aes.GenerateIV();
        using var output = new MemoryStream();
        output.Write(salt);
        output.Write(aes.IV);
        using (var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
            crypto.Write(raw);
        return output.ToArray();
    }

    private static byte[] Decrypt(byte[] stored, string password)
    {
        var salt = stored[..16];
        var iv = stored[16..32];
        var payload = stored[32..];
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 200_000, HashAlgorithmName.SHA256, 32);
        aes.IV = iv;
        using var input = new MemoryStream(payload);
        using var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var output = new MemoryStream();
        crypto.CopyTo(output);
        return output.ToArray();
    }

    private static string ComputeRootHash(IEnumerable<ImageFileEntry> entries)
    {
        var canonical = string.Join('\n', entries.Select(e => $"{e.RelativePath}|{e.OriginalLength}|{e.Sha256}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static void WriteJson<T>(Stream stream, T value)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        stream.Write(BitConverter.GetBytes(json.LongLength));
        stream.Write(json);
    }

    private static T ReadJson<T>(Stream stream)
    {
        return JsonSerializer.Deserialize<T>(ReadRawJson(stream), JsonOptions)
            ?? throw new InvalidDataException($"Invalid {typeof(T).Name} record.");
    }

    private static string ReadRawJson(Stream stream)
    {
        var lenBytes = new byte[8];
        ReadExactly(stream, lenBytes);
        var length = BitConverter.ToInt64(lenBytes);
        if (length < 2 || length > 64 * 1024 * 1024)
            throw new InvalidDataException("Invalid record length.");
        var json = new byte[length];
        ReadExactly(stream, json);
        return Encoding.UTF8.GetString(json);
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }

    private sealed record ContainerHeader(int Version, string Compression, bool Encrypted);
    private sealed record EndMarker(bool Done);
}
