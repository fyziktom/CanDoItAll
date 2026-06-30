using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceImageSetEvidenceBuilderTests
{
    [Fact]
    public void Build_reports_downward_motion_for_red_dominant_pixels()
    {
        var before = CreateImage("before.png", CreatePngWithRedBlock(y: 2));
        var after = CreateImage("after.png", CreatePngWithRedBlock(y: 8));

        var evidence = WorkspaceImageSetEvidenceBuilder.Build([before, after]);

        Assert.Contains("red-dominant pixels", evidence, StringComparison.Ordinal);
        Assert.Contains("downward in screen coordinates", evidence, StringComparison.Ordinal);
        Assert.Contains("y=+6.0", evidence, StringComparison.Ordinal);
    }

    private static WorkspaceImageContentResult CreateImage(string path, byte[] bytes)
    {
        return new WorkspaceImageContentResult(
            Succeeded: true,
            Message: "loaded",
            Receipt: new WorkspaceToolReceipt(
                "workspace_analyze_images",
                MutatesWorkspace: false,
                "workspace",
                "Succeeded",
                "loaded",
                string.Empty,
                [path],
                [],
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            Path: path,
            Format: "PNG",
            ContentType: "image/png",
            SizeBytes: bytes.Length,
            Width: 20,
            Height: 20,
            Bytes: bytes,
            Diagnostics: string.Empty);
    }

    private static byte[] CreatePngWithRedBlock(int y)
    {
        const int width = 20;
        const int height = 20;
        var raw = new byte[height * ((width * 4) + 1)];
        for (var row = 0; row < height; row++)
        {
            var offset = row * ((width * 4) + 1);
            raw[offset] = 0;
            for (var column = 0; column < width; column++)
            {
                var pixelOffset = offset + 1 + (column * 4);
                raw[pixelOffset] = 245;
                raw[pixelOffset + 1] = 248;
                raw[pixelOffset + 2] = 252;
                raw[pixelOffset + 3] = 255;
            }
        }

        for (var row = y; row < y + 4; row++)
        {
            for (var column = 8; column < 12; column++)
            {
                var pixelOffset = row * ((width * 4) + 1) + 1 + (column * 4);
                raw[pixelOffset] = 240;
                raw[pixelOffset + 1] = 20;
                raw[pixelOffset + 2] = 30;
                raw[pixelOffset + 3] = 255;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", BuildIhdr(width, height));
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static byte[] BuildIhdr(int width, int height)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        return ihdr;
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write(new byte[4]);
    }
}
