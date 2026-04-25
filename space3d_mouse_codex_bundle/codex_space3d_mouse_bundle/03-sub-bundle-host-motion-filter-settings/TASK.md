# Sub-bundle 03 — Host Motion Filter + Navigation Settings

## Goal

Make pan, zoom, and scene rotation smoother, finer, configurable, and frame-rate independent. Move navigation mapping out of Razor into testable driver-layer classes.

## Core problem to fix

Current pan and zoom are applied per telemetry frame. At higher telemetry rate, the scene moves more per second. This must be changed to rate-based control using elapsed time.

## Required implementation tasks

### 1. Add host-side control settings

Create a settings model in the driver project, for example:

`CanDoItAll.Space3D.Mouse.Driver/Control/Space3DMouseControlSettings.cs`

Suggested structure:

```csharp
namespace CanDoItAll.Space3D.Mouse.Driver.Control;

public sealed class Space3DMouseControlSettings
{
    public string ProfileName { get; set; } = "Smooth default";
    public double AccelSmoothingTauMs { get; set; } = 70d;
    public double GyroSmoothingTauMs { get; set; } = 35d;
    public double PointerSmoothingTauMs { get; set; } = 42d;

    public double AccelDeadzoneG { get; set; } = 0.035d;
    public double GyroDeadzoneDps { get; set; } = 1.2d;
    public double AccelSoftRangeG { get; set; } = 0.28d;
    public double GyroSoftRangeDps { get; set; } = 180d;

    public double PanPixelsPerSecondAtFullInput { get; set; } = 650d;
    public double ZoomLogRatePerSecondAtFullInput { get; set; } = 2.6d;
    public double OrbitDegreesPerSecondAtFullInput { get; set; } = 95d;

    public double PanResponseExponent { get; set; } = 1.45d;
    public double ZoomResponseExponent { get; set; } = 1.35d;
    public double OrbitResponseExponent { get; set; } = 1.15d;

    public double PanForwardMix { get; set; } = 0.35d;
    public double StillGyroThresholdDps { get; set; } = 2.5d;
    public double StillAccelThresholdG { get; set; } = 0.045d;
    public double StillHoldMs { get; set; } = 250d;
    public double AccelBiasTauMs { get; set; } = 900d;

    public double PrecisionMultiplier { get; set; } = 0.28d;
}
```

Adjust defaults during testing. Start with conservative values and expose them in UI.

### 2. Add a reusable control filter/mapper

Create a class, for example:

`CanDoItAll.Space3D.Mouse.Driver/Control/Space3DMouseControlFilter.cs`

Responsibilities:

- Track last sample timestamp.
- EWMA-filter scene acceleration and scene gyro.
- Estimate acceleration bias while the hand is still.
- Apply soft deadzone and response curves.
- Produce navigation deltas for pan, zoom, and orbit.
- Reset state on calibration, disconnect, or settings profile changes.

Suggested public API:

```csharp
public sealed class Space3DMouseControlFilter
{
    public Space3DMouseControlSettings Settings { get; }

    public Space3DMouseFilteredState Update(MouseSceneSnapshot snapshot);

    public Space3DNavigationCommand BuildNavigationCommand(
        MouseSceneSnapshot snapshot,
        Space3DMouseButtonState buttons,
        bool precisionMode);

    public void Reset(DateTimeOffset? now = null);
}
```

Suggested state types:

```csharp
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
    public bool HasPan => Math.Abs(PanX) > 0.001d || Math.Abs(PanY) > 0.001d;
    public bool HasOrbit => Math.Abs(OrbitAzimuthRadians) > 0.00001d || Math.Abs(OrbitPolarRadians) > 0.00001d;
    public bool HasZoom => Math.Abs(ZoomFactor - 1d) > 0.0005d;
}
```

### 3. Implement EWMA using elapsed time

Use elapsed time from `MouseTelemetrySnapshot.ReceivedAt`. Clamp `dt` to avoid huge jumps after pauses.

Suggested helper:

```csharp
private static double AlphaFromTau(double tauMs, double dtSeconds)
{
    var tauSeconds = Math.Max(0.001d, tauMs / 1000d);
    var dt = Math.Clamp(dtSeconds, 0.001d, 0.080d);
    return Math.Clamp(dt / (tauSeconds + dt), 0.01d, 1d);
}
```

### 4. Implement soft deadzone and response curve

Avoid a sudden jump when input crosses the deadzone. Use a normalized soft curve.

Suggested helper:

```csharp
private static double ApplySoftDeadzone(double value, double deadzone, double fullScale, double exponent)
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
```

This returns -1..+1. Convert it to rates later.

### 5. Fix pan mapping

Current code:

```csharp
panX = ApplyDeadzone(accel.X, 0.025) * 155;
panY = ApplyDeadzone(accel.Z + accel.Y * 0.35, 0.025) * 155;
```

Replace with rate-based mapping:

```csharp
var panInputX = ApplySoftDeadzone(controlAccel.X, settings.AccelDeadzoneG, settings.AccelSoftRangeG, settings.PanResponseExponent);
var panInputY = ApplySoftDeadzone(controlAccel.Z + controlAccel.Y * settings.PanForwardMix, settings.AccelDeadzoneG, settings.AccelSoftRangeG, settings.PanResponseExponent);
var rate = settings.PanPixelsPerSecondAtFullInput * precisionMultiplier;
var panX = panInputX * rate * deltaSeconds;
var panY = panInputY * rate * deltaSeconds;
```

Clamp per-frame deltas to avoid jumps after stalls.

### 6. Fix zoom mapping

Current code applies a multiplicative factor per frame.

Replace with log-rate-based mapping:

```csharp
var zoomInput = ApplySoftDeadzone(controlAccel.Y, settings.AccelDeadzoneG, settings.AccelSoftRangeG, settings.ZoomResponseExponent);
var zoomRate = zoomInput * settings.ZoomLogRatePerSecondAtFullInput * precisionMultiplier;
var factor = Math.Exp(zoomRate * deltaSeconds);
```

Clamp `zoomRate * deltaSeconds` to a safe range, for example -0.08..+0.08 per frame.

### 7. Smooth orbit/rotation mapping

Use filtered gyro. Do not use raw gyro directly.

Suggested mapping:

```csharp
var yawInput = ApplySoftDeadzone(filteredGyro.Z, settings.GyroDeadzoneDps, settings.GyroSoftRangeDps, settings.OrbitResponseExponent);
var pitchInput = ApplySoftDeadzone(-filteredGyro.X, settings.GyroDeadzoneDps, settings.GyroSoftRangeDps, settings.OrbitResponseExponent);
var orbitRate = settings.OrbitDegreesPerSecondAtFullInput * precisionMultiplier;
var azimuth = DegreesToRadians(yawInput * orbitRate * deltaSeconds);
var polar = DegreesToRadians(pitchInput * orbitRate * deltaSeconds);
```

This makes the full-scale rate configurable and filters tremor.

### 8. Add stillness/bias handling for acceleration

While still, estimate a small acceleration bias and subtract it from future control acceleration.

Stillness condition:

- `filteredGyro.Magnitude < StillGyroThresholdDps`
- `rawAccel.Magnitude < StillAccelThresholdG` or `filteredAccel.Magnitude < StillAccelThresholdG`
- Held for `StillHoldMs`

When still:

```csharp
accelBias = Lerp(accelBias, filteredAccel, biasAlpha);
controlAccel = filteredAccel - accelBias;
```

Avoid bias updates while any navigation button is held if it makes pan/zoom less responsive. Make this configurable if needed.

### 9. Move workbench navigation code out of Razor

Update `Space3DProcessWorkbench.razor` so `ApplyHeldNavigationAsync()` becomes a thin wrapper:

1. Ensure `workbench` and `sceneTelemetry` are valid.
2. Ask `Space3DMouseControlFilter` for a navigation command.
3. Apply `PanAsync`, `OrbitAsync`, and/or `ZoomAsync` if non-zero.
4. Update debug text.

Do not keep old hardcoded constants as the active path.

### 10. Make `MouseLabPoseTransform` settings-aware

Move pointer constants into settings or provide an optional settings object.

At minimum make these configurable:

- yaw full-scale degrees,
- pitch full-scale degrees,
- pointer deadzone degrees,
- pointer smoothing tau,
- zero-lock duration.

Do not break existing constructor behavior. Existing code should keep working if no settings object is passed.

## Required tests

Add or update a test project. If none exists, create one in the solution.

Minimum tests:

1. **Frame-rate invariance for pan**
   - Feed identical synthetic acceleration for one second at 50 Hz and 100 Hz.
   - Total pan delta must differ by less than 10%.

2. **Frame-rate invariance for zoom**
   - Feed identical forward acceleration for one second at 50 Hz and 100 Hz.
   - Final zoom factor must differ by less than 10%.

3. **Idle jitter rejection**
   - Feed deterministic tiny noise below deadzone for two seconds.
   - Pan and zoom outputs must stay below a small epsilon.

4. **Soft deadzone continuity**
   - Values just below deadzone output 0.
   - Values just above deadzone output a small non-zero value.
   - Output is monotonic.

5. **Gyro smoothing**
   - A step in gyro should ramp smoothly, not jump directly to full output when tau is non-zero.

## Acceptance criteria

- Workbench pan and zoom no longer depend on telemetry frequency.
- Rotation uses filtered gyro and configurable response.
- Settings can be changed without firmware changes.
- Razor component has less direct math and more calls to a testable mapper.
- Tests pass.
