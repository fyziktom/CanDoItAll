using System.Text.Json;

namespace CanDoItAll.Infrastructure.Storage;

internal sealed class NestedJsonArrayReadStream(
    Stream source,
    string propertyName) : Stream
{
    private readonly byte[] _propertyName = System.Text.Encoding.UTF8.GetBytes(propertyName);
    private readonly byte[] _sourceBuffer = new byte[8 * 1024];
    private readonly byte[] _candidate = new byte[64];
    private int _sourceOffset;
    private int _sourceCount;
    private bool _started;
    private bool _completed;
    private bool _insideString;
    private bool _escaped;
    private int _arrayDepth;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (buffer.Length == 0 || _completed)
        {
            return 0;
        }

        int written = 0;
        if (!_started)
        {
            await LocateArrayAsync(cancellationToken);
            buffer.Span[written++] = (byte)'[';
            if (written == buffer.Length)
            {
                return written;
            }
        }

        while (written < buffer.Length && !_completed)
        {
            int value = await ReadSourceByteAsync(cancellationToken);
            if (value < 0)
            {
                throw new JsonException("The remote JSON array ended unexpectedly.");
            }

            byte current = (byte)value;
            buffer.Span[written++] = current;
            TrackArrayByte(current);
        }

        return written;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private async ValueTask LocateArrayAsync(CancellationToken cancellationToken)
    {
        int candidateLength = 0;
        bool inString = false;
        bool escaped = false;
        bool candidateOverflow = false;
        while (true)
        {
            int value = await ReadSourceByteAsync(cancellationToken);
            if (value < 0)
            {
                throw new JsonException($"The remote JSON response did not contain '{propertyName}'.");
            }

            byte current = (byte)value;
            if (!inString)
            {
                if (current == (byte)'"')
                {
                    inString = true;
                    escaped = false;
                    candidateOverflow = false;
                    candidateLength = 0;
                }

                continue;
            }

            if (escaped)
            {
                escaped = false;
                candidateOverflow = true;
                continue;
            }

            if (current == (byte)'\\')
            {
                escaped = true;
                candidateOverflow = true;
                continue;
            }

            if (current != (byte)'"')
            {
                if (candidateLength < _candidate.Length)
                {
                    _candidate[candidateLength++] = current;
                }
                else
                {
                    candidateOverflow = true;
                }

                continue;
            }

            inString = false;
            if (candidateOverflow ||
                candidateLength != _propertyName.Length ||
                !_candidate.AsSpan(0, candidateLength).SequenceEqual(_propertyName))
            {
                continue;
            }

            if (await ReadNextNonWhitespaceAsync(cancellationToken) != (byte)':')
            {
                continue;
            }

            if (await ReadNextNonWhitespaceAsync(cancellationToken) != (byte)'[')
            {
                throw new JsonException($"The remote JSON property '{propertyName}' is not an array.");
            }

            _started = true;
            _arrayDepth = 1;
            return;
        }
    }

    private async ValueTask<byte> ReadNextNonWhitespaceAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            int value = await ReadSourceByteAsync(cancellationToken);
            if (value < 0)
            {
                throw new JsonException("The remote JSON response ended unexpectedly.");
            }

            byte current = (byte)value;
            if (current is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                return current;
            }
        }
    }

    private async ValueTask<int> ReadSourceByteAsync(CancellationToken cancellationToken)
    {
        if (_sourceOffset == _sourceCount)
        {
            _sourceCount = await source.ReadAsync(_sourceBuffer, cancellationToken);
            _sourceOffset = 0;
            if (_sourceCount == 0)
            {
                return -1;
            }
        }

        return _sourceBuffer[_sourceOffset++];
    }

    private void TrackArrayByte(byte current)
    {
        if (_insideString)
        {
            if (_escaped)
            {
                _escaped = false;
            }
            else if (current == (byte)'\\')
            {
                _escaped = true;
            }
            else if (current == (byte)'"')
            {
                _insideString = false;
            }

            return;
        }

        if (current == (byte)'"')
        {
            _insideString = true;
        }
        else if (current == (byte)'[')
        {
            _arrayDepth++;
        }
        else if (current == (byte)']' && --_arrayDepth == 0)
        {
            _completed = true;
        }
    }
}
