#include <cstdint>

extern "C" {

struct RcNativeBlock
{
    std::uint32_t index;
    std::uint64_t offset;
    std::uint32_t length;
};

__declspec(dllexport) std::uint32_t rc_native_abi_version()
{
    return 1;
}

__declspec(dllexport) int rc_v2_plan_blocks(
    std::uint64_t original_length,
    std::uint32_t block_size,
    RcNativeBlock* blocks,
    std::uint32_t capacity,
    std::uint32_t* required)
{
    if (block_size == 0 || required == nullptr)
        return -1;

    const std::uint64_t count64 = original_length == 0
        ? 0
        : ((original_length - 1) / block_size) + 1;
    if (count64 > UINT32_MAX)
        return -2;

    const auto count = static_cast<std::uint32_t>(count64);
    *required = count;
    if (blocks == nullptr || capacity < count)
        return count == 0 ? 0 : 1;

    for (std::uint32_t i = 0; i < count; ++i)
    {
        const std::uint64_t offset = static_cast<std::uint64_t>(i) * block_size;
        const std::uint64_t remaining = original_length - offset;
        blocks[i].index = i;
        blocks[i].offset = offset;
        blocks[i].length = static_cast<std::uint32_t>(remaining < block_size ? remaining : block_size);
    }

    return 0;
}

}
