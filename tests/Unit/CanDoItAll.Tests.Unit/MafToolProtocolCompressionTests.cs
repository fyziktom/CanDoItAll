using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafToolProtocolCompressionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_provider_protocol_round_trips_in_a_small_checkpoint(bool streaming) {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, [
            new TextContent(new string('x', 128 * 1024)),
            new FunctionCallContent("call-1", "build_application", new Dictionary<string, object?> { ["project"] = "Tetris" })]));
        if (streaming) {
            AssertCompactRoundTrip(response.ToChatResponseUpdates().ToArray());
        } else {
            AssertCompactRoundTrip(response);
        }
    }

    [Fact]
    public void Legacy_uncompressed_response_remains_readable() {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "legacy response"));
        var packages = string.Join("|", typeof(ChatResponse).Assembly.GetName().Version,
            typeof(OpenAIClient).Assembly.GetName().Version, typeof(OllamaApiClient).Assembly.GetName().Version);
        var payload = JsonSerializer.Serialize(new {
            Packages = packages, Shape = typeof(ChatResponse).FullName,
            Value = JsonSerializer.SerializeToElement(response, MafToolProtocolCodec.SerializationOptions)
        });
        var legacy = AgentToolProtocolEnvelope.Create(MafToolProtocolCodec.Format, 1, payload);

        Assert.Equal(response.Text, MafToolProtocolCodec.Decode<ChatResponse>(legacy).Text);
    }

    [Fact]
    public void Invalid_compressed_response_requires_explicit_recovery() {
        var envelope = AgentToolProtocolEnvelope.Create(MafToolProtocolCodec.Format,
            MafToolProtocolCodec.CompressedResponseVersion, JsonSerializer.Serialize(new { Brotli = new byte[] { 1, 2, 3 } }));

        Assert.Throws<AgentToolAdmissionException>(() => MafToolProtocolCodec.Decode<ChatResponse>(envelope));
    }

    [Fact]
    public void Compressed_payload_cannot_bypass_the_uncompressed_size_bound() {
        using var output = new MemoryStream();
        using (var compressor = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true)) {
            compressor.Write(new byte[AgentToolProtocolEnvelope.MaximumUtf8Bytes + 1]);
        }
        var envelope = AgentToolProtocolEnvelope.Create(MafToolProtocolCodec.Format,
            MafToolProtocolCodec.CompressedResponseVersion, JsonSerializer.Serialize(new { Brotli = output.ToArray() }));

        Assert.Throws<AgentToolAdmissionException>(() => MafToolProtocolCodec.Decode<ChatResponse>(envelope));
    }

    [Fact]
    public void Oversized_original_response_is_rejected_before_compression() {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant,
            new string('x', AgentToolProtocolEnvelope.MaximumUtf8Bytes)));

        Assert.Throws<AgentToolAdmissionException>(() => MafToolProtocolCodec.Encode(response));
    }

    private static void AssertCompactRoundTrip<T>(T value) {
        var expected = JsonSerializer.Serialize(value, MafToolProtocolCodec.SerializationOptions);
        var envelope = MafToolProtocolCodec.Encode(value);
        var restored = MafToolProtocolCodec.Decode<T>(envelope);

        Assert.Equal(MafToolProtocolCodec.CompressedResponseVersion, envelope.Version);
        Assert.True(Encoding.UTF8.GetByteCount(envelope.PayloadJson) < 4096);
        Assert.Equal(expected, JsonSerializer.Serialize(restored, MafToolProtocolCodec.SerializationOptions));
        Assert.Equal(MafToolProtocolCodec.Digest(value), MafToolProtocolCodec.Digest(restored));
    }
}
