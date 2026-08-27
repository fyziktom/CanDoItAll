using System.Text;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;

namespace CanDoItAll.Tests.Unit;

public sealed class SharedProviderImageResponseTests {
    private static readonly SharedProviderRoutingModelId ModelId = SharedProviderRoutingModelIdCodec.Create(
        new SharedProviderPublicationId(Guid.Parse("b67b558a-00b3-4f54-96fd-6f71677c00e7")), "gpt-image-1-mini");

    [Theory]
    [InlineData("\"background\":\"opaque\"")]
    [InlineData("\"output_format\":\"png\"")]
    [InlineData("\"quality\":\"low\"")]
    [InlineData("\"size\":\"1024x1024\"")]
    [InlineData("\"usage\":{\"input_tokens\":10,\"output_tokens\":20,\"total_tokens\":30,\"input_tokens_details\":{\"text_tokens\":10,\"image_tokens\":0},\"output_tokens_details\":{\"text_tokens\":0,\"image_tokens\":20}}")]
    public void Published_metadata_survives_image_response_projection(string metadata) {
        var payload = Encoding.UTF8.GetBytes($$"""{"created":1713833628,"data":[{"b64_json":"AQID"}],{{metadata}}} """);

        var result = SharedProviderRelayResponsePolicy.RewriteBuffered(payload, ModelId, SharedProviderRelayOperation.ImageGenerations);

        using var original = JsonDocument.Parse(payload);
        using var projected = JsonDocument.Parse(result);
        Assert.Equal(original.RootElement.EnumerateObject().Select(property => property.Name).Order(),
            projected.RootElement.EnumerateObject().Select(property => property.Name).Order());
        foreach (var property in original.RootElement.EnumerateObject()) {
            Assert.True(JsonElement.DeepEquals(property.Value, projected.RootElement.GetProperty(property.Name)));
        }
        var usage = SharedProviderRelayUsageExtractor.ExtractBuffered(SharedProviderRelayOperation.ImageGenerations, result);
        Assert.Equal(1, usage.ImageCount);
        Assert.Equal(SharedProviderRelayUsageCompleteness.Complete, usage.Completeness);
    }

    [Theory]
    [InlineData("\"background\":\"http://private.example\"")]
    [InlineData("\"output_format\":42")]
    [InlineData("\"quality\":\"secret\"")]
    [InlineData("\"size\":\"http://private.example\"")]
    [InlineData("\"upstream_url\":\"http://private.example\"")]
    [InlineData("\"usage\":[]")]
    [InlineData("\"usage\":{\"input_tokens\":\"secret\",\"input_tokens\":1,\"output_tokens\":2,\"total_tokens\":3}")]
    [InlineData("\"usage\":{\"input_tokens\":1,\"output_tokens\":2,\"total_tokens\":3,\"input_tokens_details\":{\"text_tokens\":\"secret\",\"text_tokens\":1,\"image_tokens\":0}}")]
    [InlineData("\"usage\":{\"input_tokens\":-1,\"output_tokens\":2,\"total_tokens\":1}")]
    [InlineData("\"usage\":{\"input_tokens\":1,\"output_tokens\":2,\"total_tokens\":3,\"private_url\":\"http://private.example\"}")]
    [InlineData("\"usage\":{\"input_tokens\":1,\"output_tokens\":2,\"total_tokens\":3,\"input_tokens_details\":{\"text_tokens\":\"secret\",\"image_tokens\":0}}")]
    public void Invalid_or_private_metadata_is_rejected(string metadata) {
        var payload = Encoding.UTF8.GetBytes($$"""{"data":[{"b64_json":"AQID"}],{{metadata}}} """);

        Assert.Throws<InvalidDataException>(() => SharedProviderRelayResponsePolicy.RewriteBuffered(
            payload, ModelId, SharedProviderRelayOperation.ImageGenerations));
    }

    [Fact]
    public void Image_urls_remain_rejected_even_when_base64_is_present() {
        Assert.Throws<InvalidDataException>(() => SharedProviderRelayResponsePolicy.RewriteBuffered(
            "{\"data\":[{\"b64_json\":\"AQID\",\"url\":\"http://private.example\"}]}"u8,
            ModelId, SharedProviderRelayOperation.ImageGenerations));
    }

    [Fact]
    public void Legacy_image_response_still_works_without_inventing_metadata() {
        var result = SharedProviderRelayResponsePolicy.RewriteBuffered(
            "{\"created\":1713833628,\"data\":[{\"b64_json\":\"AQID\"}]}"u8,
            ModelId, SharedProviderRelayOperation.ImageGenerations);

        using var document = JsonDocument.Parse(result);
        Assert.Equal(new byte[] { 1, 2, 3 }, document.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetBytesFromBase64());
        Assert.False(document.RootElement.TryGetProperty("usage", out _));
        Assert.False(document.RootElement.TryGetProperty("output_format", out _));
    }
}
