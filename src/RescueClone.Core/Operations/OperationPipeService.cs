using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RescueClone.Core.Operations;

public sealed record OperationServiceRequest(
    OperationRequest Request,
    string? LogDirectory = null);

public sealed record OperationServiceResponse(
    bool Succeeded,
    OperationReport? Report,
    string? Error);

public sealed class OperationPipeServer
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly OperationRunner _runner;

    public OperationPipeServer(OperationRunner? runner = null)
    {
        _runner = runner ?? new OperationRunner();
    }

    public async Task RunAsync(string pipeName, string? defaultLogDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
            throw new ArgumentException("Pipe name is required.", nameof(pipeName));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await HandleClientAsync(server, defaultLogDirectory, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(Stream stream, string? defaultLogDirectory, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            AutoFlush = true
        };

        try
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                throw new InvalidDataException("Operation service request is empty.");

            var request = JsonSerializer.Deserialize<OperationServiceRequest>(line, JsonOptions)
                ?? throw new InvalidDataException("Operation service request is invalid.");
            var report = _runner.Run(request.Request, request.LogDirectory ?? defaultLogDirectory);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new OperationServiceResponse(true, report, null), JsonOptions)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new OperationServiceResponse(false, null, ex.Message), JsonOptions)).ConfigureAwait(false);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class OperationPipeClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<OperationServiceResponse> RunOperationAsync(string pipeName, OperationServiceRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pipeName))
            throw new ArgumentException("Pipe name is required.", nameof(pipeName));
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(linked.Token).ConfigureAwait(false);

        using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        await using var writer = new StreamWriter(client, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            AutoFlush = true
        };

        await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions)).ConfigureAwait(false);
        var responseLine = await reader.ReadLineAsync(linked.Token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(responseLine))
            throw new InvalidDataException("Operation service response is empty.");

        return JsonSerializer.Deserialize<OperationServiceResponse>(responseLine, JsonOptions)
            ?? throw new InvalidDataException("Operation service response is invalid.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
