using MusicTheory.Core.Models;

namespace MusicTheory.Core.Theory;

public sealed record ChordScaleCandidate(
    int RootPitchClass,
    ModeType Mode,
    string Name,
    string IntervalFormula,
    IReadOnlyList<int> PitchClasses,
    double Score);

public static class TonalScaleLibrary
{
    private static readonly IReadOnlyDictionary<ModeType, TonalScaleDefinition> Definitions =
        new Dictionary<ModeType, TonalScaleDefinition>
        {
            [ModeType.Major] = new(
                Mode: ModeType.Major,
                IntervalsFromTonic: [0, 2, 4, 5, 7, 9, 11],
                Name: "Major",
                IntervalFormula: "1-2-3-4-5-6-7"),
            [ModeType.NaturalMinor] = new(
                Mode: ModeType.NaturalMinor,
                IntervalsFromTonic: [0, 2, 3, 5, 7, 8, 10],
                Name: "Natural minor",
                IntervalFormula: "1-2-b3-4-5-b6-b7"),
            [ModeType.Dorian] = new(
                Mode: ModeType.Dorian,
                IntervalsFromTonic: [0, 2, 3, 5, 7, 9, 10],
                Name: "Dorian",
                IntervalFormula: "1-2-b3-4-5-6-b7"),
            [ModeType.Phrygian] = new(
                Mode: ModeType.Phrygian,
                IntervalsFromTonic: [0, 1, 3, 5, 7, 8, 10],
                Name: "Phrygian",
                IntervalFormula: "1-b2-b3-4-5-b6-b7"),
            [ModeType.Lydian] = new(
                Mode: ModeType.Lydian,
                IntervalsFromTonic: [0, 2, 4, 6, 7, 9, 11],
                Name: "Lydian",
                IntervalFormula: "1-2-3-#4-5-6-7"),
            [ModeType.Mixolydian] = new(
                Mode: ModeType.Mixolydian,
                IntervalsFromTonic: [0, 2, 4, 5, 7, 9, 10],
                Name: "Mixolydian",
                IntervalFormula: "1-2-3-4-5-6-b7"),
            [ModeType.Locrian] = new(
                Mode: ModeType.Locrian,
                IntervalsFromTonic: [0, 1, 3, 5, 6, 8, 10],
                Name: "Locrian",
                IntervalFormula: "1-b2-b3-4-b5-b6-b7"),
            [ModeType.HarmonicMinor] = new(
                Mode: ModeType.HarmonicMinor,
                IntervalsFromTonic: [0, 2, 3, 5, 7, 8, 11],
                Name: "Harmonic minor",
                IntervalFormula: "1-2-b3-4-5-b6-7"),
            [ModeType.MelodicMinor] = new(
                Mode: ModeType.MelodicMinor,
                IntervalsFromTonic: [0, 2, 3, 5, 7, 9, 11],
                Name: "Melodic minor",
                IntervalFormula: "1-2-b3-4-5-6-7"),
            [ModeType.MajorPentatonic] = new(
                Mode: ModeType.MajorPentatonic,
                IntervalsFromTonic: [0, 2, 4, 7, 9],
                Name: "Major pentatonic",
                IntervalFormula: "1-2-3-5-6"),
            [ModeType.MinorPentatonic] = new(
                Mode: ModeType.MinorPentatonic,
                IntervalsFromTonic: [0, 3, 5, 7, 10],
                Name: "Minor pentatonic",
                IntervalFormula: "1-b3-4-5-b7"),
            [ModeType.BluesMinor] = new(
                Mode: ModeType.BluesMinor,
                IntervalsFromTonic: [0, 3, 5, 6, 7, 10],
                Name: "Minor blues",
                IntervalFormula: "1-b3-4-b5-5-b7"),
            [ModeType.BluesMajor] = new(
                Mode: ModeType.BluesMajor,
                IntervalsFromTonic: [0, 2, 3, 4, 7, 9],
                Name: "Major blues",
                IntervalFormula: "1-2-b3-3-5-6")
        };

    private static readonly IReadOnlyDictionary<ModeType, double> ModePriority = new Dictionary<ModeType, double>
    {
        [ModeType.Major] = 1.00,
        [ModeType.NaturalMinor] = 0.98,
        [ModeType.Dorian] = 0.94,
        [ModeType.Mixolydian] = 0.94,
        [ModeType.Lydian] = 0.90,
        [ModeType.MelodicMinor] = 0.88,
        [ModeType.HarmonicMinor] = 0.87,
        [ModeType.Phrygian] = 0.84,
        [ModeType.Locrian] = 0.78,
        [ModeType.MajorPentatonic] = 0.95,
        [ModeType.MinorPentatonic] = 0.95,
        [ModeType.BluesMinor] = 0.93,
        [ModeType.BluesMajor] = 0.91
    };

    public static TonalScaleDefinition GetRequired(ModeType mode)
    {
        if (!Definitions.TryGetValue(mode, out var definition))
        {
            throw new InvalidOperationException($"Unsupported mode '{mode}'.");
        }

        return definition;
    }

    public static IReadOnlyList<TonalScaleDefinition> GetAll()
    {
        return Definitions.Values
            .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<ChordScaleCandidate> GetCandidateScalesForChord(
        ChordInstance chord,
        int? contextKeyPitchClass = null,
        int maxResults = 6)
    {
        var normalizedChordTones = chord.PitchClasses
            .Select(PitchMath.NormalizePitchClass)
            .Distinct()
            .ToArray();
        if (normalizedChordTones.Length == 0)
        {
            return Array.Empty<ChordScaleCandidate>();
        }

        var suggestedRoots = BuildRootCandidates(chord.RootPitchClass, contextKeyPitchClass);
        var candidates = new List<ChordScaleCandidate>();

        foreach (var rootPitchClass in suggestedRoots)
        {
            foreach (var definition in Definitions.Values)
            {
                var scalePitchClasses = definition.IntervalsFromTonic
                    .Select(interval => PitchMath.NormalizePitchClass(rootPitchClass + interval))
                    .Distinct()
                    .ToArray();
                var scaleSet = scalePitchClasses.ToHashSet();
                var matchedCount = normalizedChordTones.Count(scaleSet.Contains);
                if (matchedCount == 0)
                {
                    continue;
                }

                var baseScore = (double)matchedCount / normalizedChordTones.Length;
                if (matchedCount < normalizedChordTones.Length)
                {
                    baseScore -= 0.35;
                }

                var rootBias = rootPitchClass == chord.RootPitchClass
                    ? 0.25
                    : contextKeyPitchClass.HasValue && rootPitchClass == contextKeyPitchClass.Value
                        ? 0.17
                        : 0.05;
                var modeBias = ModePriority.GetValueOrDefault(definition.Mode, 0.8);
                var score = Math.Round(baseScore + rootBias + modeBias * 0.22, 4);
                if (score <= 0)
                {
                    continue;
                }

                candidates.Add(new ChordScaleCandidate(
                    RootPitchClass: rootPitchClass,
                    Mode: definition.Mode,
                    Name: $"{PitchMath.ToNoteName(rootPitchClass, EnharmonicPreference.Sharps).Token} {definition.Name}",
                    IntervalFormula: definition.IntervalFormula,
                    PitchClasses: scalePitchClasses,
                    Score: score));
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxResults))
            .ToArray();
    }

    public static IReadOnlyList<ChordScaleCandidate> GetCandidateScalesForPitchClassScores(
        IReadOnlyDictionary<int, double> pitchClassScores,
        int? impliedRootPitchClass = null,
        int maxResults = 6)
    {
        if (pitchClassScores.Count == 0)
        {
            return Array.Empty<ChordScaleCandidate>();
        }

        var normalizedScores = pitchClassScores
            .Where(pair => pair.Value > 0)
            .GroupBy(pair => PitchMath.NormalizePitchClass(pair.Key))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Value));
        if (normalizedScores.Count == 0)
        {
            return Array.Empty<ChordScaleCandidate>();
        }

        var sumScores = normalizedScores.Values.Sum();
        if (sumScores <= 0)
        {
            return Array.Empty<ChordScaleCandidate>();
        }

        foreach (var pitchClass in normalizedScores.Keys.ToArray())
        {
            normalizedScores[pitchClass] /= sumScores;
        }

        var resolvedImpliedRoot = impliedRootPitchClass.HasValue
            ? PitchMath.NormalizePitchClass(impliedRootPitchClass.Value)
            : (int?)null;
        var rootCandidates = Enumerable.Range(0, 12)
            .Select(PitchMath.NormalizePitchClass)
            .ToHashSet();
        if (resolvedImpliedRoot.HasValue)
        {
            rootCandidates.Add(resolvedImpliedRoot.Value);
        }

        var candidates = new List<ChordScaleCandidate>();
        foreach (var rootPitchClass in rootCandidates.OrderBy(root => root))
        {
            foreach (var definition in Definitions.Values)
            {
                var scalePitchClasses = definition.IntervalsFromTonic
                    .Select(interval => PitchMath.NormalizePitchClass(rootPitchClass + interval))
                    .Distinct()
                    .ToArray();
                var scaleSet = scalePitchClasses.ToHashSet();

                var coverage = normalizedScores
                    .Where(pair => scaleSet.Contains(pair.Key))
                    .Sum(pair => pair.Value);
                var penalty = normalizedScores
                    .Where(pair => !scaleSet.Contains(pair.Key) && pair.Value > 0.06)
                    .Sum(pair => pair.Value) * 0.7;
                var rootBias = resolvedImpliedRoot.HasValue && rootPitchClass == resolvedImpliedRoot.Value
                    ? 0.10
                    : 0;
                var modeBias = ModePriority.GetValueOrDefault(definition.Mode, 0.8) * 0.12;
                var finalScore = coverage - penalty + rootBias + modeBias;
                if (finalScore <= 0)
                {
                    continue;
                }

                candidates.Add(new ChordScaleCandidate(
                    RootPitchClass: rootPitchClass,
                    Mode: definition.Mode,
                    Name: $"{PitchMath.ToNoteName(rootPitchClass, EnharmonicPreference.Sharps).Token} {definition.Name}",
                    IntervalFormula: definition.IntervalFormula,
                    PitchClasses: scalePitchClasses,
                    Score: Math.Round(finalScore, 4)));
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxResults))
            .ToArray();
    }

    private static IReadOnlyList<int> BuildRootCandidates(int chordRootPitchClass, int? contextKeyPitchClass)
    {
        var roots = new HashSet<int> { PitchMath.NormalizePitchClass(chordRootPitchClass) };
        if (contextKeyPitchClass is int contextRoot)
        {
            roots.Add(PitchMath.NormalizePitchClass(contextRoot));
        }

        roots.Add(PitchMath.NormalizePitchClass(chordRootPitchClass - 3));
        roots.Add(PitchMath.NormalizePitchClass(chordRootPitchClass + 3));
        roots.Add(PitchMath.NormalizePitchClass(chordRootPitchClass + 5));
        return roots.ToArray();
    }
}
