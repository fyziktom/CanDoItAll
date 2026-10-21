using System.Buffers;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessBoundedJson {
    internal static string Serialize<T>(T value, JsonSerializerOptions options, int maximumBytes, ArrayPool<byte>? pool = null) {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1);
        using var buffer = new BoundedBuffer(maximumBytes, pool ?? ArrayPool<byte>.Shared);
        JsonSerializer.Serialize(buffer, value, options);
        return Encoding.UTF8.GetString(buffer.Written);
    }

    private sealed class BoundedBuffer(int maximumBytes, ArrayPool<byte> pool) : Stream {
        private byte[] buffer = [];
        private int written;
        private bool disposed;
        internal ReadOnlySpan<byte> Written => buffer.AsSpan(0, written);
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !disposed;
        public override long Length => written;
        public override long Position {
            get => written;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] value, int offset, int count) => Write(value.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> value) {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (value.Length > maximumBytes - written) {
                throw new InvalidDataException("The Process JSON payload exceeds its retained UTF-8 byte limit.");
            }
            var required = written + value.Length;
            if (required > buffer.Length) {
                var capacity = (int)Math.Min(maximumBytes, Math.Max(required, Math.Max(16_384L, 2L * buffer.Length)));
                var replacement = pool.Rent(capacity);
                Written.CopyTo(replacement);
                var previous = buffer;
                buffer = replacement;
                Return(previous);
            }
            value.CopyTo(buffer.AsSpan(written));
            written = required;
        }

        public override void WriteByte(byte value) {
            Span<byte> single = stackalloc byte[1];
            single[0] = value;
            Write(single);
        }

        public override void Flush() => ObjectDisposedException.ThrowIf(disposed, this);
        public override int Read(byte[] value, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            if (disposing && !disposed) {
                disposed = true;
                var previous = buffer;
                buffer = [];
                written = 0;
                Return(previous);
            }
            base.Dispose(disposing);
        }

        private void Return(byte[] value) {
            if (value.Length > 0) {
                pool.Return(value, clearArray: true);
            }
        }
    }
}
