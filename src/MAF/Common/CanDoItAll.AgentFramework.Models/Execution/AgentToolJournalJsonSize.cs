using System.Text.Json;

namespace CanDoItAll.AgentFramework.Models;

internal static class AgentToolJournalJsonSize {
    internal static long Count(AgentToolJournalRecord journal) {
        ArgumentNullException.ThrowIfNull(journal);
        using var stream = new CountingStream();
        JsonSerializer.Serialize(stream, journal);
        return stream.BytesWritten;
    }

    private sealed class CountingStream : Stream {
        internal long BytesWritten { get; private set; }

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

        public override void Write(ReadOnlySpan<byte> buffer)
            => BytesWritten = checked(BytesWritten + buffer.Length);

        public override void WriteByte(byte value)
            => BytesWritten = checked(BytesWritten + 1);

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
