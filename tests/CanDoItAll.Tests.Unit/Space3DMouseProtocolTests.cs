using CanDoItAll.Space3D.Mouse.Driver.Protocol;
using System.Numerics;

namespace CanDoItAll.Tests.Unit;

public sealed class Space3DMouseProtocolTests
{
    [Fact]
    public void Valid_v103_frame_parses_with_legacy_source_bits()
    {
        var frame = BuildTelemetryFrame(protocolMinor: 3, sourceValue: 2, sourceStatus: 0, gyroX: -12d);

        var parsed = Space3DMouseProtocol.TryParseTelemetry(frame, out var telemetry, out var reason);

        Assert.True(parsed, reason);
        Assert.Equal(MouseOrientationSource.RotationVectorMag, telemetry.OrientationSource);
        Assert.True(telemetry.OrientationSourceUsesMagnetometer);
        Assert.Equal(-12d, telemetry.GyroDps.X, precision: 1);
    }

    [Fact]
    public void Valid_v104_frame_parses_exact_source_and_status()
    {
        var frame = BuildTelemetryFrame(
            protocolMinor: 4,
            sourceValue: 2,
            sourceStatus: 0x14,
            accelY: -0.125d);

        var parsed = Space3DMouseProtocol.TryParseTelemetry(frame, out var telemetry, out var reason);

        Assert.True(parsed, reason);
        Assert.Equal(MouseOrientationSource.GameRotationVectorNoMag, telemetry.OrientationSource);
        Assert.False(telemetry.OrientationSourceUsesMagnetometer);
        Assert.True(telemetry.ReportsReconfigured);
        Assert.True(telemetry.RuntimeSettingsValid);
        Assert.Equal(-0.125d, telemetry.LinearAccelG.Y, precision: 3);
    }

    [Fact]
    public void Corrupted_crc_rejects_frame()
    {
        var frame = BuildTelemetryFrame(protocolMinor: 4, sourceValue: 2, sourceStatus: 0x10);
        frame[^2] ^= 0x01;

        Assert.False(Space3DMouseProtocol.TryParseTelemetry(frame, out _, out var reason));
        Assert.Contains("CRC mismatch", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Corrupted_sysex_terminator_rejects_frame()
    {
        var frame = BuildTelemetryFrame(protocolMinor: 4, sourceValue: 2, sourceStatus: 0x10);
        frame[^1] = 0x00;

        Assert.False(Space3DMouseProtocol.TryParseTelemetry(frame, out _, out var reason));
        Assert.Contains("Bad SysEx envelope", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_message_type_rejects_frame()
    {
        var frame = BuildTelemetryFrame(protocolMinor: 4, sourceValue: 2, sourceStatus: 0x10);
        frame[9] = 0x32;

        Assert.False(Space3DMouseProtocol.TryParseTelemetry(frame, out _, out var reason));
        Assert.Contains("Unexpected message type", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsupported_future_protocol_minor_rejects_frame()
    {
        var frame = BuildTelemetryFrame(protocolMinor: 5, sourceValue: 2, sourceStatus: 0x10);

        Assert.False(Space3DMouseProtocol.TryParseTelemetry(frame, out _, out var reason));
        Assert.Contains("Unsupported protocol version", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_unit_orientation_quaternion_is_normalized()
    {
        var frame = BuildTelemetryFrame(protocolMinor: 4, sourceValue: 2, sourceStatus: 0x10, qW: 0.5d, qX: 0.5d);

        var parsed = Space3DMouseProtocol.TryParseTelemetry(frame, out var telemetry, out var reason);

        Assert.True(parsed, reason);
        Assert.InRange(telemetry.RelativeOrientation.Length(), 0.9999f, 1.0001f);
    }

    [Fact]
    public void Zero_orientation_quaternion_falls_back_to_identity()
    {
        var frame = BuildTelemetryFrame(protocolMinor: 4, sourceValue: 2, sourceStatus: 0x10, qW: 0d);

        var parsed = Space3DMouseProtocol.TryParseTelemetry(frame, out var telemetry, out var reason);

        Assert.True(parsed, reason);
        Assert.Equal(Quaternion.Identity, telemetry.RelativeOrientation);
    }

    private static int[] BuildTelemetryFrame(
        int protocolMinor,
        int sourceValue,
        int sourceStatus,
        double qW = 1d,
        double qX = 0d,
        double qY = 0d,
        double qZ = 0d,
        double gyroX = 0d,
        double accelY = 0d)
    {
        var bytes = new List<int>
        {
            0xF0,
            0x7D,
            'I',
            'D',
            'R',
            'M',
            0x01,
            protocolMinor & 0x7F,
            0x09,
            0x31,
            0x01,
            0x01
        };

        var flags = 0x1F;
        var status = 0x3F;
        if (protocolMinor < 4)
        {
            if ((sourceValue & 0x02) != 0)
            {
                flags |= 0x40;
            }

            if ((sourceValue & 0x01) != 0)
            {
                status |= 0x40;
            }
        }

        bytes.Add(flags & 0x7F);
        bytes.Add(status & 0x7F);
        if (protocolMinor >= 4)
        {
            bytes.Add(sourceValue & 0x7F);
            bytes.Add(sourceStatus & 0x7F);
        }

        bytes.Add(0x04);
        bytes.Add(0x03);
        AppendUnsigned14(bytes, 612);
        AppendUnsigned14(bytes, 493);
        bytes.Add(0x04);
        bytes.Add(0x07);
        AppendUnsigned14(bytes, 598);
        AppendUnsigned14(bytes, 482);

        AppendSigned14(bytes, QuantizeSigned(qW, 4096d));
        AppendSigned14(bytes, QuantizeSigned(qX, 4096d));
        AppendSigned14(bytes, QuantizeSigned(qY, 4096d));
        AppendSigned14(bytes, QuantizeSigned(qZ, 4096d));
        AppendSigned14(bytes, QuantizeSigned(gyroX, 2d));
        AppendSigned14(bytes, 0);
        AppendSigned14(bytes, 0);
        AppendSigned14(bytes, 0);
        AppendSigned14(bytes, QuantizeSigned(accelY, 1024d));
        AppendSigned14(bytes, 0);

        bytes.Add(ComputeCrc7(bytes, 1, bytes.Count));
        bytes.Add(0xF7);
        return bytes.ToArray();
    }

    private static void AppendUnsigned14(List<int> bytes, int value)
    {
        bytes.Add(value & 0x7F);
        bytes.Add((value >> 7) & 0x7F);
    }

    private static void AppendSigned14(List<int> bytes, int value)
        => AppendUnsigned14(bytes, Math.Clamp(value + 8192, 0, 16383));

    private static int QuantizeSigned(double value, double scale)
        => (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);

    private static int ComputeCrc7(IReadOnlyList<int> bytes, int offset, int endExclusive)
    {
        var crc = 0;
        for (var index = offset; index < endExclusive; index++)
        {
            crc ^= bytes[index] & 0x7F;
        }

        return crc & 0x7F;
    }
}
