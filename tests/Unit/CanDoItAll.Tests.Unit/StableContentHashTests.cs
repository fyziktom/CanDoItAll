using System.Security.Cryptography;
using System.Text;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class StableContentHashTests
{
    // Fingerprints that callers already persisted or compared (capability editor concurrency tokens, Resources
    // catalog fingerprints) were produced as lowercase hex of SHA-256 over UTF-8. The shared primitive must stay
    // byte-identical to that formula, including empty and non-ASCII payloads.
    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    public void ComputeSha256Hex_matches_published_sha256_vectors(string text, string expected)
    {
        Assert.Equal(expected, StableContentHash.ComputeSha256Hex(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("project:aabbccddeeff00112233445566778899|resource:v1:scope")]
    [InlineData("Příliš žluťoučký kůň úpěl ďábelské ódy")]
    [InlineData("Combining é and precomposed é")]
    [InlineData("Emoji \U0001F600 and CJK 漢字")]
    [InlineData("{\"id\":\"00000000-0000-0000-0000-000000000000\",\"tags\":[\"a\",\"b\"]}")]
    public void ComputeSha256Hex_is_byte_identical_to_the_lowercase_utf8_sha256_formula(string text)
    {
        string previousFormula = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        Assert.Equal(previousFormula, StableContentHash.ComputeSha256Hex(text));
    }

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
