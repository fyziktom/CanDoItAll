using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Persistence;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessBoundedJsonTests {
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("\"\\\r\n")]
    [InlineData("Český text 🙂")]
    public void Exact_byte_limit_preserves_canonical_json_and_refuses_one_byte_less(string value) {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        var expected = JsonSerializer.Serialize(value, options);
        var bytes = Encoding.UTF8.GetByteCount(expected);
        Assert.Equal(expected, ProcessBoundedJson.Serialize(value, options, bytes));
        Assert.Throws<InvalidDataException>(() => ProcessBoundedJson.Serialize(value, options, bytes - 1));
    }

    [Fact]
    public void Multiple_serializer_flushes_cannot_exceed_the_total_output_limit() {
        var value = Enumerable.Repeat(new BufferedValue(new string('x', 2048)), 64).ToArray();
        JsonSerializerOptions options = new() { DefaultBufferSize = 1024 };
        var expected = JsonSerializer.Serialize(value, options);
        var bytes = Encoding.UTF8.GetByteCount(expected);
        Assert.Equal(expected, ProcessBoundedJson.Serialize(value, options, bytes));
        Assert.Throws<InvalidDataException>(() => ProcessBoundedJson.Serialize(value, options, bytes - 1));
        var visited = 0;
        Assert.Throws<InvalidDataException>(() => ProcessBoundedJson.Serialize(Enumerate(), options, 40_000));
        Assert.InRange(visited, 2, value.Length - 1);

        IEnumerable<BufferedValue> Enumerate() {
            foreach (var item in value) {
                visited++;
                yield return item;
            }
        }
    }

    [Fact]
    public void A_single_large_escaped_value_is_refused_without_returning_partial_json() {
        JsonSerializerOptions options = new() { DefaultBufferSize = 1024 };
        var value = new string('\u0001', 8192);
        Assert.Throws<InvalidDataException>(() => ProcessBoundedJson.Serialize(value, options, 4096));
    }

    private sealed record BufferedValue(string Text);
}
