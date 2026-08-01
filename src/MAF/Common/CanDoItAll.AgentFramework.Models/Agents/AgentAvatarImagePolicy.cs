using System.Buffers.Binary;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentAvatarImageInfo(
    string ContentType,
    int ByteCount,
    int Width,
    int Height);

public static class AgentAvatarImagePolicy
{
    public const int MaxAvatarBytes = 128 * 1024;
    public const int MinAvatarDimension = 32;
    public const int MaxAvatarDimension = 2_048;

    private static readonly IReadOnlyDictionary<string, string> SupportedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = "image/png",
            ["image/jpeg"] = "image/jpeg",
            ["image/jpg"] = "image/jpeg",
            ["image/webp"] = "image/webp",
            ["image/gif"] = "image/gif"
        };

    public static bool IsSupportedContentType(string? contentType)
    {
        return TryNormalizeContentType(contentType, out _);
    }

    public static AgentAvatarImageInfo InspectGeneratedJpeg(string? contentType, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ValidateByteLength(bytes);
        if (!TryNormalizeContentType(contentType, out var normalizedContentType) ||
            !string.Equals(normalizedContentType, "image/jpeg", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Generated avatar content type must be image/jpeg and match the requested JPEG format.");
        }

        if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[2] != 0xff)
        {
            throw InvalidJpeg("file signature is invalid");
        }

        var (width, height) = InspectJpegStructure(bytes);
        if (width != height)
        {
            throw new InvalidOperationException("Generated avatar image must be square.");
        }

        if (width is < MinAvatarDimension or > MaxAvatarDimension)
        {
            throw new InvalidOperationException(
                $"Generated avatar dimensions must be between {MinAvatarDimension} and {MaxAvatarDimension} pixels.");
        }

        return new AgentAvatarImageInfo(normalizedContentType, bytes.Length, width, height);
    }

    public static string BuildDataUrl(string? contentType, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (!TryNormalizeContentType(contentType, out var normalizedContentType))
        {
            throw new InvalidOperationException("Avatar image must be PNG, JPEG, WebP, or GIF.");
        }

        ValidateByteLength(bytes);
        return $"data:{normalizedContentType};base64,{Convert.ToBase64String(bytes)}";
    }

    public static bool TryNormalizeContentType(string? contentType, out string normalizedContentType)
    {
        normalizedContentType = string.Empty;
        if (string.IsNullOrWhiteSpace(contentType) ||
            !SupportedContentTypes.TryGetValue(contentType.Trim(), out var normalized) ||
            normalized is null)
        {
            return false;
        }

        normalizedContentType = normalized;
        return true;
    }

    public static bool TryNormalizeContentType(
        ReadOnlySpan<char> contentType,
        out string normalizedContentType)
    {
        normalizedContentType = string.Empty;
        var normalized = contentType.Trim();
        if (normalized.Equals("image/png".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            normalizedContentType = "image/png";
            return true;
        }

        if (normalized.Equals("image/jpeg".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("image/jpg".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            normalizedContentType = "image/jpeg";
            return true;
        }

        if (normalized.Equals("image/webp".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            normalizedContentType = "image/webp";
            return true;
        }

        if (normalized.Equals("image/gif".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            normalizedContentType = "image/gif";
            return true;
        }

        return false;
    }

    private static void ValidateByteLength(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("Avatar image file is empty.");
        }

        if (bytes.Length > MaxAvatarBytes)
        {
            throw new InvalidOperationException($"Avatar image must be {MaxAvatarBytes / 1024} KB or smaller.");
        }
    }

    private static (int Width, int Height) InspectJpegStructure(ReadOnlySpan<byte> bytes)
    {
        var offset = 2;
        var width = 0;
        var height = 0;
        var sawFrame = false;
        var sawScan = false;
        while (offset < bytes.Length)
        {
            if (bytes[offset] != 0xff)
            {
                throw InvalidJpeg("marker stream is invalid");
            }

            while (offset < bytes.Length && bytes[offset] == 0xff)
            {
                offset++;
            }

            if (offset >= bytes.Length)
            {
                throw InvalidJpeg("image is truncated");
            }

            var marker = bytes[offset++];
            if (marker == 0xd9)
            {
                if (!sawFrame || !sawScan || offset != bytes.Length)
                {
                    throw InvalidJpeg("frame, scan, or end marker is missing");
                }

                return (width, height);
            }

            if (marker is 0x00 or 0x01 or 0xd8 or >= 0xd0 and <= 0xd7 ||
                bytes.Length - offset < 2)
            {
                throw InvalidJpeg("segment marker is invalid");
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (segmentLength < 2 || segmentLength > bytes.Length - offset)
            {
                throw InvalidJpeg("segment is truncated");
            }

            var segment = bytes.Slice(offset + 2, segmentLength - 2);
            offset += segmentLength;
            if (IsStartOfFrame(marker))
            {
                if (segment.Length < 6)
                {
                    throw InvalidJpeg("frame header is truncated");
                }

                var componentCount = segment[5];
                if (componentCount == 0 || segmentLength != 8 + 3 * componentCount)
                {
                    throw InvalidJpeg("frame header is invalid");
                }

                height = BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(1, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(3, 2));
                sawFrame = true;
                continue;
            }

            if (marker == 0xda)
            {
                if (!sawFrame || segment.Length < 4)
                {
                    throw InvalidJpeg("scan header is invalid");
                }

                var componentCount = segment[0];
                if (componentCount == 0 || segmentLength != 6 + 2 * componentCount)
                {
                    throw InvalidJpeg("scan header is invalid");
                }

                sawScan = true;
                offset = SkipEntropyData(bytes, offset);
            }
        }

        throw InvalidJpeg("end marker is missing");
    }

    private static int SkipEntropyData(ReadOnlySpan<byte> bytes, int offset)
    {
        while (offset < bytes.Length)
        {
            if (bytes[offset] != 0xff)
            {
                offset++;
                continue;
            }

            var markerOffset = offset;
            while (offset < bytes.Length && bytes[offset] == 0xff)
            {
                offset++;
            }

            if (offset >= bytes.Length)
            {
                throw InvalidJpeg("entropy data is truncated");
            }

            var marker = bytes[offset];
            if (marker == 0x00 || marker is >= 0xd0 and <= 0xd7)
            {
                offset++;
                continue;
            }

            return markerOffset;
        }

        throw InvalidJpeg("end marker is missing");
    }

    private static bool IsStartOfFrame(byte marker)
    {
        return marker is 0xc0 or 0xc1 or 0xc2 or 0xc3 or
            0xc5 or 0xc6 or 0xc7 or
            0xc9 or 0xca or 0xcb or
            0xcd or 0xce or 0xcf;
    }

    private static InvalidOperationException InvalidJpeg(string reason)
    {
        return new InvalidOperationException($"Generated avatar JPEG cannot be decoded: {reason}.");
    }
}
