using MusicTheory.Core.Models;
using MusicTheory.Core.Theory;

namespace MusicTheory.Core.Generation.Realtime;

public enum HarmonyColorMappingMode
{
    HeuristicRuleBased = 0,
    CircleOfFifthsStructured = 1
}

public sealed record HarmonyVisualMetrics(
    double Darkness,
    double Energy,
    double WorldY,
    string ColorHex,
    int RootPitchClass,
    string Symbol);

public static class HarmonyVisualMapping
{
    private const double LowWorldY = 0.15;
    private const double HighWorldY = 0.85;

    public static HarmonyVisualMetrics Compute(
        ChordInstance chord,
        HarmonyColorMappingMode mode = HarmonyColorMappingMode.HeuristicRuleBased,
        EnharmonicPreference pref = EnharmonicPreference.Sharps)
    {
        ArgumentNullException.ThrowIfNull(chord);

        _ = pref;
        var symbol = chord.Definition.Symbol ?? string.Empty;
        var rootPitchClass = PitchMath.NormalizePitchClass(chord.RootPitchClass);
        var darkness = ComputeDarkness(symbol);
        var energy = ComputeEnergy(symbol);
        var worldY = ComputeWorldYFromDarkness(darkness);
        var colorHex = mode == HarmonyColorMappingMode.CircleOfFifthsStructured
            ? ComputeColorStructured(rootPitchClass, symbol, darkness, energy)
            : ComputeColorHeuristic(rootPitchClass, symbol, darkness, energy);

        return new HarmonyVisualMetrics(
            Darkness: darkness,
            Energy: energy,
            WorldY: worldY,
            ColorHex: colorHex,
            RootPitchClass: rootPitchClass,
            Symbol: symbol);
    }

    public static double ComputeWorldY(ChordInstance chord, HarmonyColorMappingMode mode = HarmonyColorMappingMode.HeuristicRuleBased)
    {
        _ = mode;
        ArgumentNullException.ThrowIfNull(chord);
        return ComputeWorldYFromDarkness(ComputeDarkness(chord.Definition.Symbol ?? string.Empty));
    }

    public static string ComputeColorHex(ChordInstance chord, HarmonyColorMappingMode mode = HarmonyColorMappingMode.HeuristicRuleBased)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var symbol = chord.Definition.Symbol ?? string.Empty;
        var rootPitchClass = PitchMath.NormalizePitchClass(chord.RootPitchClass);
        var darkness = ComputeDarkness(symbol);
        var energy = ComputeEnergy(symbol);
        return mode == HarmonyColorMappingMode.CircleOfFifthsStructured
            ? ComputeColorStructured(rootPitchClass, symbol, darkness, energy)
            : ComputeColorHeuristic(rootPitchClass, symbol, darkness, energy);
    }

    private static double ComputeWorldYFromDarkness(double darkness)
    {
        return Clamp(Lerp(LowWorldY, HighWorldY, darkness), 0, 1);
    }

    private static double ComputeDarkness(string symbol)
    {
        var normalized = (symbol ?? string.Empty).Trim().ToLowerInvariant();
        var darkness = DetermineDarknessBase(normalized);

        if (ContainsAny(normalized, "b9", "#9", "b5", "#5"))
        {
            darkness += 0.08;
        }

        if (normalized.Contains("#11", StringComparison.Ordinal))
        {
            darkness += 0.05;
        }

        return Clamp(darkness, 0, 1);
    }

    private static double DetermineDarknessBase(string symbol)
    {
        if (IsHalfDiminished(symbol))
        {
            return 0.78;
        }

        if (IsDiminished(symbol))
        {
            return 0.85;
        }

        if (IsAlteredDominant(symbol))
        {
            return 0.72;
        }

        if (IsMinor(symbol))
        {
            return 0.65;
        }

        if (IsDominantOrExtended(symbol))
        {
            return 0.35;
        }

        if (IsSuspended(symbol))
        {
            return 0.25;
        }

        if (IsMajorFamily(symbol))
        {
            return 0.20;
        }

        return 0.35;
    }

    private static double ComputeEnergy(string symbol)
    {
        var normalized = (symbol ?? string.Empty).Trim().ToLowerInvariant();
        var energy = DetermineEnergyBase(normalized);

        if (ContainsAny(normalized, "9", "11", "13"))
        {
            energy += 0.05;
        }

        if (ContainsAny(normalized, "b9", "#9", "#11", "b13", "#5"))
        {
            energy += 0.05;
        }

        return Clamp(energy, 0, 1);
    }

    private static double DetermineEnergyBase(string symbol)
    {
        if (IsAlteredDominant(symbol))
        {
            return 0.90;
        }

        if (IsDominantOrExtended(symbol))
        {
            return 0.75;
        }

        if (IsHalfDiminished(symbol) || IsDiminished(symbol))
        {
            return 0.70;
        }

        if (IsMinor(symbol))
        {
            return symbol.Contains("7", StringComparison.Ordinal) ? 0.45 : 0.42;
        }

        if (IsSuspended(symbol))
        {
            return 0.28;
        }

        if (symbol.Contains("maj7", StringComparison.Ordinal))
        {
            return 0.32;
        }

        if (IsMajorFamily(symbol))
        {
            return 0.35;
        }

        return 0.40;
    }

    private static string ComputeColorHeuristic(int rootPitchClass, string symbol, double darkness, double energy)
    {
        var fifthsIndex = GetCircleOfFifthsIndex(rootPitchClass);
        var hueOffset = (fifthsIndex - 6) * 3.0;
        var baseHue = SelectBaseHue(symbol, energy);
        var hue = NormalizeHue(baseHue + hueOffset);
        var saturation = Clamp(40 + energy * 40, 24, 90);
        var lightness = Clamp(68 - darkness * 38, 18, 82);
        return HslToHex(hue, saturation, lightness);
    }

    private static string ComputeColorStructured(int rootPitchClass, string symbol, double darkness, double energy)
    {
        var fifthsIndex = GetCircleOfFifthsIndex(rootPitchClass);
        var angle = fifthsIndex * 30.0;
        var baseHue = energy >= 0.66 ? 18.0 : SelectCalmBaseHue(symbol);
        var modulationOffset = ((angle + 180.0) % 360.0 - 180.0) * 0.18;
        var hue = NormalizeHue(baseHue + modulationOffset);
        var saturation = Clamp(36 + energy * 44, 22, 92);
        var lightness = Clamp(70 - darkness * 40, 16, 84);
        return HslToHex(hue, saturation, lightness);
    }

    private static double SelectBaseHue(string symbol, double energy)
    {
        if (energy >= 0.66)
        {
            return 18.0;
        }

        return SelectCalmBaseHue(symbol);
    }

    private static double SelectCalmBaseHue(string symbol)
    {
        return IsMajorFamily(symbol) || IsSuspended(symbol) ? 150.0 : 210.0;
    }

    private static bool IsMajorFamily(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return true;
        }

        if (symbol.StartsWith("maj", StringComparison.Ordinal))
        {
            return true;
        }

        if (symbol.StartsWith("add", StringComparison.Ordinal))
        {
            return true;
        }

        if (symbol.Equals("6", StringComparison.Ordinal))
        {
            return true;
        }

        if (symbol.Equals("maj", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool IsMinor(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        if (symbol.Contains("min", StringComparison.Ordinal))
        {
            return true;
        }

        return symbol.StartsWith("m", StringComparison.Ordinal) &&
               !symbol.StartsWith("maj", StringComparison.Ordinal);
    }

    private static bool IsHalfDiminished(string symbol)
    {
        return symbol.Contains("m7b5", StringComparison.Ordinal) ||
               symbol.Contains("ø", StringComparison.Ordinal);
    }

    private static bool IsDiminished(string symbol)
    {
        if (IsHalfDiminished(symbol))
        {
            return false;
        }

        return symbol.Contains("dim", StringComparison.Ordinal) ||
               symbol.Contains("o", StringComparison.Ordinal);
    }

    private static bool IsSuspended(string symbol)
    {
        return symbol.Contains("sus2", StringComparison.Ordinal) ||
               symbol.Contains("sus4", StringComparison.Ordinal) ||
               symbol.Contains("sus", StringComparison.Ordinal);
    }

    private static bool IsAlteredDominant(string symbol)
    {
        return symbol.Contains("alt", StringComparison.Ordinal) ||
               symbol.Contains("#9", StringComparison.Ordinal) ||
               symbol.Contains("b9", StringComparison.Ordinal) ||
               symbol.Contains("#5", StringComparison.Ordinal) ||
               symbol.Contains("b5", StringComparison.Ordinal) ||
               symbol.Contains("#11", StringComparison.Ordinal) ||
               symbol.Contains("b13", StringComparison.Ordinal);
    }

    private static bool IsDominantOrExtended(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return false;
        }

        if (IsMinor(symbol) || IsMajorFamily(symbol))
        {
            return false;
        }

        return symbol.Contains("7", StringComparison.Ordinal) ||
               symbol.Contains("9", StringComparison.Ordinal) ||
               symbol.Contains("11", StringComparison.Ordinal) ||
               symbol.Contains("13", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string text, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (text.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetCircleOfFifthsIndex(int rootPitchClass)
    {
        return PitchMath.NormalizePitchClass(rootPitchClass) switch
        {
            0 => 0,   // C
            7 => 1,   // G
            2 => 2,   // D
            9 => 3,   // A
            4 => 4,   // E
            11 => 5,  // B
            6 => 6,   // F#
            1 => 7,   // C#
            8 => 8,   // G#
            3 => 9,   // D#
            10 => 10, // A#
            5 => 11,  // F
            _ => 0
        };
    }

    private static string HslToHex(double h, double s, double l)
    {
        h = NormalizeHue(h) / 360.0;
        s = Clamp(s, 0, 100) / 100.0;
        l = Clamp(l, 0, 100) / 100.0;

        var c = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
        var x = c * (1.0 - Math.Abs((h * 6.0) % 2.0 - 1.0));
        var m = l - c / 2.0;

        (double r1, double g1, double b1) = (h * 6.0) switch
        {
            < 1.0 => (c, x, 0.0),
            < 2.0 => (x, c, 0.0),
            < 3.0 => (0.0, c, x),
            < 4.0 => (0.0, x, c),
            < 5.0 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        var r = ToByte(r1 + m);
        var g = ToByte(g1 + m);
        var b = ToByte(b1 + m);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static byte ToByte(double value)
    {
        var clamped = Clamp(value, 0, 1);
        return (byte)Math.Round(clamped * 255.0, MidpointRounding.AwayFromZero);
    }

    private static double NormalizeHue(double hue)
    {
        var normalized = hue % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static double Lerp(double from, double to, double t)
    {
        return from + (to - from) * t;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
