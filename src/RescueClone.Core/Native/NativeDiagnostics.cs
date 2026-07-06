namespace RescueClone.Core.Native;

public sealed record NativeEngineStatus(
    bool Loaded,
    uint AbiVersion,
    int SampleBlockCount,
    string Detail);

public static class NativeDiagnostics
{
    public static NativeEngineStatus GetStatus()
    {
        var sample = NativeBlockPlanner.PlanV2Blocks(3 * 1024 * 1024L + 7, 1024 * 1024);
        return new NativeEngineStatus(
            Loaded: true,
            AbiVersion: NativeBlockPlanner.GetAbiVersion(),
            SampleBlockCount: sample.Count,
            Detail: "Native C++ v2 block planner is available.");
    }
}
