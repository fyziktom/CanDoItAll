using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentToolJournalWireEquality {
    internal static bool AreEqual(AgentToolJournalRecord left, AgentToolJournalRecord right) {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var expected = JsonSerializer.SerializeToUtf8Bytes(left);
        using var stream = new ComparisonStream(expected);
        JsonSerializer.Serialize(stream, right);
        return stream.Matches;
    }

    private sealed class ComparisonStream(byte[] expected) : Stream {
        private long written;
        private bool mismatch;

        internal bool Matches => !mismatch && written == expected.LongLength;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            ValidateBufferArguments(buffer, offset, count);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer) {
            if (!mismatch && (written > expected.LongLength - buffer.Length ||
                !buffer.SequenceEqual(expected.AsSpan((int)written, buffer.Length)))) {
                mismatch = true;
            }
            written = checked(written + buffer.Length);
        }

        public override void WriteByte(byte value) {
            ReadOnlySpan<byte> buffer = [value];
            Write(buffer);
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
