using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.AgentFramework.Maf;

internal static class WorkspaceImageSetEvidenceBuilder
{
    private static readonly ColorTarget[] ColorTargets =
    [
        new("red-dominant", static pixel => pixel.R >= 160 && pixel.R >= pixel.G + 60 && pixel.R >= pixel.B + 60),
        new("green-dominant", static pixel => pixel.G >= 120 && pixel.G >= pixel.R + 40 && pixel.G >= pixel.B + 40),
        new("blue-dominant", static pixel => pixel.B >= 140 && pixel.B >= pixel.R + 50 && pixel.B >= pixel.G + 30),
        new("yellow-dominant", static pixel => pixel.R >= 150 && pixel.G >= 130 && pixel.B <= 120 && Math.Abs(pixel.R - pixel.G) <= 80)
    ];

    public static string Build(IReadOnlyList<WorkspaceImageContentResult> images)
    {
        ArgumentNullException.ThrowIfNull(images);
        if (images.Count < 2)
        {
            return string.Empty;
        }

        var decodedFrames = images
            .Select((image, index) => TryDecodePng(image.Bytes, out var decoded)
                ? new DecodedFrame(index + 1, image.Path, decoded)
                : null)
            .Where(frame => frame is not null)
            .Cast<DecodedFrame>()
            .ToList();
        if (decodedFrames.Count < 2)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Tool-computed pixel evidence from the image files:");
        for (var index = 0; index < decodedFrames.Count - 1 && index < 3; index++)
        {
            AppendFramePairEvidence(builder, decodedFrames[index], decodedFrames[index + 1]);
        }

        return builder.ToString().Trim();
    }

    private static void AppendFramePairEvidence(
        StringBuilder builder,
        DecodedFrame first,
        DecodedFrame second)
    {
        builder.AppendLine($"- Frame {first.Index} file: {Path.GetFileName(first.Path)} ({first.Image.Width}x{first.Image.Height}).");
        builder.AppendLine($"- Frame {second.Index} file: {Path.GetFileName(second.Path)} ({second.Image.Width}x{second.Image.Height}).");
        if (first.Image.Width != second.Image.Width ||
            first.Image.Height != second.Image.Height)
        {
            builder.AppendLine($"- Frames {first.Index}->{second.Index}: dimensions differ, so pixel movement summary is unavailable.");
            return;
        }

        var changedBox = FindChangedBox(first.Image, second.Image);
        if (changedBox.Count > 0)
        {
            builder.AppendLine(
                $"- Frames {first.Index}->{second.Index}: {changedBox.Count:N0} changed pixels, changed region {FormatBox(changedBox)}.");
        }

        foreach (var target in ColorTargets)
        {
            var firstBox = FindColorBox(first.Image, target);
            var secondBox = FindColorBox(second.Image, target);
            if (firstBox.Count == 0 && secondBox.Count == 0)
            {
                continue;
            }

            if (firstBox.Count == 0 || secondBox.Count == 0)
            {
                builder.AppendLine(
                    $"- {target.Name} pixels: frame {first.Index} {FormatOptionalBox(firstBox)}, frame {second.Index} {FormatOptionalBox(secondBox)}.");
                continue;
            }

            var deltaX = secondBox.CenterX - firstBox.CenterX;
            var deltaY = secondBox.CenterY - firstBox.CenterY;
            builder.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "- {0} pixels: frame {1} {2}, frame {3} {4}, center delta x={5:+0.0;-0.0;0.0}, y={6:+0.0;-0.0;0.0} ({7}).",
                    target.Name,
                    first.Index,
                    FormatBox(firstBox),
                    second.Index,
                    FormatBox(secondBox),
                    deltaX,
                    deltaY,
                    DescribeVerticalMovement(deltaY)));
        }
    }

    private static PixelBox FindChangedBox(DecodedImage first, DecodedImage second)
    {
        var box = PixelBox.Empty;
        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                var firstPixel = first.GetPixel(x, y);
                var secondPixel = second.GetPixel(x, y);
                var delta = Math.Abs(firstPixel.R - secondPixel.R) +
                            Math.Abs(firstPixel.G - secondPixel.G) +
                            Math.Abs(firstPixel.B - secondPixel.B);
                if (delta >= 90)
                {
                    box = box.Include(x, y);
                }
            }
        }

        return box;
    }

    private static PixelBox FindColorBox(DecodedImage image, ColorTarget target)
    {
        var box = PixelBox.Empty;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.A >= 128 && target.Matches(pixel))
                {
                    box = box.Include(x, y);
                }
            }
        }

        return box;
    }

    private static string FormatOptionalBox(PixelBox box)
        => box.Count == 0 ? "not detected" : FormatBox(box);

    private static string FormatBox(PixelBox box)
        => string.Format(
            CultureInfo.InvariantCulture,
            "bbox x={0}..{1}, y={2}..{3}, center=({4:0.0},{5:0.0}), count={6:N0}",
            box.MinX,
            box.MaxX,
            box.MinY,
            box.MaxY,
            box.CenterX,
            box.CenterY,
            box.Count);

    private static string DescribeVerticalMovement(double deltaY)
    {
        return deltaY switch
        {
            > 2 => "downward in screen coordinates",
            < -2 => "upward in screen coordinates",
            _ => "no meaningful vertical movement detected"
        };
    }

    private static bool TryDecodePng(byte[] bytes, out DecodedImage image)
    {
        image = default;
        if (bytes.Length < 33 || !HasPngSignature(bytes))
        {
            return false;
        }

        var offset = 8;
        var width = 0;
        var height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        using var idat = new MemoryStream();
        while (offset + 8 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            offset += 4;
            if (length < 0 || offset + 4 + length + 4 > bytes.Length)
            {
                return false;
            }

            var type = Encoding.ASCII.GetString(bytes, offset, 4);
            offset += 4;
            var data = bytes.AsSpan(offset, length);
            offset += length + 4;

            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data[..4]);
                height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4));
                bitDepth = data[8];
                colorType = data[9];
                var compression = data[10];
                var filter = data[11];
                var interlace = data[12];
                if (width <= 0 || height <= 0 || bitDepth != 8 || compression != 0 || filter != 0 || interlace != 0)
                {
                    return false;
                }
            }
            else if (type == "IDAT")
            {
                idat.Write(data);
            }
            else if (type == "IEND")
            {
                break;
            }
        }

        var channels = colorType switch
        {
            0 => 1,
            2 => 3,
            6 => 4,
            _ => 0
        };
        if (width <= 0 || height <= 0 || channels == 0 || idat.Length == 0)
        {
            return false;
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        zlib.CopyTo(decompressed);
        var scanlines = decompressed.ToArray();
        var stride = width * channels;
        var expectedMinimumLength = height * (stride + 1);
        if (scanlines.Length < expectedMinimumLength)
        {
            return false;
        }

        var rgba = new byte[width * height * 4];
        var previous = new byte[stride];
        var current = new byte[stride];
        var sourceOffset = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = scanlines[sourceOffset++];
            for (var x = 0; x < stride; x++)
            {
                var raw = scanlines[sourceOffset++];
                var left = x >= channels ? current[x - channels] : 0;
                var up = previous[x];
                var upLeft = x >= channels ? previous[x - channels] : 0;
                current[x] = filter switch
                {
                    0 => raw,
                    1 => unchecked((byte)(raw + left)),
                    2 => unchecked((byte)(raw + up)),
                    3 => unchecked((byte)(raw + ((left + up) / 2))),
                    4 => unchecked((byte)(raw + Paeth(left, up, upLeft))),
                    _ => raw
                };
            }

            CopyRowToRgba(current, rgba, y * width * 4, width, channels);
            (previous, current) = (current, previous);
            Array.Clear(current);
        }

        image = new DecodedImage(width, height, rgba);
        return true;
    }

    private static bool HasPngSignature(byte[] bytes)
        => bytes[0] == 0x89 &&
           bytes[1] == 0x50 &&
           bytes[2] == 0x4E &&
           bytes[3] == 0x47 &&
           bytes[4] == 0x0D &&
           bytes[5] == 0x0A &&
           bytes[6] == 0x1A &&
           bytes[7] == 0x0A;

    private static void CopyRowToRgba(
        byte[] source,
        byte[] destination,
        int destinationOffset,
        int width,
        int channels)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceOffset = x * channels;
            var targetOffset = destinationOffset + (x * 4);
            if (channels == 1)
            {
                var gray = source[sourceOffset];
                destination[targetOffset] = gray;
                destination[targetOffset + 1] = gray;
                destination[targetOffset + 2] = gray;
                destination[targetOffset + 3] = 255;
                continue;
            }

            destination[targetOffset] = source[sourceOffset];
            destination[targetOffset + 1] = source[sourceOffset + 1];
            destination[targetOffset + 2] = source[sourceOffset + 2];
            destination[targetOffset + 3] = channels == 4 ? source[sourceOffset + 3] : (byte)255;
        }
    }

    private static byte Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);
        if (leftDistance <= upDistance && leftDistance <= upLeftDistance)
        {
            return (byte)left;
        }

        return upDistance <= upLeftDistance ? (byte)up : (byte)upLeft;
    }

    private sealed record ColorTarget(string Name, Func<Pixel, bool> Matches);

    private sealed record DecodedFrame(int Index, string Path, DecodedImage Image);

    private readonly record struct DecodedImage(int Width, int Height, byte[] Rgba)
    {
        public Pixel GetPixel(int x, int y)
        {
            var offset = ((y * Width) + x) * 4;
            return new Pixel(Rgba[offset], Rgba[offset + 1], Rgba[offset + 2], Rgba[offset + 3]);
        }
    }

    private readonly record struct Pixel(byte R, byte G, byte B, byte A);

    private readonly record struct PixelBox(int MinX, int MinY, int MaxX, int MaxY, int Count)
    {
        public static PixelBox Empty => new(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue, 0);

        public double CenterX => (MinX + MaxX) / 2d;

        public double CenterY => (MinY + MaxY) / 2d;

        public PixelBox Include(int x, int y)
            => Count == 0
                ? new PixelBox(x, y, x, y, 1)
                : new PixelBox(Math.Min(MinX, x), Math.Min(MinY, y), Math.Max(MaxX, x), Math.Max(MaxY, y), Count + 1);
    }
}
