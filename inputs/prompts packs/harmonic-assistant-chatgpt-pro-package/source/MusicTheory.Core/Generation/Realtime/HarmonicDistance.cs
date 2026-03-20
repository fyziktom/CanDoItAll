using MusicTheory.Core.Theory;

namespace MusicTheory.Core.Generation.Realtime;

public static class HarmonicDistance
{
    private static readonly IReadOnlyDictionary<int, int> CircleOfFifthsIndex = BuildCircleOfFifthsIndex();

    public static int MinCircleOfFifthsSteps(int pitchClassA, int pitchClassB)
    {
        var normalizedA = PitchMath.NormalizePitchClass(pitchClassA);
        var normalizedB = PitchMath.NormalizePitchClass(pitchClassB);
        var indexA = CircleOfFifthsIndex[normalizedA];
        var indexB = CircleOfFifthsIndex[normalizedB];
        var distance = Math.Abs(indexA - indexB);
        return Math.Min(distance, 12 - distance);
    }

    private static IReadOnlyDictionary<int, int> BuildCircleOfFifthsIndex()
    {
        var order = new[] { 0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5 };
        var map = new Dictionary<int, int>(12);
        for (var index = 0; index < order.Length; index++)
        {
            map[order[index]] = index;
        }

        return map;
    }
}
