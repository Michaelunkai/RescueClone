using System.Text.Json;

namespace RescueClone.Core.Operations;

public enum OperationState
{
    Succeeded,
    Failed
}

public sealed record OperationRequest(
    string Kind,
    Dictionary<string, JsonElement> Parameters,
    string? OperationId = null);

public sealed record OperationReport(
    string OperationId,
    string Kind,
    OperationState State,
    DateTimeOffset StartedUtc,
    DateTimeOffset FinishedUtc,
    string? LogPath,
    JsonElement? Result,
    string? Error,
    string? RecoveryStatePath = null);

public sealed record OperationRecoveryState(
    OperationRequest Request,
    OperationReport Report,
    DateTimeOffset WrittenUtc);
