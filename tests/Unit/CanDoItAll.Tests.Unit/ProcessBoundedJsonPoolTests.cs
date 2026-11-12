using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Persistence;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessBoundedJsonPoolTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Growth_and_size_refusal_return_every_buffer_with_clearing(bool refuse) {
        var pool = new TrackingPool();
        var value = Enumerable.Repeat(new BufferedValue(new string('s', 2048)), 64).ToArray();
        JsonSerializerOptions options = new() { DefaultBufferSize = 1024 };
        if (refuse) {
            Assert.Throws<InvalidDataException>(() => ProcessBoundedJson.Serialize(value, options, 40_000, pool));
        } else {
            Assert.Equal(JsonSerializer.Serialize(value, options), ProcessBoundedJson.Serialize(value, options, 200_000, pool));
        }
        Assert.True(pool.Rented.Count > 1);
        pool.AssertClearedAndReturned();
    }

    [Fact]
    public void Serializer_failure_returns_already_written_sensitive_buffers() {
        var pool = new TrackingPool();
        var failure = new JsonException("serialization-stopped");
        JsonSerializerOptions options = new() { DefaultBufferSize = 1024 };
        options.Converters.Add(new FailingConverter(failure));
        var value = Enumerable.Repeat(new BufferedValue(new string('s', 2048)), 64)
            .Append(new BufferedValue(string.Empty, new FailingValue())).ToArray();
        var observed = Assert.Throws<JsonException>(() => ProcessBoundedJson.Serialize(value, options, 200_000, pool));
        Assert.Contains("serialization-stopped", observed.Message, StringComparison.Ordinal);
        Assert.NotEmpty(pool.Rented);
        pool.AssertClearedAndReturned();
    }

    private sealed record BufferedValue(string Text, FailingValue? Failure = null);
    private sealed class FailingValue { }

    private sealed class FailingConverter(JsonException failure) : JsonConverter<FailingValue> {
        public override FailingValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, FailingValue value, JsonSerializerOptions options) {
            writer.WriteStartArray();
            for (var index = 0; index < 16; index++) {
                writer.WriteStringValue(new string('s', 2048));
            }
            writer.Flush();
            throw failure;
        }
    }

    private sealed class TrackingPool : ArrayPool<byte> {
        internal List<byte[]> Rented { get; } = [];
        private readonly HashSet<byte[]> outstanding = [];

        public override byte[] Rent(int minimumLength) {
            var value = new byte[minimumLength];
            Rented.Add(value);
            Assert.True(outstanding.Add(value));
            return value;
        }

        public override void Return(byte[] array, bool clearArray = false) {
            Assert.True(clearArray);
            Assert.True(outstanding.Remove(array));
            Array.Clear(array);
        }

        internal void AssertClearedAndReturned() {
            Assert.Empty(outstanding);
            Assert.All(Rented, value => Assert.True(value.AsSpan().IndexOfAnyExcept((byte)0) < 0));
        }
    }
}
