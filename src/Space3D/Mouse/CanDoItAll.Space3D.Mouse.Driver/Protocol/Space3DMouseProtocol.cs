using System.Numerics;

namespace CanDoItAll.Space3D.Mouse.Driver.Protocol;

public enum MouseOrientationSource
{
    Unknown = 0,
    RotationVectorMag = 1,
    GameRotationVectorNoMag = 2,
    GeomagneticNoGyro = 3,
    ArvrStabilizedMag = 4,
    ArvrStabilizedGameNoMag = 5,
    RotationVector = RotationVectorMag,
    Geomagnetic = GeomagneticNoGyro,
    ArvrStabilized = ArvrStabilizedMag
}

public enum MouseAdcSignalState
{
    Disabled = 0,
    Idle = 1,
    Pressed = 2,
    Holding = 3
}

public enum MouseAdcEventKind
{
    None = 0,
    Press = 1,
    Release = 2,
    Click = 3,
    DoubleClick = 4,
    Hold = 5
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
    bool OrientationSourceUsesMagnetometer,
    bool OrientationFallbackUsed,
    bool ReportsReconfigured,
    bool SettingsDirty,
    bool RuntimeSettingsValid,
    int OrientationAccuracy,
    int LinearAccelAccuracy,
    int GyroAccuracy,
    int PressedButtons,
    MouseAdcSignalState AdcSignalState,
    int AdcRaw,
    int AdcMillivolts,
    MouseAdcEventKind AdcEventKind,
    int AdcEventSequence,
    int AdcEventRaw,
    int AdcEventMillivolts,
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
    private const int MinSupportedProtocolMinor = 1;
    private const int MaxSupportedProtocolMinor = 4;

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

        var protocolMajor = ReadByte(rawData, 6) & 0x7F;
        var protocolMinor = ReadByte(rawData, 7) & 0x7F;
        if (protocolMajor != 1 || protocolMinor < MinSupportedProtocolMinor || protocolMinor > MaxSupportedProtocolMinor)
        {
            rejectReason = $"Unsupported protocol version: {protocolMajor}.{protocolMinor:00}.";
            return false;
        }

        var hasSourceByte = protocolMinor == 1;
        var hasPackedSourceBits = protocolMinor >= 2;
        var hasExtendedSourceStatus = protocolMinor >= 4;
        var hasExtendedAdcPayload = protocolMinor >= 3;
        if (hasSourceByte && rawData.Count < 41)
        {
            rejectReason = $"Protocol minor 1 frame too short: {rawData.Count}.";
            return false;
        }

        if (hasExtendedAdcPayload && rawData.Count < 48)
        {
            rejectReason = $"Protocol minor {protocolMinor} frame too short: {rawData.Count}.";
            return false;
        }

        if (hasExtendedSourceStatus && rawData.Count < 50)
        {
            rejectReason = $"Protocol minor {protocolMinor} frame too short for extended source status: {rawData.Count}.";
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
        var orientationSource = hasExtendedSourceStatus
            ? DecodeExtendedOrientationSource(ReadByte(rawData, 14) & 0x7F)
            : hasSourceByte
            ? DecodeLegacySourceByte(ReadByte(rawData, 14) & 0x7F)
            : hasPackedSourceBits
                ? DecodePackedOrientationSource(((flags >> 5) & 0x02) | ((status >> 6) & 0x01))
                : DecodeLegacyOrientationSource(status);
        var sourceStatus = hasExtendedSourceStatus ? ReadByte(rawData, 15) & 0x7F : 0;
        var sourceUsesMagnetometer = hasExtendedSourceStatus
            ? (sourceStatus & 0x01) != 0
            : OrientationSourceUsesMagnetometer(orientationSource);
        var orientationFallbackUsed = hasExtendedSourceStatus && (sourceStatus & 0x02) != 0;
        var reportsReconfigured = hasExtendedSourceStatus && (sourceStatus & 0x04) != 0;
        var settingsDirty = hasExtendedSourceStatus && (sourceStatus & 0x08) != 0;
        var runtimeSettingsValid = !hasExtendedSourceStatus || (sourceStatus & 0x10) != 0;

        var payloadOffset = hasExtendedSourceStatus ? 2 : hasSourceByte ? 1 : 0;
        var payloadStart = 14 + payloadOffset;
        var pressedButtons = ReadByte(rawData, payloadStart) & 0x7F;

        MouseAdcSignalState adcSignalState;
        int adcRaw;
        int adcMillivolts;
        MouseAdcEventKind adcEventKind;
        int adcEventSequence;
        int adcEventRaw;
        int adcEventMillivolts;
        int poseStart;

        if (hasExtendedAdcPayload)
        {
            adcSignalState = DecodeAdcSignalState(ReadByte(rawData, payloadStart + 1) & 0x7F);
            adcRaw = ReadUnsigned14(rawData, payloadStart + 2);
            adcMillivolts = ReadUnsigned14(rawData, payloadStart + 4);
            adcEventKind = DecodeAdcEventKind(ReadByte(rawData, payloadStart + 6) & 0x7F);
            adcEventSequence = ReadByte(rawData, payloadStart + 7) & 0x7F;
            adcEventRaw = ReadUnsigned14(rawData, payloadStart + 8);
            adcEventMillivolts = ReadUnsigned14(rawData, payloadStart + 10);
            poseStart = payloadStart + 12;
        }
        else
        {
            var legacyBucket = ReadByte(rawData, payloadStart + 1) & 0x7F;
            adcRaw = ReadUnsigned14(rawData, payloadStart + 2);
            adcMillivolts = ConvertRawToMillivolts(adcRaw);
            adcSignalState = !((flags & 0x04) != 0)
                ? MouseAdcSignalState.Disabled
                : legacyBucket == 0x7F
                    ? MouseAdcSignalState.Idle
                    : MouseAdcSignalState.Pressed;
            adcEventKind = MouseAdcEventKind.None;
            adcEventSequence = 0;
            adcEventRaw = adcRaw;
            adcEventMillivolts = adcMillivolts;
            poseStart = payloadStart + 4;
        }

        var qW = ReadSigned14(rawData, poseStart) / 4096f;
        var qX = ReadSigned14(rawData, poseStart + 2) / 4096f;
        var qY = ReadSigned14(rawData, poseStart + 4) / 4096f;
        var qZ = ReadSigned14(rawData, poseStart + 6) / 4096f;

        var gX = ReadSigned14(rawData, poseStart + 8) / 2f;
        var gY = ReadSigned14(rawData, poseStart + 10) / 2f;
        var gZ = ReadSigned14(rawData, poseStart + 12) / 2f;

        var aX = ReadSigned14(rawData, poseStart + 14) / 1024f;
        var aY = ReadSigned14(rawData, poseStart + 16) / 1024f;
        var aZ = ReadSigned14(rawData, poseStart + 18) / 1024f;

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
            OrientationSourceUsesMagnetometer: sourceUsesMagnetometer,
            OrientationFallbackUsed: orientationFallbackUsed,
            ReportsReconfigured: reportsReconfigured,
            SettingsDirty: settingsDirty,
            RuntimeSettingsValid: runtimeSettingsValid,
            OrientationAccuracy: status & 0x03,
            LinearAccelAccuracy: (status >> 2) & 0x03,
            GyroAccuracy: (status >> 4) & 0x03,
            PressedButtons: pressedButtons,
            AdcSignalState: adcSignalState,
            AdcRaw: adcRaw,
            AdcMillivolts: adcMillivolts,
            AdcEventKind: adcEventKind,
            AdcEventSequence: adcEventSequence,
            AdcEventRaw: adcEventRaw,
            AdcEventMillivolts: adcEventMillivolts,
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

    private static MouseOrientationSource DecodePackedOrientationSource(int source)
        => (source & 0x03) switch
        {
            1 => MouseOrientationSource.GeomagneticNoGyro,
            2 => MouseOrientationSource.RotationVectorMag,
            3 => MouseOrientationSource.ArvrStabilizedMag,
            _ => MouseOrientationSource.Unknown
        };

    private static MouseOrientationSource DecodeLegacySourceByte(int source)
        => DecodePackedOrientationSource(source);

    private static MouseOrientationSource DecodeExtendedOrientationSource(int source)
        => source switch
        {
            1 => MouseOrientationSource.RotationVectorMag,
            2 => MouseOrientationSource.GameRotationVectorNoMag,
            3 => MouseOrientationSource.GeomagneticNoGyro,
            4 => MouseOrientationSource.ArvrStabilizedMag,
            5 => MouseOrientationSource.ArvrStabilizedGameNoMag,
            _ => MouseOrientationSource.Unknown
        };

    private static MouseOrientationSource DecodeLegacyOrientationSource(int status)
        => (status >> 6) switch
        {
            1 => MouseOrientationSource.GeomagneticNoGyro,
            _ => MouseOrientationSource.Unknown
        };

    private static bool OrientationSourceUsesMagnetometer(MouseOrientationSource source)
        => source is MouseOrientationSource.RotationVectorMag
            or MouseOrientationSource.GeomagneticNoGyro
            or MouseOrientationSource.ArvrStabilizedMag;

    private static MouseAdcSignalState DecodeAdcSignalState(int value)
        => value switch
        {
            1 => MouseAdcSignalState.Idle,
            2 => MouseAdcSignalState.Pressed,
            3 => MouseAdcSignalState.Holding,
            _ => MouseAdcSignalState.Disabled
        };

    private static MouseAdcEventKind DecodeAdcEventKind(int value)
        => value switch
        {
            1 => MouseAdcEventKind.Press,
            2 => MouseAdcEventKind.Release,
            3 => MouseAdcEventKind.Click,
            4 => MouseAdcEventKind.DoubleClick,
            5 => MouseAdcEventKind.Hold,
            _ => MouseAdcEventKind.None
        };

    private static int ConvertRawToMillivolts(int raw)
        => Math.Clamp((int)Math.Round((Math.Clamp(raw, 0, 4095) * 3300d) / 4095d, MidpointRounding.AwayFromZero), 0, 4095);

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
