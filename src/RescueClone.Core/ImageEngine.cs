using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RescueClone.Core.Native;

namespace RescueClone.Core;

public sealed class ImageEngine : IImageEngine
{
    private const int V2BlockSize = 1024 * 1024;
    private static readonly byte[] V1Magic = Encoding.ASCII.GetBytes("RCIMG1\n");
    private static readonly byte[] V2Magic = Encoding.ASCII.GetBytes("RCIMG2\n");
    private static readonly byte[] V2FooterMagic = Encoding.ASCII.GetBytes("RCEND2\n");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public ImageReport Create(ImageOptions options)
    {
        return options.Format == ImageContainerFormat.V1 ? CreateV1(options) : CreateV2(options);
    }

    private ImageReport CreateV1(ImageOptions options)
    {
        if (!Directory.Exists(options.SourcePath))
            throw new DirectoryNotFoundException(options.SourcePath);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.ImagePath)) ?? ".");
        var sourceRoot = Path.GetFullPath(options.SourcePath);
        var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        PrepareImageForWrite(options.ImagePath);
        using var output = File.Create(options.ImagePath);
        output.Write(V1Magic);
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
        output.Dispose();
        ProtectImage(options.ImagePath);
        return new ImageReport(options.ImagePath, entries.Count, originalBytes, storedBytes, rootHash, entries, FormatVersion: 1);
    }

    private ImageReport CreateV2(ImageOptions options)
    {
        if (!Directory.Exists(options.SourcePath))
            throw new DirectoryNotFoundException(options.SourcePath);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.ImagePath)) ?? ".");
        var sourceRoot = Path.GetFullPath(options.SourcePath);
        var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        PrepareImageForWrite(options.ImagePath);
        using var output = File.Create(options.ImagePath);
        output.Write(V2Magic);
        WriteJson(output, new ContainerHeader(2, options.Compression.ToString(), options.Password is not null));

        var entries = new List<ImageFileEntry>();
        var manifestFiles = new List<V2FileManifest>();
        long originalBytes = 0;
        long storedBytes = 0;

        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
            var raw = File.ReadAllBytes(file);
            var fileHash = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
            var blocks = new List<V2BlockEntry>();
            foreach (var plan in NativeBlockPlanner.PlanV2Blocks(raw.LongLength, V2BlockSize))
            {
                var block = raw.AsSpan(checked((int)plan.Offset), plan.Length).ToArray();
                var stored = EncodePayload(block, options.Compression, options.Password);
                var blockHash = Convert.ToHexString(SHA256.HashData(block)).ToLowerInvariant();
                var payloadOffset = output.Position;
                output.Write(stored);
                blocks.Add(new V2BlockEntry(plan.Index, payloadOffset, plan.Length, stored.LongLength, blockHash));
                storedBytes += stored.LongLength;
            }

            entries.Add(new ImageFileEntry(relative, raw.LongLength, blocks.Sum(b => b.StoredLength), fileHash));
            manifestFiles.Add(new V2FileManifest(relative, raw.LongLength, fileHash, blocks));
            originalBytes += raw.LongLength;
        }

        var rootHash = ComputeRootHash(entries);
        var manifestOffset = output.Position;
        WriteJson(output, new V2Manifest(rootHash, originalBytes, storedBytes, manifestFiles));
        var manifestLength = output.Position - manifestOffset;
        WriteV2Footer(output, new V2Footer(manifestOffset, manifestLength));

        output.Dispose();
        ProtectImage(options.ImagePath);
        return new ImageReport(options.ImagePath, entries.Count, originalBytes, storedBytes, rootHash, entries, FormatVersion: 2);
    }

    public ImageReport Verify(string imagePath, string? password)
    {
        var (files, formatVersion) = ReadImage(imagePath, password, restorePath: null, overwrite: false, verifyOnly: true);
        var originalBytes = files.Sum(f => f.OriginalLength);
        var storedBytes = files.Sum(f => f.StoredLength);
        return new ImageReport(imagePath, files.Count, originalBytes, storedBytes, ComputeRootHash(files), files, formatVersion);
    }

    public RestoreReport Restore(RestoreOptions options)
    {
        if (Directory.Exists(options.TargetPath) && Directory.EnumerateFileSystemEntries(options.TargetPath).Any() && !options.Overwrite)
            throw new IOException("Target path is not empty. Pass overwrite=true to restore into it.");

        Directory.CreateDirectory(options.TargetPath);
        var (files, _) = ReadImage(options.ImagePath, options.Password, options.TargetPath, options.Overwrite, verifyOnly: false);
        return new RestoreReport(options.ImagePath, options.TargetPath, files.Count, files.Sum(f => f.OriginalLength));
    }

    private static (List<ImageFileEntry> Files, int FormatVersion) ReadImage(string imagePath, string? password, string? restorePath, bool overwrite, bool verifyOnly)
    {
        using var input = File.OpenRead(imagePath);
        var magic = new byte[V1Magic.Length];
        ReadExactly(input, magic);
        if (magic.SequenceEqual(V1Magic))
            return (ReadV1Image(input, password, restorePath, overwrite, verifyOnly), 1);
        if (magic.SequenceEqual(V2Magic))
            return (ReadV2Image(input, imagePath, password, restorePath, overwrite, verifyOnly), 2);
        throw new InvalidDataException("Not a RescueClone image.");
    }

    private static List<ImageFileEntry> ReadV1Image(Stream input, string? password, string? restorePath, bool overwrite, bool verifyOnly)
    {
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

    private static List<ImageFileEntry> ReadV2Image(FileStream input, string imagePath, string? password, string? restorePath, bool overwrite, bool verifyOnly)
    {
        var header = ReadJson<ContainerHeader>(input);
        var compression = Enum.Parse<CompressionMode>(header.Compression);
        if (header.Encrypted && string.IsNullOrEmpty(password))
            throw new InvalidDataException("Image is encrypted; a password is required.");

        var footer = ReadV2Footer(input);
        input.Position = footer.ManifestOffset;
        var manifest = ReadJson<V2Manifest>(input);
        var entries = new List<ImageFileEntry>();

        foreach (var file in manifest.Files)
        {
            var output = verifyOnly || restorePath is null ? null : OpenRestoreTarget(restorePath, file.RelativePath, overwrite);
            using (output)
            using (var hasher = SHA256.Create())
            {
                long storedLength = 0;
                foreach (var block in file.Blocks.OrderBy(b => b.Index))
                {
                    input.Position = block.PayloadOffset;
                    var stored = new byte[checked((int)block.StoredLength)];
                    ReadExactly(input, stored);
                    storedLength += stored.LongLength;
                    var raw = DecodePayload(stored, compression, header.Encrypted ? password : null);
                    if (raw.LongLength != block.OriginalLength)
                        throw new InvalidDataException($"Block length mismatch for {file.RelativePath}.");
                    var blockHash = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
                    if (!StringComparer.OrdinalIgnoreCase.Equals(blockHash, block.Sha256))
                        throw new InvalidDataException($"Block checksum mismatch for {file.RelativePath}.");

                    hasher.TransformBlock(raw, 0, raw.Length, null, 0);
                    output?.Write(raw);
                }

                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var fileHash = Convert.ToHexString(hasher.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
                if (!StringComparer.OrdinalIgnoreCase.Equals(fileHash, file.Sha256))
                    throw new InvalidDataException($"Checksum mismatch for {file.RelativePath}.");

                entries.Add(new ImageFileEntry(file.RelativePath, file.OriginalLength, storedLength, file.Sha256));
            }
        }

        var rootHash = ComputeRootHash(entries);
        if (!StringComparer.OrdinalIgnoreCase.Equals(rootHash, manifest.RootSha256))
            throw new InvalidDataException($"Manifest root checksum mismatch in image: {imagePath}");
        return entries;
    }

    private static FileStream OpenRestoreTarget(string restorePath, string relativePath, bool overwrite)
    {
        var target = Path.GetFullPath(Path.Combine(restorePath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(restorePath);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsafe relative path in image: {relativePath}");
        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? root);
        if (File.Exists(target) && !overwrite)
            throw new IOException($"Target file exists: {target}");
        return File.Create(target);
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

    private static void PrepareImageForWrite(string imagePath)
    {
        if (!File.Exists(imagePath))
            return;

        var attributes = File.GetAttributes(imagePath);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(imagePath, attributes & ~FileAttributes.ReadOnly);
    }

    private static void ProtectImage(string imagePath)
    {
        var attributes = File.GetAttributes(imagePath);
        File.SetAttributes(imagePath, attributes | FileAttributes.ReadOnly);
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

    private static void WriteV2Footer(Stream stream, V2Footer footer)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(footer, JsonOptions);
        stream.Write(json);
        stream.Write(BitConverter.GetBytes(json.LongLength));
        stream.Write(V2FooterMagic);
    }

    private static V2Footer ReadV2Footer(FileStream stream)
    {
        var trailerLength = sizeof(long) + V2FooterMagic.Length;
        if (stream.Length < trailerLength)
            throw new InvalidDataException("Invalid RescueClone v2 footer.");

        stream.Position = stream.Length - V2FooterMagic.Length;
        var footerMagic = new byte[V2FooterMagic.Length];
        ReadExactly(stream, footerMagic);
        if (!footerMagic.SequenceEqual(V2FooterMagic))
            throw new InvalidDataException("Missing RescueClone v2 footer.");

        stream.Position = stream.Length - trailerLength;
        var lengthBytes = new byte[sizeof(long)];
        ReadExactly(stream, lengthBytes);
        var footerLength = BitConverter.ToInt64(lengthBytes);
        if (footerLength < 2 || footerLength > 1024 * 1024)
            throw new InvalidDataException("Invalid RescueClone v2 footer length.");

        stream.Position = stream.Length - trailerLength - footerLength;
        var footerJson = new byte[checked((int)footerLength)];
        ReadExactly(stream, footerJson);
        return JsonSerializer.Deserialize<V2Footer>(footerJson, JsonOptions)
            ?? throw new InvalidDataException("Invalid RescueClone v2 footer record.");
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
    private sealed record V2BlockEntry(int Index, long PayloadOffset, long OriginalLength, long StoredLength, string Sha256);
    private sealed record V2FileManifest(string RelativePath, long OriginalLength, string Sha256, IReadOnlyList<V2BlockEntry> Blocks);
    private sealed record V2Manifest(string RootSha256, long OriginalBytes, long StoredBytes, IReadOnlyList<V2FileManifest> Files);
    private sealed record V2Footer(long ManifestOffset, long ManifestLength);
}
