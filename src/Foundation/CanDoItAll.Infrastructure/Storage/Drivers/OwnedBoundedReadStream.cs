namespace CanDoItAll.Infrastructure.Storage;

internal sealed class OwnedBoundedReadStream(
    Stream inner,
    IDisposable owner,
    long maximumBytes) : Stream
{
    private long _bytesRead;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = inner.Read(buffer, offset, LimitCount(count));
        RecordRead(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = inner.Read(buffer[..LimitCount(buffer.Length)]);
        RecordRead(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        int read = await inner.ReadAsync(buffer[..LimitCount(buffer.Length)], cancellationToken);
        RecordRead(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
            owner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        owner.Dispose();
        GC.SuppressFinalize(this);
    }

    private int LimitCount(int requested)
    {
        long remaining = maximumBytes - _bytesRead;
        if (remaining <= 0)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.BudgetExceeded,
                "The remote content stream exceeded its byte limit."));
        }

        return (int)Math.Min(requested, remaining);
    }

    private void RecordRead(int read)
    {
        _bytesRead += read;
    }
}
