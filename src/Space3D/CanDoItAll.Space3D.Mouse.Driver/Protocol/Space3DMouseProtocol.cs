using System.Numerics;

namespace CanDoItAll.Space3D.Mouse.Driver.Protocol;

public enum MouseOrientationSource
{
    Unknown = 0,
    Geomagnetic = 1,
    RotationVector = 2,
    ArvrStabilized = 3
}

public readonly record struct SceneVector(double X, double Y, double Z)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public static SceneVector Zero => new(0d, 0d, 0d);

    public static SceneVector operator +(SceneVector left, SceneVector right)
        => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static SceneVector operator -(SceneVector left, SceneVector right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static SceneVector operator *(SceneVector value, double scalar)
        => new(value.X * scalar, value.Y * scalar, value.Z * scalar);

    public static SceneVector operator /(SceneVector value, double scalar)
        => Math.Abs(scalar) <= 0.000001d
            ? Zero
            : new(value.X / scalar, value.Y / scalar, value.Z / scalar);

    public double Dot(SceneVector other)
        => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    public SceneVector Cross(SceneVector other)
        => new(
            (Y * other.Z) - (Z * other.Y),
            (Z * other.X) - (X * other.Z),
            (X * other.Y) - (Y * other.X));

    public SceneVector Normalized()
        => Length <= 0.000001d
            ? Zero
            : this / Length;

    public bool IsNearZero(double epsilon = 0.000001d)
        => Length <= epsilon;

    public Vector3 ToNumerics()
        => new((float)X, (float)Y, (float)Z);

    public static SceneVector FromNumerics(Vector3 value)
        => new(value.X, value.Y, value.Z);
}

public sealed record MouseTelemetrySnapshot(
    int Sequence,
    int CalibrationGeneration,
    bool CalibrationValid,
    bool PoseValid,
    bool AdcEnabled,
    bool AdcStable,
    bool SensorReady,
    bool ResetObserved,
    MouseOrientationSource OrientationSource,
    int OrientationAccuracy,
    int LinearAccelAccuracy,
    int GyroAccuracy,
    int PressedButtons,
    int AdcBucket,
    int AdcRaw,
    Quaternion RelativeOrientation,
    SceneVector GyroDps,
    SceneVector LinearAccelG,
    DateTimeOffset ReceivedAt)
{
    public double GyroMagnitudeDps => GyroDps.Length;

    public double LinearAccelMagnitudeG => LinearAccelG.Length;
}

public static class Space3DMouseProtocol
{
    private const byte ManufacturerId = 0x7D;
    private const byte MouseStateTelemetryType = 0x31;

    public static bool TryParseTelemetry(IReadOnlyList<int>? rawData, out MouseTelemetrySnapshot telemetry)
        => TryParseTelemetry(rawData, out telemetry, out _);

    public static bool TryParseTelemetry(IReadOnlyList<int>? rawData, out MouseTelemetrySnapshot telemetry, out string rejectReason)
    {
        telemetry = default!;
        rejectReason = string.Empty;
        if (rawData is null || rawData.Count < 40)
        {
            rejectReason = rawData is null
                ? "No frame data."
                : $"Frame too short: {rawData.Count}.";
            return false;
        }

        if (ReadByte(rawData, 0) != 0xF0 || ReadByte(rawData, rawData.Count - 1) != 0xF7 || ReadByte(rawData, 1) != ManufacturerId)
        {
            rejectReason = $"Bad SysEx envelope: first=0x{ReadByte(rawData, 0):X2}, manufacturer=0x{ReadByte(rawData, 1):X2}, last=0x{ReadByte(rawData, rawData.Count - 1):X2}.";
            return false;
        }

        if (ReadByte(rawData, 2) != (byte)'I' || ReadByte(rawData, 3) != (byte)'D' ||
            ReadByte(rawData, 4) != (byte)'R' || ReadByte(rawData, 5) != (byte)'M')
        {
            rejectReason = $"Bad magic: {ReadByte(rawData, 2):X2} {ReadByte(rawData, 3):X2} {ReadByte(rawData, 4):X2} {ReadByte(rawData, 5):X2}.";
            return false;
        }

        var messageType = ReadByte(rawData, 9) & 0x7F;
        if (messageType != MouseStateTelemetryType)
        {
            rejectReason = $"Unexpected message type: 0x{messageType:X2}.";
            return false;
        }

        var protocolMinor = ReadByte(rawData, 7) & 0x7F;
        var hasSourceByte = protocolMinor == 1;
        var hasPackedSourceBits = protocolMinor >= 2;
        if (hasSourceByte && rawData.Count < 41)
        {
            rejectReason = $"Protocol minor 1 frame too short: {rawData.Count}.";
            return false;
        }

        var expectedCrc = (byte)(ReadByte(rawData, rawData.Count - 2) & 0x7F);
        var actualCrc = ComputeCrc7(rawData, 1, rawData.Count - 2);
        if (expectedCrc != actualCrc)
        {
            rejectReason = $"CRC mismatch: expected 0x{expectedCrc:X2}, actual 0x{actualCrc:X2}, length {rawData.Count}.";
            return false;
        }

        var flags = ReadByte(rawData, 12) & 0x7F;
        var status = ReadByte(rawData, 13) & 0x7F;
        var orientationSource = hasSourceByte
            ? DecodeOrientationSource(ReadByte(rawData, 14) & 0x7F)
            : hasPackedSourceBits
                ? DecodeOrientationSource(((flags >> 5) & 0x02) | ((status >> 6) & 0x01))
                : DecodeLegacyOrientationSource(status);
        var payloadOffset = hasSourceByte ? 1 : 0;
        var pressedButtons = ReadByte(rawData, 14 + payloadOffset) & 0x7F;
        var adcBucket = ReadByte(rawData, 15 + payloadOffset) & 0x7F;
        var adcRaw = ReadUnsigned14(rawData, 16 + payloadOffset);

        var qW = ReadSigned14(rawData, 18 + payloadOffset) / 4096f;
        var qX = ReadSigned14(rawData, 20 + payloadOffset) / 4096f;
        var qY = ReadSigned14(rawData, 22 + payloadOffset) / 4096f;
        var qZ = ReadSigned14(rawData, 24 + payloadOffset) / 4096f;

        var gX = ReadSigned14(rawData, 26 + payloadOffset) / 2f;
        var gY = ReadSigned14(rawData, 28 + payloadOffset) / 2f;
        var gZ = ReadSigned14(rawData, 30 + payloadOffset) / 2f;

        var aX = ReadSigned14(rawData, 32 + payloadOffset) / 1024f;
        var aY = ReadSigned14(rawData, 34 + payloadOffset) / 1024f;
        var aZ = ReadSigned14(rawData, 36 + payloadOffset) / 1024f;

        var rawOrientation = new Quaternion(qX, qY, qZ, qW);
        var orientationLength = rawOrientation.Length();
        var orientation = orientationLength > 0.0001f
            ? Quaternion.Normalize(rawOrientation)
            : Quaternion.Identity;

        telemetry = new MouseTelemetrySnapshot(
            Sequence: ReadByte(rawData, 8) & 0x7F,
            CalibrationGeneration: ReadByte(rawData, 10) & 0x7F,
            CalibrationValid: (flags & 0x01) != 0,
            PoseValid: (flags & 0x02) != 0,
            AdcEnabled: (flags & 0x04) != 0,
            AdcStable: (flags & 0x08) != 0,
            SensorReady: (flags & 0x10) != 0,
            ResetObserved: (flags & 0x20) != 0,
            OrientationSource: orientationSource,
            OrientationAccuracy: status & 0x03,
            LinearAccelAccuracy: (status >> 2) & 0x03,
            GyroAccuracy: (status >> 4) & 0x03,
            PressedButtons: pressedButtons,
            AdcBucket: adcBucket,
            AdcRaw: adcRaw,
            RelativeOrientation: orientation,
            GyroDps: new SceneVector(gX, gY, gZ),
            LinearAccelG: new SceneVector(aX, aY, aZ),
            ReceivedAt: DateTimeOffset.UtcNow);
        rejectReason = string.Empty;
        return true;
    }

    private static byte ReadByte(IReadOnlyList<int> bytes, int index)
        => (byte)(bytes[index] & 0xFF);

    private static int ReadUnsigned14(IReadOnlyList<int> bytes, int index)
        => (ReadByte(bytes, index) & 0x7F) | ((ReadByte(bytes, index + 1) & 0x7F) << 7);

    private static int ReadSigned14(IReadOnlyList<int> bytes, int index)
        => ReadUnsigned14(bytes, index) - 8192;

    private static MouseOrientationSource DecodeOrientationSource(int source)
        => (source & 0x03) switch
        {
            1 => MouseOrientationSource.Geomagnetic,
            2 => MouseOrientationSource.RotationVector,
            3 => MouseOrientationSource.ArvrStabilized,
            _ => MouseOrientationSource.Unknown
        };

    private static MouseOrientationSource DecodeLegacyOrientationSource(int status)
        => (status >> 6) switch
        {
            1 => MouseOrientationSource.Geomagnetic,
            _ => MouseOrientationSource.Unknown
        };

    private static byte ComputeCrc7(IReadOnlyList<int> data, int startIndex, int endExclusive)
    {
        byte crc = 0;
        for (var index = startIndex; index < endExclusive; index++)
        {
            crc ^= (byte)(ReadByte(data, index) & 0x7F);
        }

        return (byte)(crc & 0x7F);
    }
}
