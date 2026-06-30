using CanDoItAll.Space3D.Mouse.Driver.Protocol;
using CanDoItAll.Space3D.Mouse.Driver.Scene;

namespace CanDoItAll.Space3D.Mouse.Driver.Control;

public sealed record Space3DMouseFilteredState(
    SceneVector RawAccel,
    SceneVector FilteredAccel,
    SceneVector AccelBias,
    SceneVector ControlAccel,
    SceneVector RawGyro,
    SceneVector FilteredGyro,
    bool Still,
    double DeltaSeconds);

public sealed record Space3DNavigationCommand(
    double PanX,
    double PanY,
    double OrbitAzimuthRadians,
    double OrbitPolarRadians,
    double ZoomFactor,
    string DebugLabel)
{
    public static Space3DNavigationCommand None { get; } = new(0d, 0d, 0d, 0d, 1d, "idle");

    public bool HasPan => Math.Abs(PanX) > 0.001d || Math.Abs(PanY) > 0.001d;

    public bool HasOrbit => Math.Abs(OrbitAzimuthRadians) > 0.00001d || Math.Abs(OrbitPolarRadians) > 0.00001d;

    public bool HasZoom => Math.Abs(ZoomFactor - 1d) > 0.0005d;
}

public sealed class Space3DMouseControlFilter
{
    private enum NavigationAction
    {
        None,
        Pan,
        Rotate,
        Zoom
    }

    private DateTimeOffset? lastSampleAt;
    private DateTimeOffset? stillCandidateSince;
    private SceneVector filteredAccel = SceneVector.Zero;
    private SceneVector filteredGyro = SceneVector.Zero;
    private SceneVector accelBias = SceneVector.Zero;
    private SceneVector actionOriginAccel = SceneVector.Zero;
    private SceneVector actionOriginGyro = SceneVector.Zero;
    private double actionOriginYawDeg;
    private double actionOriginRollDeg;
    private NavigationAction activeAction;
    private bool haveFilteredState;

    public Space3DMouseControlFilter(Space3DMouseControlSettings? settings = null)
    {
        Settings = (settings ?? Space3DMouseControlSettings.CreateDefault()).Clone().Normalize();
    }

    public Space3DMouseControlSettings Settings { get; private set; }

    public Space3DMouseFilteredState LastState { get; private set; } = new(
        SceneVector.Zero,
        SceneVector.Zero,
        SceneVector.Zero,
        SceneVector.Zero,
        SceneVector.Zero,
        SceneVector.Zero,
        Still: false,
        DeltaSeconds: 0d);

    public Space3DNavigationCommand LastCommand { get; private set; } = Space3DNavigationCommand.None;

    public void ApplySettings(Space3DMouseControlSettings settings)
    {
        Settings = settings.Clone().Normalize();
        Reset();
    }

    public void Reset(DateTimeOffset? now = null)
    {
        lastSampleAt = now;
        stillCandidateSince = null;
        filteredAccel = SceneVector.Zero;
        filteredGyro = SceneVector.Zero;
        accelBias = SceneVector.Zero;
        actionOriginAccel = SceneVector.Zero;
        actionOriginGyro = SceneVector.Zero;
        actionOriginYawDeg = 0d;
        actionOriginRollDeg = 0d;
        activeAction = NavigationAction.None;
        haveFilteredState = false;
        LastState = LastState with
        {
            RawAccel = SceneVector.Zero,
            FilteredAccel = SceneVector.Zero,
            AccelBias = SceneVector.Zero,
            ControlAccel = SceneVector.Zero,
            RawGyro = SceneVector.Zero,
            FilteredGyro = SceneVector.Zero,
            Still = false,
            DeltaSeconds = 0d
        };
        LastCommand = Space3DNavigationCommand.None;
    }

    public Space3DMouseFilteredState Update(MouseSceneSnapshot snapshot)
        => UpdateCore(snapshot, suppressBiasUpdate: false);

    public Space3DNavigationCommand BuildNavigationCommand(
        MouseSceneSnapshot snapshot,
        Space3DMouseButtonSnapshot buttons,
        bool precisionMode)
    {
        if (!snapshot.Valid)
        {
            LastCommand = Space3DNavigationCommand.None;
            return LastCommand;
        }

        var action = ResolveNavigationAction(buttons);
        var hasNavigationButton = action != NavigationAction.None;
        var state = UpdateCore(snapshot, suppressBiasUpdate: hasNavigationButton);
        var precisionMultiplier = precisionMode ? Settings.PrecisionMultiplier : 1d;

        if (action == NavigationAction.None)
        {
            activeAction = NavigationAction.None;
            LastCommand = Space3DNavigationCommand.None with
            {
                DebugLabel = $"idle accel {FormatVector(state.ControlAccel)} gyro {FormatVector(state.FilteredGyro)}"
            };
            return LastCommand;
        }

        if (action != activeAction)
        {
            activeAction = action;
            actionOriginAccel = state.ControlAccel;
            actionOriginGyro = state.FilteredGyro;
            actionOriginYawDeg = snapshot.ForwardAzimuthDeg;
            actionOriginRollDeg = snapshot.RollDeg;
            LastCommand = Space3DNavigationCommand.None with
            {
                DebugLabel = $"{action.ToString().ToLowerInvariant()} origin captured yaw {actionOriginYawDeg:+0.0;-0.0;+0.0} roll {actionOriginRollDeg:+0.0;-0.0;+0.0}"
            };
            return LastCommand;
        }

        var relativeYawDeg = NormalizeDegrees(snapshot.ForwardAzimuthDeg - actionOriginYawDeg);
        var relativeRollDeg = NormalizeDegrees(snapshot.RollDeg - actionOriginRollDeg);
        var relativeState = state with
        {
            ControlAccel = state.ControlAccel - actionOriginAccel,
            FilteredGyro = state.FilteredGyro - actionOriginGyro
        };

        if (action == NavigationAction.Pan)
        {
            LastCommand = BuildPanCommand(relativeState, relativeYawDeg, relativeRollDeg, precisionMultiplier);
            return LastCommand;
        }

        if (action == NavigationAction.Rotate)
        {
            LastCommand = BuildOrbitCommand(relativeState, precisionMultiplier);
            return LastCommand;
        }

        if (action == NavigationAction.Zoom)
        {
            LastCommand = BuildZoomCommand(relativeState, relativeYawDeg, relativeRollDeg, precisionMultiplier);
            return LastCommand;
        }

        LastCommand = Space3DNavigationCommand.None;
        return LastCommand;
    }

    public static double ApplySoftDeadzone(double value, double deadzone, double fullScale, double exponent)
    {
        var magnitude = Math.Abs(value);
        if (magnitude <= deadzone)
        {
            return 0d;
        }

        var usable = Math.Max(0.000001d, fullScale - deadzone);
        var normalized = Math.Clamp((magnitude - deadzone) / usable, 0d, 1d);
        var curved = Math.Pow(normalized, Math.Max(0.25d, exponent));
        return Math.CopySign(curved, value);
    }

    private Space3DMouseFilteredState UpdateCore(MouseSceneSnapshot snapshot, bool suppressBiasUpdate)
    {
        var deltaSeconds = ResolveDeltaSeconds(snapshot.Source.ReceivedAt);
        var rawAccel = snapshot.LinearAccel;
        var rawGyro = snapshot.Gyro;

        if (!haveFilteredState)
        {
            filteredAccel = rawAccel;
            filteredGyro = rawGyro;
            haveFilteredState = true;
        }
        else
        {
            var accelAlpha = AlphaFromTau(Settings.AccelSmoothingTauMs, deltaSeconds);
            var gyroAlpha = AlphaFromTau(Settings.GyroSmoothingTauMs, deltaSeconds);
            filteredAccel = Lerp(filteredAccel, rawAccel, accelAlpha);
            filteredGyro = Lerp(filteredGyro, rawGyro, gyroAlpha);
        }

        var stillCandidate =
            filteredGyro.Length <= Settings.StillGyroThresholdDps &&
            (rawAccel.Length <= Settings.StillAccelThresholdG ||
             filteredAccel.Length <= Settings.StillAccelThresholdG);
        var still = ResolveStillness(snapshot.Source.ReceivedAt, stillCandidate);
        if (still && !suppressBiasUpdate)
        {
            accelBias = Lerp(accelBias, filteredAccel, AlphaFromTau(Settings.AccelBiasTauMs, deltaSeconds));
        }

        var controlAccel = filteredAccel - accelBias;
        LastState = new Space3DMouseFilteredState(
            rawAccel,
            filteredAccel,
            accelBias,
            controlAccel,
            rawGyro,
            filteredGyro,
            still,
            deltaSeconds);
        return LastState;
    }

    private Space3DNavigationCommand BuildPanCommand(
        Space3DMouseFilteredState state,
        double relativeYawDeg,
        double relativeRollDeg,
        double multiplier)
    {
        var panInputX = ApplySoftDeadzone(
            relativeYawDeg,
            Settings.JoystickDeadzoneDeg,
            Settings.JoystickFullScaleDeg,
            Settings.PanResponseExponent);
        var panInputY = ApplySoftDeadzone(
            relativeRollDeg,
            Settings.JoystickDeadzoneDeg,
            Settings.JoystickFullScaleDeg,
            Settings.PanResponseExponent);
        var rate = Settings.PanPixelsPerSecondAtFullInput * multiplier;
        var panX = Math.Clamp(panInputX * rate * state.DeltaSeconds, -80d, 80d);
        var panY = Math.Clamp(panInputY * rate * state.DeltaSeconds, -80d, 80d);
        return new Space3DNavigationCommand(
            panX,
            panY,
            0d,
            0d,
            1d,
            $"pan joystick {panInputX:+0.000;-0.000;+0.000}/{panInputY:+0.000;-0.000;+0.000} yaw {relativeYawDeg:+0.0;-0.0;+0.0} roll {relativeRollDeg:+0.0;-0.0;+0.0}");
    }

    private Space3DNavigationCommand BuildZoomCommand(
        Space3DMouseFilteredState state,
        double relativeYawDeg,
        double relativeRollDeg,
        double multiplier)
    {
        var zoomAxisDeg = BlendDominantAxis(relativeRollDeg, relativeYawDeg);
        var zoomInput = ApplySoftDeadzone(
            zoomAxisDeg,
            Settings.JoystickDeadzoneDeg,
            Settings.JoystickFullScaleDeg,
            Settings.ZoomResponseExponent);
        var zoomRate = zoomInput * Settings.ZoomLogRatePerSecondAtFullInput * multiplier;
        var exponent = Math.Clamp(zoomRate * state.DeltaSeconds, -0.5d, 0.5d);
        return new Space3DNavigationCommand(
            0d,
            0d,
            0d,
            0d,
            Math.Exp(exponent),
            $"zoom joystick {zoomInput:+0.000;-0.000;+0.000} yaw {relativeYawDeg:+0.0;-0.0;+0.0} roll {relativeRollDeg:+0.0;-0.0;+0.0}");
    }

    private Space3DNavigationCommand BuildOrbitCommand(Space3DMouseFilteredState state, double multiplier)
    {
        var yawInput = ApplySoftDeadzone(
            BlendDominantAxis(state.FilteredGyro.Y, state.FilteredGyro.Z),
            Settings.GyroDeadzoneDps,
            Settings.GyroSoftRangeDps,
            Settings.OrbitResponseExponent);
        var pitchInput = ApplySoftDeadzone(
            -state.FilteredGyro.X,
            Settings.GyroDeadzoneDps,
            Settings.GyroSoftRangeDps,
            Settings.OrbitResponseExponent);
        var orbitRate = Settings.OrbitDegreesPerSecondAtFullInput * multiplier;
        var azimuth = DegreesToRadians(Math.Clamp(yawInput * orbitRate * state.DeltaSeconds, -12d, 12d));
        var polar = DegreesToRadians(Math.Clamp(pitchInput * orbitRate * state.DeltaSeconds, -12d, 12d));
        return new Space3DNavigationCommand(
            0d,
            0d,
            azimuth,
            polar,
            1d,
            $"orbit relative {yawInput:+0.000;-0.000;+0.000}/{pitchInput:+0.000;-0.000;+0.000} gyro {FormatVector(state.FilteredGyro)}");
    }

    private double ResolveDeltaSeconds(DateTimeOffset timestamp)
    {
        var deltaSeconds = lastSampleAt is null
            ? 0.02d
            : Math.Max(0.001d, (timestamp - lastSampleAt.Value).TotalSeconds);
        lastSampleAt = timestamp;
        return Math.Clamp(deltaSeconds, 0.001d, 0.08d);
    }

    private bool ResolveStillness(DateTimeOffset timestamp, bool stillCandidate)
    {
        if (!stillCandidate)
        {
            stillCandidateSince = null;
            return false;
        }

        stillCandidateSince ??= timestamp;
        return (timestamp - stillCandidateSince.Value).TotalMilliseconds >= Settings.StillHoldMs;
    }

    private static bool IsPanPressed(Space3DMouseButtonSnapshot buttons)
        => buttons.IsPressed(1) || buttons.ActiveAction == Space3DMouseButtonAction.Pan;

    private static bool IsRotatePressed(Space3DMouseButtonSnapshot buttons)
        => buttons.IsPressed(2) || buttons.ActiveAction == Space3DMouseButtonAction.Rotate;

    private static bool IsZoomPressed(Space3DMouseButtonSnapshot buttons)
        => buttons.IsPressed(3) || buttons.ActiveAction == Space3DMouseButtonAction.Zoom;

    private static NavigationAction ResolveNavigationAction(Space3DMouseButtonSnapshot buttons)
    {
        if (IsPanPressed(buttons))
        {
            return NavigationAction.Pan;
        }

        if (IsRotatePressed(buttons))
        {
            return NavigationAction.Rotate;
        }

        if (IsZoomPressed(buttons))
        {
            return NavigationAction.Zoom;
        }

        return NavigationAction.None;
    }

    private static double AlphaFromTau(double tauMs, double dtSeconds)
    {
        if (tauMs <= 0d)
        {
            return 1d;
        }

        var tauSeconds = Math.Max(0.001d, tauMs / 1000d);
        var dt = Math.Clamp(dtSeconds, 0.001d, 0.080d);
        return Math.Clamp(1d - Math.Exp(-dt / tauSeconds), 0.01d, 1d);
    }

    private static SceneVector Lerp(SceneVector from, SceneVector to, double alpha)
        => from + ((to - from) * alpha);

    private static double BlendDominantAxis(double first, double second)
        => Math.Abs(first) >= Math.Abs(second)
            ? first + (second * 0.25d)
            : second + (first * 0.25d);

    private static string FormatVector(SceneVector value)
        => $"{value.X:+0.000;-0.000;+0.000}, {value.Y:+0.000;-0.000;+0.000}, {value.Z:+0.000;-0.000;+0.000}";

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
}
