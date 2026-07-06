using System.Runtime.InteropServices;

namespace RescueClone.Core.Native;

public sealed record NativeBlockPlan(int Index, long Offset, int Length);

public static class NativeBlockPlanner
{
    private const uint AbiVersion = 1;

    public static uint GetAbiVersion() => rc_native_abi_version();

    public static IReadOnlyList<NativeBlockPlan> PlanV2Blocks(long originalLength, int blockSize)
    {
        if (originalLength < 0)
            throw new ArgumentOutOfRangeException(nameof(originalLength));
        if (blockSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize));

        var actualAbi = GetAbiVersion();
        if (actualAbi != AbiVersion)
            throw new InvalidOperationException($"Unsupported RescueClone native ABI version: {actualAbi}");

        var status = rc_v2_plan_blocks((ulong)originalLength, (uint)blockSize, null, 0, out var required);
        if (status < 0)
            throw new InvalidOperationException($"Native block planning failed with status {status}.");
        if (required == 0)
            return Array.Empty<NativeBlockPlan>();

        var nativeBlocks = new RcNativeBlock[required];
        status = rc_v2_plan_blocks((ulong)originalLength, (uint)blockSize, nativeBlocks, required, out required);
        if (status != 0)
            throw new InvalidOperationException($"Native block planning failed with status {status}.");

        return nativeBlocks
            .Select(b => new NativeBlockPlan(checked((int)b.Index), checked((long)b.Offset), checked((int)b.Length)))
            .ToArray();
    }

    [DllImport("RescueClone.Native", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint rc_native_abi_version();

    [DllImport("RescueClone.Native", CallingConvention = CallingConvention.Cdecl)]
    private static extern int rc_v2_plan_blocks(
        ulong originalLength,
        uint blockSize,
        [Out] RcNativeBlock[]? blocks,
        uint capacity,
        out uint required);

    [StructLayout(LayoutKind.Sequential)]
    private struct RcNativeBlock
    {
        public uint Index;
        public ulong Offset;
        public uint Length;
    }
}
