using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class StableContentHashTests
{
    [Fact]
    public void ComputeShortSha256Hex_returns_stable_lowercase_prefix()
    {
        var hash = StableContentHash.ComputeShortSha256Hex("hello");

        Assert.Equal("2cf24dba5fb0", hash);
    }

    [Fact]
    public void ComputeShortSha256Hex_rejects_invalid_byte_count()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StableContentHash.ComputeShortSha256Hex("hello", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StableContentHash.ComputeShortSha256Hex("hello", 33));
    }
}
