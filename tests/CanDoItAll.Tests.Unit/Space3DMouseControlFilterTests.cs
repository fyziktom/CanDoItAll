using System.Numerics;
using CanDoItAll.Space3D.Mouse.Driver.Control;
using CanDoItAll.Space3D.Mouse.Driver.Protocol;
using CanDoItAll.Space3D.Mouse.Driver.Scene;

namespace CanDoItAll.Tests.Unit;

public sealed class Space3DMouseControlFilterTests
{
    [Fact]
    public void Pan_is_frame_rate_invariant()
    {
        var pan50 = IntegratePan(50);
        var pan100 = IntegratePan(100);

        AssertWithinRelativeDifference(pan50, pan100, 0.10d);
    }

    [Fact]
    public void Zoom_is_frame_rate_invariant()
    {
        var zoom50 = IntegrateZoom(50);
        var zoom100 = IntegrateZoom(100);

        AssertWithinRelativeDifference(Math.Log(zoom50), Math.Log(zoom100), 0.10d);
    }

    [Fact]
    public void Orbit_is_frame_rate_invariant()
    {
        var orbit50 = IntegrateOrbit(50);
        var orbit100 = IntegrateOrbit(100);

        AssertWithinRelativeDifference(orbit50, orbit100, 0.10d);
    }

    [Fact]
    public void Idle_jitter_below_deadzone_does_not_create_navigation()
    {
        var filter = new Space3DMouseControlFilter(Space3DMouseControlSettings.CreateDefault());
        var start = DateTimeOffset.Parse("2026-04-24T00:00:00Z");
        var panButton = Button(Space3DMouseButtonAction.Pan, 1);
        var zoomButton = Button(Space3DMouseButtonAction.Zoom, 3);
        var panTotal = 0d;
        var zoom = 1d;

        for (var index = 0; index < 100; index++)
        {
            var sign = index % 2 == 0 ? 1d : -1d;
            var snapshot = Snapshot(
                start.AddMilliseconds(index * 20),
                new SceneVector(0.01d * sign, -0.008d * sign, 0.006d * sign),
                new SceneVector(0.4d * sign, 0d, -0.3d * sign));
            panTotal += Math.Abs(filter.BuildNavigationCommand(snapshot, panButton, precisionMode: false).PanX);
            zoom *= filter.BuildNavigationCommand(snapshot, zoomButton, precisionMode: false).ZoomFactor;
        }

        Assert.True(panTotal < 0.5d, $"Expected near-zero pan jitter, got {panTotal:0.000} px.");
        Assert.InRange(Math.Abs(zoom - 1d), 0d, 0.002d);
    }

    [Fact]
    public void Soft_deadzone_is_continuous_and_monotonic()
    {
        var below = Space3DMouseControlFilter.ApplySoftDeadzone(0.011d, 0.012d, 0.18d, 1d);
        var justAbove = Space3DMouseControlFilter.ApplySoftDeadzone(0.013d, 0.012d, 0.18d, 1d);
        var larger = Space3DMouseControlFilter.ApplySoftDeadzone(0.12d, 0.012d, 0.18d, 1d);

        Assert.Equal(0d, below);
        Assert.InRange(justAbove, 0d, 0.01d);
        Assert.True(larger > justAbove);
    }

    [Fact]
    public void Button_press_captures_relative_origin_for_scene_motion()
    {
        var filter = new Space3DMouseControlFilter(Space3DMouseControlSettings.CreateDefault());
        var start = DateTimeOffset.Parse("2026-04-24T00:00:00Z");
        var panButton = Button(Space3DMouseButtonAction.Pan, 1);

        var origin = filter.BuildNavigationCommand(
            Snapshot(start, SceneVector.Zero, SceneVector.Zero, yawDeg: 6d, rollDeg: -4d),
            panButton,
            precisionMode: false);
        var samePosition = filter.BuildNavigationCommand(
            Snapshot(start.AddMilliseconds(20), SceneVector.Zero, SceneVector.Zero, yawDeg: 6d, rollDeg: -4d),
            panButton,
            precisionMode: false);
        var relativeMove = filter.BuildNavigationCommand(
            Snapshot(start.AddMilliseconds(40), SceneVector.Zero, SceneVector.Zero, yawDeg: 18d, rollDeg: 7d),
            panButton,
            precisionMode: false);

        Assert.False(origin.HasPan);
        Assert.False(samePosition.HasPan);
        Assert.True(relativeMove.PanX > 0.1d);
        Assert.True(relativeMove.PanY > 0.1d);
    }

    [Fact]
    public void Zoom_uses_relative_roll_or_yaw_as_joystick_axis()
    {
        var rollZoom = BuildZoomCommandFromAngles(yawDeg: 0d, rollDeg: 14d);
        var yawZoom = BuildZoomCommandFromAngles(yawDeg: 14d, rollDeg: 0d);

        Assert.True(rollZoom.ZoomFactor > 1.001d);
        Assert.True(yawZoom.ZoomFactor > 1.001d);
    }

    [Fact]
    public void Gyro_smoothing_ramps_instead_of_jumping()
    {
        var settings = Space3DMouseControlSettings.CreateDefault();
        settings.GyroSmoothingTauMs = 100d;
        var filter = new Space3DMouseControlFilter(settings);
        var start = DateTimeOffset.Parse("2026-04-24T00:00:00Z");
        var rotateButton = Button(Space3DMouseButtonAction.Rotate, 2);

        filter.BuildNavigationCommand(Snapshot(start, SceneVector.Zero, SceneVector.Zero), rotateButton, precisionMode: false);
        filter.BuildNavigationCommand(
            Snapshot(start.AddMilliseconds(20), SceneVector.Zero, new SceneVector(0d, 0d, 100d)),
            rotateButton,
            precisionMode: false);

        Assert.InRange(filter.LastState.FilteredGyro.Z, 0.1d, 99d);
    }

    [Fact]
    public void Y_axis_gyro_contributes_to_orbit_yaw()
    {
        var filter = new Space3DMouseControlFilter(Space3DMouseControlSettings.CreateDefault());
        var start = DateTimeOffset.Parse("2026-04-24T00:00:00Z");
        var rotateButton = Button(Space3DMouseButtonAction.Rotate, 2);

        filter.BuildNavigationCommand(Snapshot(start, SceneVector.Zero, SceneVector.Zero), rotateButton, precisionMode: false);
        var command = filter.BuildNavigationCommand(
            Snapshot(start.AddMilliseconds(20), SceneVector.Zero, new SceneVector(0d, 80d, 0d)),
            rotateButton,
            precisionMode: false);

        Assert.True(command.OrbitAzimuthRadians > 0.001d);
    }

    [Fact]
    public void Profile_reset_restores_default_values()
    {
        var currentLike = Space3DMouseControlSettings.CreateProfile("Current-like");
        Assert.Equal("Current-like", currentLike.ProfileName);
        Assert.Equal(700d, currentLike.PanPixelsPerSecondAtFullInput);

        var smooth = Space3DMouseControlSettings.CreateProfile("Smooth default");
        smooth.PanPixelsPerSecondAtFullInput = 42d;
        smooth = Space3DMouseControlSettings.CreateProfile(smooth.ProfileName);

        Assert.Equal(2200d, smooth.PanPixelsPerSecondAtFullInput);
    }

    private static double IntegratePan(int hertz)
    {
        var filter = new Space3DMouseControlFilter(Space3DMouseControlSettings.CreateDefault());
        var button = Button(Space3DMouseButtonAction.Pan, 1);
        var start = DateTimeOffset.Parse("2026-04-24T00:00:00Z");
        var total = 0d;
        filter.BuildNavigationCommand(Snapshot(start, SceneVector.Zero, SceneVector.Zero), button, precisionMode: false);
        for (var index = 1; index <= hertz; index++)
        {
            var command = filter.BuildNavigationCommand(
                Snapshot(start.AddSeconds((double)index / hertz), SceneVector.Zero, SceneVector.Zero, yawDeg: 14d, rollDeg: 10d),
                button,
                precisionMode: false);
            total += command.PanX;
        }

        return total;
    }

    private static double IntegrateZoom(int hertz)
    {
        var filter = new Space3DMouseControlFilter(Space3DMouseControlSettings.CreateDefault());
        var button = Button(Space3DMouseButtonAction.Zoom, 3);
        var start = DateTimeOffset.Parse("2026-04-24T00:00:00Z");
        var factor = 1d;
        filter.BuildNavigationCommand(Snapshot(start, SceneVector.Zero, SceneVector.Zero), button, precisionMode: false);
        for (var index = 1; index <= hertz; index++)
        {
            var command = filter.BuildNavigationCommand(
                Snapshot(start.AddSeconds((double)index / hertz), SceneVector.Zero, SceneVector.Zero, rollDeg: 12d),
                button,
                precisionMode: false);
            factor *= command.ZoomFactor;
        }

        return factor;
    }

    private static double IntegrateOrbit(int hertz)
    {
        var filter = new Space3DMouseControlFilter(Space3DMouseControlSettings.CreateDefault());
        var button = Button(Space3DMouseButtonAction.Rotate, 2);
        var start = DateTimeOffset.Parse("2026-04-24T00:00:00Z");
        var total = 0d;
        filter.BuildNavigationCommand(Snapshot(start, SceneVector.Zero, SceneVector.Zero), button, precisionMode: false);
        for (var index = 1; index <= hertz; index++)
        {
            var command = filter.BuildNavigationCommand(
                Snapshot(start.AddSeconds((double)index / hertz), SceneVector.Zero, new SceneVector(0d, 0d, 95d)),
                button,
                precisionMode: false);
            total += command.OrbitAzimuthRadians;
        }

        return total;
    }

    private static Space3DNavigationCommand BuildZoomCommandFromAngles(double yawDeg, double rollDeg)
    {
        var filter = new Space3DMouseControlFilter(Space3DMouseControlSettings.CreateDefault());
        var button = Button(Space3DMouseButtonAction.Zoom, 3);
        var start = DateTimeOffset.Parse("2026-04-24T00:00:00Z");
        filter.BuildNavigationCommand(Snapshot(start, SceneVector.Zero, SceneVector.Zero), button, precisionMode: false);
        return filter.BuildNavigationCommand(
            Snapshot(start.AddMilliseconds(20), SceneVector.Zero, SceneVector.Zero, yawDeg: yawDeg, rollDeg: rollDeg),
            button,
            precisionMode: false);
    }

    private static MouseSceneSnapshot Snapshot(
        DateTimeOffset receivedAt,
        SceneVector accel,
        SceneVector gyro,
        double yawDeg = 0d,
        double pitchDeg = 0d,
        double rollDeg = 0d)
    {
        var source = new MouseTelemetrySnapshot(
            Sequence: 1,
            CalibrationGeneration: 0,
            CalibrationValid: true,
            PoseValid: true,
            AdcEnabled: true,
            AdcStable: true,
            SensorReady: true,
            ResetObserved: false,
            OrientationSource: MouseOrientationSource.GameRotationVectorNoMag,
            OrientationSourceUsesMagnetometer: false,
            OrientationFallbackUsed: false,
            ReportsReconfigured: false,
            SettingsDirty: false,
            RuntimeSettingsValid: true,
            OrientationAccuracy: 3,
            LinearAccelAccuracy: 3,
            GyroAccuracy: 3,
            PressedButtons: 0x04,
            AdcSignalState: MouseAdcSignalState.Pressed,
            AdcRaw: 0,
            AdcMillivolts: 0,
            AdcEventKind: MouseAdcEventKind.None,
            AdcEventSequence: 0,
            AdcEventRaw: 0,
            AdcEventMillivolts: 0,
            RelativeOrientation: Quaternion.Identity,
            GyroDps: gyro,
            LinearAccelG: accel,
            ReceivedAt: receivedAt);

        return new MouseSceneSnapshot(
            source,
            Valid: true,
            RightAxis: new SceneVector(1d, 0d, 0d),
            UpAxis: new SceneVector(0d, 0d, 1d),
            ForwardAxis: new SceneVector(0d, 1d, 0d),
            PointerPosition: SceneVector.Zero,
            Gyro: gyro,
            LinearAccel: accel,
            ForwardAzimuthDeg: yawDeg,
            ForwardElevationDeg: pitchDeg,
            RollDeg: rollDeg,
            GyroMagnitudeDps: gyro.Length,
            LinearAccelMagnitudeG: accel.Length);
    }

    private static Space3DMouseButtonSnapshot Button(Space3DMouseButtonAction action, int buttonNumber)
        => new(
            AdcEnabled: true,
            AdcStable: true,
            SignalState: MouseAdcSignalState.Pressed,
            CurrentRaw: 0,
            CurrentMillivolts: 0,
            ActiveButtonNumber: buttonNumber,
            ActiveButtonName: $"Button {buttonNumber}",
            ActiveAction: action,
            Events: []);

    private static void AssertWithinRelativeDifference(double first, double second, double maxFraction)
    {
        var averageMagnitude = Math.Max(0.000001d, (Math.Abs(first) + Math.Abs(second)) / 2d);
        var fraction = Math.Abs(first - second) / averageMagnitude;
        Assert.True(fraction < maxFraction, $"Expected values within {maxFraction:P0}; first={first}, second={second}, diff={fraction:P2}.");
    }
}
