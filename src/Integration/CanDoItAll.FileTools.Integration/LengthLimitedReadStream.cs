namespace CanDoItAll.FileTools.Integration;

internal sealed class LengthLimitedReadStream(Stream inner, long maximumLength) : Stream
{
    private long _read;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => Math.Min(inner.CanSeek ? inner.Length - inner.Position : maximumLength, maximumLength);

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int allowed = Limit(count);
        if (allowed == 0)
        {
            return 0;
        }

        int read = inner.Read(buffer, offset, allowed);
        _read += read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int allowed = Limit(buffer.Length);
        if (allowed == 0)
        {
            return 0;
        }

        int read = inner.Read(buffer[..allowed]);
        _read += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        int allowed = Limit(buffer.Length);
        if (allowed == 0)
        {
            return 0;
        }

        int read = await inner.ReadAsync(buffer[..allowed], cancellationToken);
        _read += read;
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
        => ReadArrayAsync(buffer, offset, count, cancellationToken);

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private int Limit(int requested)
        => (int)Math.Min(requested, Math.Max(0, maximumLength - _read));

    private async Task<int> ReadArrayAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
        => await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
}
