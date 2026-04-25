using System.Text.Json;

namespace CanDoItAll.Space3D.Mouse.Driver.Control;

public sealed class Space3DMouseControlSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string ProfileName { get; set; } = "Smooth default";
    public double AccelSmoothingTauMs { get; set; } = 35d;
    public double GyroSmoothingTauMs { get; set; } = 18d;
    public double PointerSmoothingTauMs { get; set; } = 42d;

    public double AccelDeadzoneG { get; set; } = 0.012d;
    public double GyroDeadzoneDps { get; set; } = 0.6d;
    public double AccelSoftRangeG { get; set; } = 0.18d;
    public double GyroSoftRangeDps { get; set; } = 85d;

    public double PanPixelsPerSecondAtFullInput { get; set; } = 2200d;
    public double ZoomLogRatePerSecondAtFullInput { get; set; } = 7.0d;
    public double OrbitDegreesPerSecondAtFullInput { get; set; } = 260d;

    public double PanResponseExponent { get; set; } = 1.0d;
    public double ZoomResponseExponent { get; set; } = 1.0d;
    public double OrbitResponseExponent { get; set; } = 0.9d;

    public double PanForwardMix { get; set; } = 1.2d;
    public double StillGyroThresholdDps { get; set; } = 2.5d;
    public double StillAccelThresholdG { get; set; } = 0.045d;
    public double StillHoldMs { get; set; } = 250d;
    public double AccelBiasTauMs { get; set; } = 900d;

    public double PrecisionMultiplier { get; set; } = 0.28d;

    public double PointerYawFullScaleDeg { get; set; } = 55d;
    public double PointerPitchFullScaleDeg { get; set; } = 55d;
    public double PointerDeadzoneDeg { get; set; } = 2.25d;
    public double PointerSnapMagnitude { get; set; } = 0.035d;
    public double PointerZeroLockMs { get; set; } = 180d;

    public static IReadOnlyList<string> ProfileNames { get; } =
    [
        "Current-like",
        "Smooth default",
        "Precision",
        "Fast orbit"
    ];

    public static Space3DMouseControlSettings CreateDefault()
        => CreateProfile("Smooth default");

    public static Space3DMouseControlSettings CreateProfile(string? profileName)
    {
        var normalized = NormalizeProfileName(profileName);
        var settings = new Space3DMouseControlSettings { ProfileName = normalized };
        settings.ApplyProfile(normalized);
        return settings.Normalize();
    }

    public Space3DMouseControlSettings Clone()
        => new()
        {
            ProfileName = ProfileName,
            AccelSmoothingTauMs = AccelSmoothingTauMs,
            GyroSmoothingTauMs = GyroSmoothingTauMs,
            PointerSmoothingTauMs = PointerSmoothingTauMs,
            AccelDeadzoneG = AccelDeadzoneG,
            GyroDeadzoneDps = GyroDeadzoneDps,
            AccelSoftRangeG = AccelSoftRangeG,
            GyroSoftRangeDps = GyroSoftRangeDps,
            PanPixelsPerSecondAtFullInput = PanPixelsPerSecondAtFullInput,
            ZoomLogRatePerSecondAtFullInput = ZoomLogRatePerSecondAtFullInput,
            OrbitDegreesPerSecondAtFullInput = OrbitDegreesPerSecondAtFullInput,
            PanResponseExponent = PanResponseExponent,
            ZoomResponseExponent = ZoomResponseExponent,
            OrbitResponseExponent = OrbitResponseExponent,
            PanForwardMix = PanForwardMix,
            StillGyroThresholdDps = StillGyroThresholdDps,
            StillAccelThresholdG = StillAccelThresholdG,
            StillHoldMs = StillHoldMs,
            AccelBiasTauMs = AccelBiasTauMs,
            PrecisionMultiplier = PrecisionMultiplier,
            PointerYawFullScaleDeg = PointerYawFullScaleDeg,
            PointerPitchFullScaleDeg = PointerPitchFullScaleDeg,
            PointerDeadzoneDeg = PointerDeadzoneDeg,
            PointerSnapMagnitude = PointerSnapMagnitude,
            PointerZeroLockMs = PointerZeroLockMs
        };

    public Space3DMouseControlSettings Normalize()
    {
        ProfileName = string.IsNullOrWhiteSpace(ProfileName) ? "Smooth default" : ProfileName.Trim();
        AccelSmoothingTauMs = ClampFinite(AccelSmoothingTauMs, 0d, 400d, 35d);
        GyroSmoothingTauMs = ClampFinite(GyroSmoothingTauMs, 0d, 250d, 18d);
        PointerSmoothingTauMs = ClampFinite(PointerSmoothingTauMs, 0d, 300d, 42d);
        AccelDeadzoneG = ClampFinite(AccelDeadzoneG, 0d, 0.25d, 0.012d);
        GyroDeadzoneDps = ClampFinite(GyroDeadzoneDps, 0d, 30d, 0.6d);
        AccelSoftRangeG = ClampFinite(AccelSoftRangeG, Math.Max(AccelDeadzoneG + 0.001d, 0.02d), 2d, 0.18d);
        GyroSoftRangeDps = ClampFinite(GyroSoftRangeDps, Math.Max(GyroDeadzoneDps + 0.1d, 10d), 720d, 85d);
        PanPixelsPerSecondAtFullInput = ClampFinite(PanPixelsPerSecondAtFullInput, 20d, 3000d, 2200d);
        ZoomLogRatePerSecondAtFullInput = ClampFinite(ZoomLogRatePerSecondAtFullInput, 0.05d, 12d, 7.0d);
        OrbitDegreesPerSecondAtFullInput = ClampFinite(OrbitDegreesPerSecondAtFullInput, 5d, 720d, 260d);
        PanResponseExponent = ClampFinite(PanResponseExponent, 0.25d, 4d, 1.0d);
        ZoomResponseExponent = ClampFinite(ZoomResponseExponent, 0.25d, 4d, 1.0d);
        OrbitResponseExponent = ClampFinite(OrbitResponseExponent, 0.25d, 4d, 0.9d);
        PanForwardMix = ClampFinite(PanForwardMix, -2d, 2d, 1.2d);
        StillGyroThresholdDps = ClampFinite(StillGyroThresholdDps, 0.1d, 60d, 2.5d);
        StillAccelThresholdG = ClampFinite(StillAccelThresholdG, 0.001d, 0.5d, 0.045d);
        StillHoldMs = ClampFinite(StillHoldMs, 0d, 2000d, 250d);
        AccelBiasTauMs = ClampFinite(AccelBiasTauMs, 50d, 10000d, 900d);
        PrecisionMultiplier = ClampFinite(PrecisionMultiplier, 0.02d, 1d, 0.28d);
        PointerYawFullScaleDeg = ClampFinite(PointerYawFullScaleDeg, 5d, 180d, 55d);
        PointerPitchFullScaleDeg = ClampFinite(PointerPitchFullScaleDeg, 5d, 180d, 55d);
        PointerDeadzoneDeg = ClampFinite(PointerDeadzoneDeg, 0d, 20d, 2.25d);
        PointerSnapMagnitude = ClampFinite(PointerSnapMagnitude, 0d, 0.5d, 0.035d);
        PointerZeroLockMs = ClampFinite(PointerZeroLockMs, 0d, 2000d, 180d);
        return this;
    }

    public string ToJson()
        => JsonSerializer.Serialize(this, JsonOptions);

    public static bool TryFromJson(string? json, out Space3DMouseControlSettings settings)
    {
        settings = CreateDefault();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Space3DMouseControlSettings>(json, JsonOptions);
            if (parsed is null)
            {
                return false;
            }

            settings = parsed.Normalize();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void ApplyProfile(string profileName)
    {
        switch (profileName)
        {
            case "Current-like":
                AccelSmoothingTauMs = 20d;
                GyroSmoothingTauMs = 12d;
                AccelDeadzoneG = 0.025d;
                GyroDeadzoneDps = 1.5d;
                AccelSoftRangeG = 0.30d;
                GyroSoftRangeDps = 180d;
                PanPixelsPerSecondAtFullInput = 700d;
                ZoomLogRatePerSecondAtFullInput = 2.8d;
                OrbitDegreesPerSecondAtFullInput = 85d;
                PanResponseExponent = 1d;
                ZoomResponseExponent = 1d;
                OrbitResponseExponent = 1d;
                PanForwardMix = 0.35d;
                PrecisionMultiplier = 0.35d;
                break;
            case "Precision":
                AccelSmoothingTauMs = 55d;
                GyroSmoothingTauMs = 28d;
                AccelDeadzoneG = 0.018d;
                GyroDeadzoneDps = 0.8d;
                AccelSoftRangeG = 0.22d;
                GyroSoftRangeDps = 110d;
                PanPixelsPerSecondAtFullInput = 1050d;
                ZoomLogRatePerSecondAtFullInput = 3.6d;
                OrbitDegreesPerSecondAtFullInput = 125d;
                PanResponseExponent = 1.15d;
                ZoomResponseExponent = 1.10d;
                OrbitResponseExponent = 1.0d;
                PanForwardMix = 0.9d;
                PrecisionMultiplier = 0.22d;
                break;
            case "Fast orbit":
                AccelSmoothingTauMs = 30d;
                GyroSmoothingTauMs = 12d;
                AccelDeadzoneG = 0.012d;
                GyroDeadzoneDps = 0.5d;
                AccelSoftRangeG = 0.18d;
                GyroSoftRangeDps = 70d;
                PanPixelsPerSecondAtFullInput = 2100d;
                ZoomLogRatePerSecondAtFullInput = 6.4d;
                OrbitDegreesPerSecondAtFullInput = 360d;
                PanResponseExponent = 1.0d;
                ZoomResponseExponent = 1.0d;
                OrbitResponseExponent = 0.85d;
                PanForwardMix = 1.1d;
                PrecisionMultiplier = 0.28d;
                break;
        }
    }

    private static string NormalizeProfileName(string? profileName)
        => ProfileNames.FirstOrDefault(name => string.Equals(name, profileName?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? "Smooth default";

    private static double ClampFinite(double value, double min, double max, double fallback)
        => double.IsFinite(value)
            ? Math.Clamp(value, min, max)
            : fallback;
}
