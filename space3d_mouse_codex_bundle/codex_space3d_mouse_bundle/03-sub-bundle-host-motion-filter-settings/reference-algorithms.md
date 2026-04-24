# Reference Algorithms for Host Control Mapping

These are implementation references, not mandatory exact code. Keep any comments in source code in English.

## Time step

Clamp the time step to avoid both zero-dt and pause spikes:

```csharp
private static double ResolveDeltaSeconds(DateTimeOffset current, DateTimeOffset? previous)
{
    if (previous is null)
    {
        return 1d / 60d;
    }

    var dt = (current - previous.Value).TotalSeconds;
    return Math.Clamp(dt, 0.001d, 0.080d);
}
```

## EWMA alpha

```csharp
private static double AlphaFromTau(double tauMs, double dtSeconds)
{
    var tauSeconds = Math.Max(0.001d, tauMs / 1000d);
    return Math.Clamp(dtSeconds / (tauSeconds + dtSeconds), 0.01d, 1d);
}
```

## Vector smoothing

```csharp
private static SceneVector Lerp(SceneVector from, SceneVector to, double alpha)
    => new(
        from.X + ((to.X - from.X) * alpha),
        from.Y + ((to.Y - from.Y) * alpha),
        from.Z + ((to.Z - from.Z) * alpha));
```

## Soft deadzone

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

## Pan command

```csharp
var inputX = ApplySoftDeadzone(controlAccel.X, settings.AccelDeadzoneG, settings.AccelSoftRangeG, settings.PanResponseExponent);
var inputY = ApplySoftDeadzone(controlAccel.Z + (controlAccel.Y * settings.PanForwardMix), settings.AccelDeadzoneG, settings.AccelSoftRangeG, settings.PanResponseExponent);
var rate = settings.PanPixelsPerSecondAtFullInput * precisionMultiplier;
var panX = inputX * rate * dt;
var panY = inputY * rate * dt;
```

## Zoom command

```csharp
var input = ApplySoftDeadzone(controlAccel.Y, settings.AccelDeadzoneG, settings.AccelSoftRangeG, settings.ZoomResponseExponent);
var logRate = input * settings.ZoomLogRatePerSecondAtFullInput * precisionMultiplier;
var frameLogDelta = Math.Clamp(logRate * dt, -0.08d, 0.08d);
var zoomFactor = Math.Exp(frameLogDelta);
```

## Orbit command

```csharp
var yawInput = ApplySoftDeadzone(filteredGyro.Z, settings.GyroDeadzoneDps, settings.GyroSoftRangeDps, settings.OrbitResponseExponent);
var pitchInput = ApplySoftDeadzone(-filteredGyro.X, settings.GyroDeadzoneDps, settings.GyroSoftRangeDps, settings.OrbitResponseExponent);
var rate = settings.OrbitDegreesPerSecondAtFullInput * precisionMultiplier;
var azimuth = DegreesToRadians(yawInput * rate * dt);
var polar = DegreesToRadians(pitchInput * rate * dt);
```

## Acceleration bias while still

```csharp
var still = filteredGyro.MagnitudeDps < settings.StillGyroThresholdDps
    && filteredAccel.MagnitudeG < settings.StillAccelThresholdG;

if (stillForLongEnough && !navigationButtonHeld)
{
    var biasAlpha = AlphaFromTau(settings.AccelBiasTauMs, dt);
    accelBias = Lerp(accelBias, filteredAccel, biasAlpha);
}

var controlAccel = filteredAccel - accelBias;
```

Adjust the magnitude property names to match the actual `SceneVector` implementation.
