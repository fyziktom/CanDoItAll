using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class SharedProviderRoutingModelIdTests
{
    private static readonly SharedProviderPublicationId PublicationId =
        new(Guid.Parse("11111111-2222-3333-4444-555555555555"));

    [Fact]
    public void CreateMatchesTheFrozenStableVector()
    {
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");

        Assert.Equal(
            "sp1.11111111222233334444555555555555.XFGmawr_L2CfrjWMXJ5vcRfOK8WP_5Ad03A4FNyplmQ",
            routingModelId.Value);
    }

    [Fact]
    public void CreateRejectsOuterWhitespaceToPreserveExactModelIdentity()
    {
        Assert.Throws<ArgumentException>(() =>
            SharedProviderRoutingModelIdCodec.Create(PublicationId, " gpt-4.1"));
        Assert.Throws<ArgumentException>(() =>
            SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1 "));
        Assert.Throws<ArgumentException>(() =>
            SharedProviderRoutingModelIdCodec.Create(PublicationId, "\uD800"));
    }

    [Fact]
    public void DuplicateModelNamesFromDifferentPublicationsRemainDistinct()
    {
        var otherPublicationId = new SharedProviderPublicationId(
            Guid.Parse("99999999-8888-7777-6666-555555555555"));

        var first = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");
        var second = SharedProviderRoutingModelIdCodec.Create(otherPublicationId, "gpt-4.1");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void UpstreamModelIdentityIsCaseSensitive()
    {
        var lower = SharedProviderRoutingModelIdCodec.Create(PublicationId, "model-a");
        var upper = SharedProviderRoutingModelIdCodec.Create(PublicationId, "MODEL-A");

        Assert.NotEqual(lower, upper);
    }

    [Fact]
    public void RoutingIdDoesNotRevealUpstreamModelOrInternalProfileId()
    {
        var internalProviderProfileId = Guid.Parse("12345678-aaaa-bbbb-cccc-1234567890ab");
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "private-deployment-name");

        Assert.DoesNotContain("private-deployment-name", routingModelId.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(internalProviderProfileId.ToString("N"), routingModelId.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(internalProviderProfileId.ToString("D"), routingModelId.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParseReturnsOnlyPublicPublicationAndFullFingerprint()
    {
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");

        var parsed = SharedProviderRoutingModelIdCodec.TryParse(
            routingModelId.Value,
            out var reparsedRoutingModelId,
            out var route);

        Assert.True(parsed);
        Assert.Equal(routingModelId, reparsedRoutingModelId);
        Assert.Equal(PublicationId, route!.PublicationId);
        Assert.Equal(64, route.ModelFingerprint.Length);
        Assert.All(route.ModelFingerprint, character => Assert.True(char.IsAsciiHexDigitLower(character)));
    }

    [Fact]
    public void UnknownCodecVersionFailsClosed()
    {
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");
        var malformed = $"sp2{routingModelId.Value[3..]}";

        Assert.False(SharedProviderRoutingModelIdCodec.TryParse(malformed, out _, out _));
        Assert.Throws<FormatException>(() => SharedProviderRoutingModelIdCodec.Parse(malformed));
    }

    [Fact]
    public void UppercasePublicationEncodingFailsClosed()
    {
        var publicationId = new SharedProviderPublicationId(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(publicationId, "gpt-4.1");
        var uppercase = routingModelId.Value.ToUpperInvariant();

        Assert.False(SharedProviderRoutingModelIdCodec.TryParse(uppercase, out _, out _));
    }

    [Fact]
    public void TruncatedOrNonBase64UrlFingerprintFailsClosed()
    {
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");
        var truncated = routingModelId.Value[..^1];
        var invalidAlphabet = $"{routingModelId.Value[..^1]}!";

        Assert.False(SharedProviderRoutingModelIdCodec.TryParse(truncated, out _, out _));
        Assert.False(SharedProviderRoutingModelIdCodec.TryParse(invalidAlphabet, out _, out _));
    }

    [Fact]
    public void MatchesRequiresTheExactPublicationAndModel()
    {
        var routingModelId = SharedProviderRoutingModelIdCodec.Create(PublicationId, "gpt-4.1");
        var otherPublicationId = new SharedProviderPublicationId(
            Guid.Parse("99999999-8888-7777-6666-555555555555"));

        Assert.True(SharedProviderRoutingModelIdCodec.Matches(routingModelId, PublicationId, "gpt-4.1"));
        Assert.False(SharedProviderRoutingModelIdCodec.Matches(routingModelId, PublicationId, "gpt-4.1-mini"));
        Assert.False(SharedProviderRoutingModelIdCodec.Matches(routingModelId, otherPublicationId, "gpt-4.1"));
    }
}
