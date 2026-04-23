using System.Numerics;
using CanDoItAll.Space3D.Mouse.Driver.Protocol;

namespace CanDoItAll.Space3D.Mouse.Driver.Scene;

public enum SceneAxisSource
{
    X = 0,
    Y = 1,
    Z = 2
}

public sealed class SceneAxisRule
{
    public SceneAxisRule(SceneAxisSource source, bool inverted)
    {
        Source = source;
        Inverted = inverted;
    }

    public SceneAxisSource Source { get; set; }

    public bool Inverted { get; set; }
}

public sealed record MouseSceneSnapshot(
    MouseTelemetrySnapshot Source,
    bool Valid,
    SceneVector RightAxis,
    SceneVector UpAxis,
    SceneVector ForwardAxis,
    SceneVector PointerPosition,
    SceneVector Gyro,
    SceneVector LinearAccel,
    double ForwardAzimuthDeg,
    double ForwardElevationDeg,
    double RollDeg,
    double GyroMagnitudeDps,
    double LinearAccelMagnitudeG);

public sealed class MouseLabPoseTransform
{
    private static readonly SceneVector WorldRight = new(1d, 0d, 0d);
    private static readonly SceneVector WorldForward = new(0d, 1d, 0d);
    private static readonly SceneVector WorldUp = new(0d, 0d, 1d);
    private static readonly SceneBasis DesiredBasis = new(WorldRight, WorldUp, WorldForward);
    private static readonly SceneVector DeviceRightAxis = new(1d, 0d, 0d);
    private static readonly SceneVector DeviceForwardAxis = new(0d, 1d, 0d);
    private static readonly SceneVector DeviceUpAxis = new(0d, 0d, 1d);

    private SceneBasis? referenceBasis;
    private SceneVector filteredPointer = SceneVector.Zero;
    private double filteredYawDeg;
    private double filteredPitchDeg;
    private double filteredRollDeg;
    private DateTimeOffset? lastAngularSampleAt;
    private DateTimeOffset zeroLockUntil = DateTimeOffset.MinValue;
    private bool haveFilteredAngularState;
    private const double PointerYawFullScaleDeg = 55d;
    private const double PointerPitchFullScaleDeg = 55d;
    private const double PointerDeadzoneDeg = 2.25d;
    private const double PointerSnapMagnitude = 0.035d;
    private const double PointerSmoothingTauMs = 42d;
    private const double CaptureZeroLockMs = 180d;

    public MouseLabPoseTransform()
    {
        ApplyDefaultPreset();
    }

    public SceneAxisRule SceneX { get; } = new(SceneAxisSource.X, true);

    public SceneAxisRule SceneY { get; } = new(SceneAxisSource.Y, false);

    public SceneAxisRule SceneZ { get; } = new(SceneAxisSource.Z, false);

    public double RotationXDeg { get; private set; }

    public double RotationYDeg { get; private set; }

    public double RotationZDeg { get; private set; }

    public bool HasReference => referenceBasis is not null;

    public string MappingSummary
        => $"Map X <- {FormatRule(SceneX)}, Y <- {FormatRule(SceneY)}, Z <- {FormatRule(SceneZ)} | Rot X {RotationXDeg:+0;-0;+0} deg, Y {RotationYDeg:+0;-0;+0} deg, Z {RotationZDeg:+0;-0;+0} deg";

    public void ApplyDefaultPreset()
    {
        SceneX.Source = SceneAxisSource.X;
        SceneX.Inverted = true;
        SceneY.Source = SceneAxisSource.Y;
        SceneY.Inverted = false;
        SceneZ.Source = SceneAxisSource.Z;
        SceneZ.Inverted = false;
        ResetRotation();
        ClearReference();
    }

    public void SetAxisSource(string axisKey, SceneAxisSource source)
    {
        ResolveRule(axisKey).Source = source;
        ClearReference();
    }

    public void SetAxisInverted(string axisKey, bool inverted)
    {
        ResolveRule(axisKey).Inverted = inverted;
        ClearReference();
    }

    public void SetRotation(string axisKey, double degrees)
    {
        switch (axisKey)
        {
            case "X":
                RotationXDeg = NormalizeDegrees(degrees);
                break;
            case "Y":
                RotationYDeg = NormalizeDegrees(degrees);
                break;
            case "Z":
                RotationZDeg = NormalizeDegrees(degrees);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axisKey), axisKey, "Unsupported rotation axis.");
        }

        ClearReference();
    }

    public void NudgeRotation(string axisKey, double deltaDegrees)
    {
        var current = axisKey switch
        {
            "X" => RotationXDeg,
            "Y" => RotationYDeg,
            "Z" => RotationZDeg,
            _ => throw new ArgumentOutOfRangeException(nameof(axisKey), axisKey, "Unsupported rotation axis.")
        };

        SetRotation(axisKey, current + deltaDegrees);
    }

    public double GetRotation(string axisKey)
        => axisKey switch
        {
            "X" => RotationXDeg,
            "Y" => RotationYDeg,
            "Z" => RotationZDeg,
            _ => throw new ArgumentOutOfRangeException(nameof(axisKey), axisKey, "Unsupported rotation axis.")
        };

    public void ResetRotation()
    {
        RotationXDeg = 0d;
        RotationYDeg = 0d;
        RotationZDeg = 0d;
        ClearReference();
    }

    public void ClearReference()
    {
        referenceBasis = null;
        ResetAngularState();
    }

    public bool CaptureReference(MouseTelemetrySnapshot? rawPose)
    {
        var pose = TransformWithoutReference(rawPose);
        if (pose is null || !pose.Source.PoseValid)
        {
            return false;
        }

        if (!TryCreateBasis(pose.ForwardAxis, pose.UpAxis, pose.RightAxis, out var capturedBasis))
        {
            return false;
        }

        referenceBasis = capturedBasis;
        ResetAngularState(pose.Source.ReceivedAt, lockToZero: true);
        return true;
    }

    public MouseSceneSnapshot? Transform(MouseTelemetrySnapshot? rawPose)
    {
        var pose = TransformWithoutReference(rawPose);
        if (pose is null)
        {
            return null;
        }

        if (referenceBasis is null)
        {
            var sceneGyro = ProjectLocalVector(pose.RightAxis, pose.UpAxis, pose.ForwardAxis, pose.LocalGyro);
            var (unlockedYawDeg, unlockedPitchDeg, unlockedRollDeg) = ComputeOrientationAngles(pose.RightAxis, pose.UpAxis, pose.ForwardAxis);
            return BuildSnapshot(
                pose.Source,
                pose.RightAxis,
                pose.UpAxis,
                pose.ForwardAxis,
                sceneGyro,
                pose.SceneLinearAccel,
                pointer: pose.ForwardAxis.Normalized(),
                unlockedYawDeg,
                unlockedPitchDeg,
                unlockedRollDeg);
        }

        var right = ApplyReference(pose.RightAxis).Normalized();
        var up = ApplyReference(pose.UpAxis).Normalized();
        var forward = ApplyReference(pose.ForwardAxis).Normalized();
        var gyro = ProjectLocalVector(right, up, forward, pose.LocalGyro);
        var linearAccel = ProjectLocalVector(right, up, forward, pose.LocalLinearAccel);
        var (rawYawDeg, rawPitchDeg, rawRollDeg) = ComputeOrientationAngles(right, up, forward);
        rawYawDeg = ApplySignedDeadzone(rawYawDeg, PointerDeadzoneDeg);
        rawPitchDeg = ApplySignedDeadzone(rawPitchDeg, PointerDeadzoneDeg);
        rawRollDeg = ApplySignedDeadzone(rawRollDeg, PointerDeadzoneDeg);
        var (yawDeg, pitchDeg, rollDeg, pointer) = UpdateAngularState(rawYawDeg, rawPitchDeg, rawRollDeg, pose.Source.ReceivedAt);

        return BuildSnapshot(
            pose.Source,
            right,
            up,
            forward,
            gyro,
            linearAccel,
            pointer,
            yawDeg,
            pitchDeg,
            rollDeg);
    }

    private MappedScenePose? TransformWithoutReference(MouseTelemetrySnapshot? rawPose)
    {
        if (rawPose is null)
        {
            return null;
        }

        var orientation = rawPose.RelativeOrientation;
        var rightAxis = TransformVector(orientation, DeviceRightAxis);
        var upAxis = TransformVector(orientation, DeviceUpAxis);
        var forwardAxis = TransformVector(orientation, DeviceForwardAxis);

        var right = Rotate(Remap(rightAxis)).Normalized();
        var up = Rotate(Remap(upAxis)).Normalized();
        var forward = Rotate(Remap(forwardAxis)).Normalized();
        var gyro = Rotate(Remap(rawPose.GyroDps));
        var localLinearAccel = Rotate(Remap(rawPose.LinearAccelG));
        var sceneLinearAccel = ProjectLocalVector(right, up, forward, localLinearAccel);

        return new MappedScenePose(rawPose, right, up, forward, gyro, localLinearAccel, sceneLinearAccel);
    }

    private MouseSceneSnapshot BuildSnapshot(
        MouseTelemetrySnapshot source,
        SceneVector right,
        SceneVector up,
        SceneVector forward,
        SceneVector gyro,
        SceneVector linearAccel,
        SceneVector pointer,
        double yawDeg,
        double pitchDeg,
        double rollDeg)
    {
        if (pointer.Length > 1d)
        {
            pointer = pointer.Normalized();
        }

        return new MouseSceneSnapshot(
            Source: source,
            Valid: source.PoseValid,
            RightAxis: right,
            UpAxis: up,
            ForwardAxis: forward,
            PointerPosition: pointer,
            Gyro: gyro,
            LinearAccel: linearAccel,
            ForwardAzimuthDeg: yawDeg,
            ForwardElevationDeg: pitchDeg,
            RollDeg: rollDeg,
            GyroMagnitudeDps: gyro.Length,
            LinearAccelMagnitudeG: linearAccel.Length);
    }

    private static SceneVector TransformVector(Quaternion quaternion, SceneVector vector)
        => SceneVector.FromNumerics(Vector3.Transform(vector.ToNumerics(), quaternion));

    private void ResetAngularState(DateTimeOffset? timestamp = null, bool lockToZero = false)
    {
        filteredPointer = SceneVector.Zero;
        filteredYawDeg = 0d;
        filteredPitchDeg = 0d;
        filteredRollDeg = 0d;
        lastAngularSampleAt = timestamp;
        haveFilteredAngularState = false;
        zeroLockUntil = lockToZero
            ? (timestamp ?? DateTimeOffset.UtcNow).AddMilliseconds(CaptureZeroLockMs)
            : DateTimeOffset.MinValue;
    }

    private (double YawDeg, double PitchDeg, double RollDeg, SceneVector Pointer) UpdateAngularState(
        double yawDeg,
        double pitchDeg,
        double rollDeg,
        DateTimeOffset timestamp)
    {
        if (timestamp < zeroLockUntil)
        {
            filteredPointer = SceneVector.Zero;
            filteredYawDeg = 0d;
            filteredPitchDeg = 0d;
            filteredRollDeg = 0d;
            lastAngularSampleAt = timestamp;
            haveFilteredAngularState = true;
            return (0d, 0d, 0d, SceneVector.Zero);
        }

        if (!haveFilteredAngularState)
        {
            filteredYawDeg = yawDeg;
            filteredPitchDeg = pitchDeg;
            filteredRollDeg = rollDeg;
            haveFilteredAngularState = true;
            lastAngularSampleAt = timestamp;
        }
        else
        {
            var deltaSeconds = ResolveAngularDeltaSeconds(timestamp);
            var alpha = AlphaFromTau(PointerSmoothingTauMs, deltaSeconds);
            filteredYawDeg = LerpAngle(filteredYawDeg, yawDeg, alpha);
            filteredPitchDeg = LerpAngle(filteredPitchDeg, pitchDeg, alpha);
            filteredRollDeg = LerpAngle(filteredRollDeg, rollDeg, alpha);
        }

        filteredPointer = MapOrientationPointer(filteredYawDeg, filteredPitchDeg, filteredRollDeg);
        if (filteredPointer.Length <= PointerSnapMagnitude)
        {
            filteredPointer = SceneVector.Zero;
            filteredYawDeg = 0d;
            filteredPitchDeg = 0d;
            filteredRollDeg = 0d;
        }

        return (filteredYawDeg, filteredPitchDeg, filteredRollDeg, filteredPointer);
    }

    private static bool TryCreateBasis(
        SceneVector forward,
        SceneVector primaryUpHint,
        SceneVector secondaryUpHint,
        out SceneBasis basis)
    {
        var forwardAxis = forward.Normalized();
        if (forwardAxis.IsNearZero())
        {
            basis = default;
            return false;
        }

        foreach (var upHint in new[] { primaryUpHint, secondaryUpHint, WorldUp, WorldRight, WorldForward })
        {
            var rightAxis = forwardAxis.Cross(upHint).Normalized();
            if (rightAxis.IsNearZero())
            {
                continue;
            }

            var upAxis = rightAxis.Cross(forwardAxis).Normalized();
            if (upAxis.IsNearZero())
            {
                continue;
            }

            basis = new SceneBasis(rightAxis, upAxis, forwardAxis);
            return true;
        }

        basis = default;
        return false;
    }

    private SceneVector ApplyReference(SceneVector vector)
    {
        var basis = referenceBasis;
        if (basis is null || vector.IsNearZero())
        {
            return vector;
        }

        var local = new SceneVector(
            vector.Dot(basis.Value.Right),
            vector.Dot(basis.Value.Up),
            vector.Dot(basis.Value.Forward));

        return (DesiredBasis.Right * local.X) +
               (DesiredBasis.Up * local.Y) +
               (DesiredBasis.Forward * local.Z);
    }

    private SceneVector Remap(SceneVector vector)
        => new(
            ResolveValue(SceneX, vector),
            ResolveValue(SceneY, vector),
            ResolveValue(SceneZ, vector));

    private SceneVector Rotate(SceneVector vector)
    {
        if (vector.IsNearZero())
        {
            return vector;
        }

        var rotated = RotateAroundX(vector, DegreesToRadians(RotationXDeg));
        rotated = RotateAroundY(rotated, DegreesToRadians(RotationYDeg));
        rotated = RotateAroundZ(rotated, DegreesToRadians(RotationZDeg));
        return rotated;
    }

    private static (double YawDeg, double PitchDeg, double RollDeg) ComputeOrientationAngles(
        SceneVector right,
        SceneVector up,
        SceneVector forward)
    {
        var planarLength = Math.Sqrt((forward.X * forward.X) + (forward.Y * forward.Y));
        var yawDeg = Math.Atan2(forward.X, forward.Y) * 180d / Math.PI;
        var pitchDeg = Math.Atan2(forward.Z, planarLength) * 180d / Math.PI;

        if (!TryCreateBasis(forward, WorldUp, WorldRight, out var noRollBasis))
        {
            return (yawDeg, pitchDeg, 0d);
        }

        var rollDeg = Math.Atan2(
            right.Dot(noRollBasis.Up),
            right.Dot(noRollBasis.Right)) * 180d / Math.PI;

        return (yawDeg, pitchDeg, rollDeg);
    }

    private static SceneVector MapOrientationPointer(double yawDeg, double pitchDeg, double rollDeg)
    {
        var x = ClampUnit(yawDeg / PointerYawFullScaleDeg);
        var z = ClampUnit(pitchDeg / PointerPitchFullScaleDeg);
        var radialSquared = (x * x) + (z * z);
        if (radialSquared > 1d)
        {
            var scale = 1d / Math.Sqrt(radialSquared);
            x *= scale;
            z *= scale;
            radialSquared = 1d;
        }

        // Keep the cursor on the front hemisphere surface with the sphere center at the origin.
        // Neutral therefore sits at the front pole on +Y instead of in the middle of the dome volume.
        var y = Math.Sqrt(Math.Max(0d, 1d - radialSquared));
        return new SceneVector(x, y, z);
    }

    private static SceneVector ProjectLocalVector(
        SceneVector right,
        SceneVector up,
        SceneVector forward,
        SceneVector localVector)
        => (right * localVector.X) +
           (forward * localVector.Y) +
           (up * localVector.Z);

    private double ResolveAngularDeltaSeconds(DateTimeOffset timestamp)
    {
        var deltaSeconds = lastAngularSampleAt is null
            ? 0.02d
            : Math.Max(0.001d, (timestamp - lastAngularSampleAt.Value).TotalSeconds);
        lastAngularSampleAt = timestamp;
        return Math.Clamp(deltaSeconds, 0.001d, 0.08d);
    }

    private static double ApplySignedDeadzone(double value, double deadzone)
    {
        var magnitude = Math.Abs(value);
        if (magnitude <= deadzone)
        {
            return 0d;
        }

        return Math.Sign(value) * (magnitude - deadzone);
    }

    private static double LerpAngle(double from, double to, double alpha)
        => NormalizeDegrees(from + (ShortestAngleDelta(from, to) * alpha));

    private static double ShortestAngleDelta(double from, double to)
        => NormalizeDegrees(to - from);

    private static double AlphaFromTau(double tauMs, double deltaSeconds)
    {
        var tauSeconds = Math.Max(0.001d, tauMs / 1000d);
        return Math.Clamp(deltaSeconds / (tauSeconds + deltaSeconds), 0.01d, 1d);
    }

    private static double ClampUnit(double value)
        => double.IsFinite(value)
            ? Math.Clamp(value, -1d, 1d)
            : 0d;

    private static SceneVector RotateAroundX(SceneVector vector, double radians)
    {
        if (Math.Abs(radians) <= 0.000001d)
        {
            return vector;
        }

        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new SceneVector(
            vector.X,
            (vector.Y * cos) - (vector.Z * sin),
            (vector.Y * sin) + (vector.Z * cos));
    }

    private static SceneVector RotateAroundY(SceneVector vector, double radians)
    {
        if (Math.Abs(radians) <= 0.000001d)
        {
            return vector;
        }

        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new SceneVector(
            (vector.X * cos) + (vector.Z * sin),
            vector.Y,
            (-vector.X * sin) + (vector.Z * cos));
    }

    private static SceneVector RotateAroundZ(SceneVector vector, double radians)
    {
        if (Math.Abs(radians) <= 0.000001d)
        {
            return vector;
        }

        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return new SceneVector(
            (vector.X * cos) - (vector.Y * sin),
            (vector.X * sin) + (vector.Y * cos),
            vector.Z);
    }

    private static double ResolveValue(SceneAxisRule rule, SceneVector vector)
    {
        var value = rule.Source switch
        {
            SceneAxisSource.X => vector.X,
            SceneAxisSource.Y => vector.Y,
            SceneAxisSource.Z => vector.Z,
            _ => 0d
        };

        return rule.Inverted ? -value : value;
    }

    private SceneAxisRule ResolveRule(string axisKey)
        => axisKey switch
        {
            "X" => SceneX,
            "Y" => SceneY,
            "Z" => SceneZ,
            _ => throw new ArgumentOutOfRangeException(nameof(axisKey), axisKey, "Unsupported scene axis.")
        };

    private static string FormatRule(SceneAxisRule rule)
        => $"{(rule.Inverted ? "-" : "+")}{rule.Source}";

    private static double DegreesToRadians(double degrees)
        => Math.PI * degrees / 180d;

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360d;
        if (normalized > 180d)
        {
            normalized -= 360d;
        }
        else if (normalized <= -180d)
        {
            normalized += 360d;
        }

        return normalized;
    }

    private readonly record struct SceneBasis(SceneVector Right, SceneVector Up, SceneVector Forward);

    private sealed record MappedScenePose(
        MouseTelemetrySnapshot Source,
        SceneVector RightAxis,
        SceneVector UpAxis,
        SceneVector ForwardAxis,
        SceneVector LocalGyro,
        SceneVector LocalLinearAccel,
        SceneVector SceneLinearAccel);
}
