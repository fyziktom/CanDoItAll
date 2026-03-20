using MusicTheory.Core.Models;
using MusicTheory.Core.Recognition;
using MusicTheory.Core.Theory;

namespace MusicTheory.Core.Generation.Realtime;

public enum HarmonicSectionType
{
    Verse = 0,
    Chorus = 1,
    Bridge = 2
}

public sealed record AssistantSettings(
    StylePackPreset StylePack = StylePackPreset.PopSimple,
    double Brightness = 0.5,
    double Colorfulness = 0.45,
    HarmonicSectionType Section = HarmonicSectionType.Verse,
    int HistorySteps = 32,
    int HorizonChords = 3,
    int BeamWidth = 8,
    bool LockKey = false,
    int? LockedKeyPitchClass = null,
    ModeType LockedMode = ModeType.Major);

public sealed record DetectedChordCandidateWeight(
    ChordInstance Chord,
    string DisplayName,
    double Probability,
    double Score);

public sealed record DetectedChordEvent(
    DateTimeOffset TimestampUtc,
    IReadOnlyList<DetectedChordCandidateWeight> Candidates);

public sealed record HarmonicHypothesis(
    ChordInstance CurrentChord,
    KeyContext KeyContext,
    double Weight,
    IReadOnlyList<string> Reasons);

public sealed record HarmonicSuggestionStep(
    string ChordName,
    string IntervalStructure,
    double Score,
    IReadOnlyList<string> Reasons,
    string? SuggestedScale);

public sealed record HarmonicSuggestionPath(
    IReadOnlyList<HarmonicSuggestionStep> Steps,
    double Score,
    double Probability,
    string KeyDisplayName);

public sealed record HarmonicAssistantState(
    IReadOnlyList<DetectedChordEvent> History,
    IReadOnlyList<HarmonicHypothesis> Hypotheses,
    AssistantSettings Settings)
{
    public static HarmonicAssistantState Default => new(
        History: Array.Empty<DetectedChordEvent>(),
        Hypotheses: Array.Empty<HarmonicHypothesis>(),
        Settings: new AssistantSettings());
}

public sealed record HarmonicAssistantUpdate(
    HarmonicAssistantState State,
    IReadOnlyList<HarmonicSuggestionPath> Suggestions);

public sealed class RealtimeHarmonicAssistantEngine
{
    private HarmonicAssistantState state = HarmonicAssistantState.Default;

    public HarmonicAssistantState State => state;

    public void Reset(AssistantSettings? settings = null)
    {
        state = new HarmonicAssistantState(
            History: Array.Empty<DetectedChordEvent>(),
            Hypotheses: Array.Empty<HarmonicHypothesis>(),
            Settings: settings ?? state.Settings);
    }

    public HarmonicAssistantUpdate Update(
        RealtimeChordDetectionResult detection,
        AssistantSettings? settings = null,
        DateTimeOffset? timestampUtc = null)
    {
        var resolvedSettings = settings ?? state.Settings;
        var now = timestampUtc ?? DateTimeOffset.UtcNow;
        var performanceContext = BuildPerformanceContext(detection);
        var weightedCandidates = BuildWeightedCandidates(detection);
        if (weightedCandidates.Count == 0)
        {
            state = state with { Settings = resolvedSettings };
            var fallbackSuggestions = BuildSuggestions(state.Hypotheses, resolvedSettings, state.History, performanceContext);
            return new HarmonicAssistantUpdate(state, fallbackSuggestions);
        }

        var eventSnapshot = new DetectedChordEvent(now, weightedCandidates);
        var historySteps = Math.Clamp(resolvedSettings.HistorySteps, 8, 256);
        var updatedHistory = state.History
            .TakeLast(historySteps - 1)
            .Append(eventSnapshot)
            .ToArray();
        var hypotheses = UpdateHypotheses(state.Hypotheses, eventSnapshot, resolvedSettings, performanceContext);
        var suggestions = BuildSuggestions(hypotheses, resolvedSettings, updatedHistory, performanceContext);

        state = new HarmonicAssistantState(
            History: updatedHistory,
            Hypotheses: hypotheses,
            Settings: resolvedSettings);

        return new HarmonicAssistantUpdate(state, suggestions);
    }

    private static IReadOnlyList<DetectedChordCandidateWeight> BuildWeightedCandidates(RealtimeChordDetectionResult detection)
    {
        var raw = detection.Candidates
            .Take(5)
            .ToArray();
        if (raw.Length == 0)
        {
            return Array.Empty<DetectedChordCandidateWeight>();
        }

        var minScore = raw.Min(candidate => candidate.Score);
        var shifted = raw
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = Math.Max(0.001, candidate.Score - minScore + 0.001)
            })
            .ToArray();
        var total = shifted.Sum(item => item.Score);
        return shifted
            .Select(item => new DetectedChordCandidateWeight(
                Chord: item.Candidate.Chord,
                DisplayName: item.Candidate.DisplayName,
                Probability: item.Score / total,
                Score: item.Candidate.Score))
            .ToArray();
    }

    private static PerformanceContext BuildPerformanceContext(RealtimeChordDetectionResult detection)
    {
        var scaleCandidates = detection.InferredScaleContext
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
        var primaryScale = scaleCandidates.FirstOrDefault();
        var isBluesContext = primaryScale is not null &&
                             primaryScale.Mode is ModeType.BluesMinor or ModeType.BluesMajor;

        return new PerformanceContext(
            ScaleCandidates: scaleCandidates,
            PrimaryScaleCandidate: primaryScale,
            IsBluesContext: isBluesContext);
    }

    private static DeviceUsageCounters BuildHistoricalUsageCounters(
        IReadOnlyList<DetectedChordEvent> history,
        PerformanceContext performanceContext)
    {
        if (history.Count == 0)
        {
            return DeviceUsageCounters.Empty;
        }

        var sample = history
            .TakeLast(8)
            .Select(eventItem => eventItem.Candidates.FirstOrDefault())
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToArray();
        if (sample.Length == 0)
        {
            return DeviceUsageCounters.Empty;
        }

        var primaryScaleSet = performanceContext.PrimaryScaleCandidate?.PitchClasses
            .Select(PitchMath.NormalizePitchClass)
            .ToHashSet();
        var chromatic = 0;
        var secondaryDominants = 0;
        var substitutions = 0;
        foreach (var candidate in sample)
        {
            var symbol = candidate.Chord.Definition.Symbol;
            var scaleCoverage = primaryScaleSet is null
                ? 1.0
                : ComputeScaleCoverage(candidate.Chord, primaryScaleSet);
            var isChromatic = scaleCoverage < 0.70;
            if (isChromatic)
            {
                chromatic++;
            }

            var isDominantFamily = symbol.Contains('7', StringComparison.OrdinalIgnoreCase) &&
                                   !symbol.Contains("maj7", StringComparison.OrdinalIgnoreCase);
            if (isDominantFamily && isChromatic)
            {
                secondaryDominants++;
            }

            if (symbol.Contains("dim", StringComparison.OrdinalIgnoreCase) ||
                symbol.Contains("sus", StringComparison.OrdinalIgnoreCase))
            {
                substitutions++;
            }
        }

        return new DeviceUsageCounters(
            ChromaticBars: chromatic,
            SecondaryDominants: secondaryDominants,
            Substitutions: substitutions);
    }

    private static IReadOnlyList<HarmonicHypothesis> UpdateHypotheses(
        IReadOnlyList<HarmonicHypothesis> previous,
        DetectedChordEvent chordEvent,
        AssistantSettings settings,
        PerformanceContext performanceContext)
    {
        var generated = new List<HarmonicHypothesis>();

        foreach (var candidate in chordEvent.Candidates)
        {
            foreach (var key in ResolveCandidateKeys(candidate, settings, performanceContext))
            {
                generated.Add(new HarmonicHypothesis(
                    CurrentChord: candidate.Chord,
                    KeyContext: key,
                    Weight: candidate.Probability * 0.75,
                    Reasons: ["detected chord", $"key hypothesis {key.DisplayName}"]));
            }
        }

        foreach (var hypothesis in previous)
        {
            foreach (var candidate in chordEvent.Candidates)
            {
                var keyCompatibility = hypothesis.KeyContext.Contains(candidate.Chord.RootPitchClass) ? 1.0 : 0.48;
                var voiceLeading = 1.0 / (1.0 + ComputeVoiceLeadingCost(hypothesis.CurrentChord, candidate.Chord));
                var stability = IsSameHarmony(hypothesis.CurrentChord, candidate.Chord) ? 1.12 : 1.0;
                var updatedWeight = (hypothesis.Weight * 0.45 + candidate.Probability * 0.45 + keyCompatibility * 0.08 + voiceLeading * 0.02) * stability;
                var nextKey = ResolveBestKey(hypothesis.KeyContext, candidate.Chord, settings, performanceContext);

                generated.Add(new HarmonicHypothesis(
                    CurrentChord: candidate.Chord,
                    KeyContext: nextKey,
                    Weight: updatedWeight,
                    Reasons: ["history continuity", $"voice-leading {voiceLeading:0.00}"]));
            }
        }

        if (generated.Count == 0)
        {
            return Array.Empty<HarmonicHypothesis>();
        }

        var ordered = generated
            .OrderByDescending(hypothesis => hypothesis.Weight)
            .ThenBy(hypothesis => hypothesis.CurrentChord.Name(EnharmonicPreference.Sharps), StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
        var total = ordered.Sum(item => item.Weight);
        if (total <= 0.000001)
        {
            return ordered;
        }

        return ordered
            .Select(item => item with { Weight = item.Weight / total })
            .ToArray();
    }

    private static KeyContext ResolveBestKey(
        KeyContext previous,
        ChordInstance chord,
        AssistantSettings settings,
        PerformanceContext performanceContext)
    {
        if (settings.LockKey && settings.LockedKeyPitchClass.HasValue)
        {
            return new KeyContext(
                settings.LockedKeyPitchClass.Value,
                settings.LockedMode,
                EnharmonicPreference.Sharps);
        }

        if (previous.Contains(chord.RootPitchClass))
        {
            return previous;
        }

        return ResolveCandidateKeys(
                new DetectedChordCandidateWeight(chord, chord.Name(EnharmonicPreference.Sharps), 1, 1),
                settings,
                performanceContext)
            .First();
    }

    private static IReadOnlyList<KeyContext> ResolveCandidateKeys(
        DetectedChordCandidateWeight candidate,
        AssistantSettings settings,
        PerformanceContext performanceContext)
    {
        if (settings.LockKey && settings.LockedKeyPitchClass.HasValue)
        {
            return
            [
                new KeyContext(settings.LockedKeyPitchClass.Value, settings.LockedMode, EnharmonicPreference.Sharps)
            ];
        }

        var result = new List<KeyContext>(4);

        static bool IsSameKeyContext(KeyContext left, KeyContext right)
        {
            return left.TonicPitchClass == right.TonicPitchClass &&
                   left.Mode == right.Mode;
        }

        static void AddIfMissing(ICollection<KeyContext> keys, KeyContext candidateKey)
        {
            if (keys.Any(existing => IsSameKeyContext(existing, candidateKey)))
            {
                return;
            }

            keys.Add(candidateKey);
        }

        if (performanceContext.PrimaryScaleCandidate is { } primaryScale)
        {
            AddIfMissing(
                result,
                new KeyContext(primaryScale.RootPitchClass, primaryScale.Mode, EnharmonicPreference.Sharps));
        }

        var chordRoot = candidate.Chord.RootPitchClass;
        var chordSymbol = candidate.Chord.Definition.Symbol;
        var mode = chordSymbol.Contains("min", StringComparison.OrdinalIgnoreCase) ||
                   chordSymbol.StartsWith("m", StringComparison.OrdinalIgnoreCase)
            ? ModeType.NaturalMinor
            : ModeType.Major;

        var primary = new KeyContext(chordRoot, mode, EnharmonicPreference.Sharps);
        var relative = mode == ModeType.Major
            ? new KeyContext(PitchMath.NormalizePitchClass(chordRoot + 9), ModeType.NaturalMinor, EnharmonicPreference.Sharps)
            : new KeyContext(PitchMath.NormalizePitchClass(chordRoot + 3), ModeType.Major, EnharmonicPreference.Sharps);
        AddIfMissing(result, primary);
        AddIfMissing(result, relative);

        if (settings.Section == HarmonicSectionType.Bridge || settings.Brightness < 0.35)
        {
            var darker = new KeyContext(chordRoot, ModeType.Dorian, EnharmonicPreference.Sharps);
            AddIfMissing(result, darker);
        }

        return result.Count == 0
            ? [primary, relative]
            : result;
    }

    private static IReadOnlyList<HarmonicSuggestionPath> BuildSuggestions(
        IReadOnlyList<HarmonicHypothesis> hypotheses,
        AssistantSettings settings,
        IReadOnlyList<DetectedChordEvent> history,
        PerformanceContext performanceContext)
    {
        if (hypotheses.Count == 0)
        {
            return Array.Empty<HarmonicSuggestionPath>();
        }

        var suggestionPaths = new List<HarmonicSuggestionPath>();
        foreach (var hypothesis in hypotheses)
        {
            var generated = RunBeamSearch(hypothesis, settings, history, performanceContext);
            suggestionPaths.AddRange(generated);
        }

        if (suggestionPaths.Count == 0)
        {
            return Array.Empty<HarmonicSuggestionPath>();
        }

        var top = suggestionPaths
            .OrderByDescending(path => path.Score)
            .ThenBy(path => path.Steps.Count)
            .Take(12)
            .ToArray();
        var exponentials = top.Select(path => Math.Exp(path.Score)).ToArray();
        var total = exponentials.Sum();

        return top
            .Select((path, index) => path with
            {
                Probability = total <= 0.000001 ? 0 : exponentials[index] / total
            })
            .ToArray();
    }

    private static IReadOnlyList<HarmonicSuggestionPath> RunBeamSearch(
        HarmonicHypothesis hypothesis,
        AssistantSettings settings,
        IReadOnlyList<DetectedChordEvent> history,
        PerformanceContext performanceContext)
    {
        var style = HarmonicStylePackLibrary.GetRequired(settings.StylePack);
        var horizon = Math.Clamp(settings.HorizonChords, 3, 6);
        var width = Math.Clamp(settings.BeamWidth, 4, 12);
        var historicalUsage = BuildHistoricalUsageCounters(history, performanceContext);
        var beam = new List<PathNode>
        {
            new(
                hypothesis.CurrentChord,
                Array.Empty<HarmonicSuggestionStep>(),
                Math.Log(Math.Max(0.0001, hypothesis.Weight + 0.0001)),
                historicalUsage)
        };

        for (var depth = 0; depth < horizon; depth++)
        {
            var next = new List<PathNode>();
            foreach (var node in beam)
            {
                var transitions = GenerateTransitions(
                    node.CurrentChord,
                    hypothesis.KeyContext,
                    settings,
                    performanceContext,
                    style,
                    depth);
                foreach (var transition in transitions)
                {
                    var voiceLeadingCost = ComputeVoiceLeadingCost(node.CurrentChord, transition.Chord);
                    var styleWeight = ResolveStyleWeight(style, transition);
                    var constraintMultiplier = ResolveConstraintMultiplier(style.Constraints, node.UsageCounters, transition);
                    var circleSteps = HarmonicDistance.MinCircleOfFifthsSteps(node.CurrentChord.RootPitchClass, transition.Chord.RootPitchClass);
                    var tonalDistanceTerm = circleSteps == 1
                        ? 0.10
                        : -0.03 * Math.Max(0, circleSteps - 2);
                    var fromMood = HarmonyVisualMapping.ComputeWorldY(node.CurrentChord);
                    var toMood = HarmonyVisualMapping.ComputeWorldY(transition.Chord);
                    var moodContinuityTerm = -0.06 * Math.Abs(toMood - fromMood);
                    var scaleContextTerm = ComputeScaleContextBonus(transition.Chord, performanceContext);

                    var transitionScore = transition.BaseScore * styleWeight * constraintMultiplier;
                    var stepScore = transitionScore
                        - voiceLeadingCost * 0.11
                        + tonalDistanceTerm
                        + moodContinuityTerm
                        + scaleContextTerm;
                    var score = node.Score + stepScore;
                    var inferredScaleRoot = performanceContext.PrimaryScaleCandidate?.RootPitchClass ?? hypothesis.KeyContext.TonicPitchClass;
                    var scale = TonalScaleLibrary.GetCandidateScalesForChord(transition.Chord, inferredScaleRoot, 1)
                        .FirstOrDefault()?.Name;
                    var reasons = new List<string>(transition.Reasons);
                    if (!string.IsNullOrWhiteSpace(transition.DeviceName))
                    {
                        reasons.Add($"device {transition.DeviceName}");
                    }

                    if (Math.Abs(styleWeight - 1.0) > 0.01)
                    {
                        reasons.Add($"style weight x{styleWeight:0.00}");
                    }

                    if (constraintMultiplier < 0.99)
                    {
                        reasons.Add("style constraint pressure");
                    }

                    if (circleSteps == 1)
                    {
                        reasons.Add("fifths coherence");
                    }
                    else if (circleSteps >= 4)
                    {
                        reasons.Add("long tonal jump");
                    }

                    if (Math.Abs(scaleContextTerm) > 0.01)
                    {
                        reasons.Add(scaleContextTerm > 0 ? "scale-context aligned" : "scale-context tension");
                    }

                    var step = new HarmonicSuggestionStep(
                        ChordName: transition.Chord.Name(EnharmonicPreference.Sharps),
                        IntervalStructure: transition.Chord.Definition.IntervalFormula,
                        Score: stepScore,
                        Reasons: reasons,
                        SuggestedScale: scale);

                    next.Add(new PathNode(
                        transition.Chord,
                        node.Steps.Append(step).ToArray(),
                        score,
                        node.UsageCounters.Add(transition)));
                }
            }

            beam = next
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.CurrentChord.Name(EnharmonicPreference.Sharps), StringComparer.OrdinalIgnoreCase)
                .Take(width)
                .ToList();
        }

        return beam
            .Select(node => new HarmonicSuggestionPath(
                Steps: node.Steps,
                Score: node.Score,
                Probability: 0,
                KeyDisplayName: hypothesis.KeyContext.DisplayName))
            .ToArray();
    }

    private static IReadOnlyList<TransitionCandidate> GenerateTransitions(
        ChordInstance chord,
        KeyContext key,
        AssistantSettings settings,
        PerformanceContext performanceContext,
        HarmonicStylePack style,
        int depth)
    {
        var minorFamily = IsMinorFamily(key.Mode);
        var degree = ResolveScaleDegree(key, chord.RootPitchClass) ?? 1;
        var targetDegrees = degree switch
        {
            1 => new[] { 5, 6, 4, 2 },
            2 => new[] { 5, 1, 6 },
            3 => new[] { 6, 4, 2 },
            4 => new[] { 5, 1, 2 },
            5 => new[] { 1, 6, 4 },
            6 => new[] { 4, 2, 5 },
            _ => new[] { 5, 1, 4 }
        };

        var transitions = new List<TransitionCandidate>();
        foreach (var targetDegree in targetDegrees)
        {
            var diatonic = BuildDiatonicChord(key, targetDegree, settings.Colorfulness);
            var cadenceBonus = targetDegree == 1 ? 0.35 : 0;
            var reason = targetDegree == 1
                ? "cadential resolution"
                : degree == 2 && targetDegree == 5
                    ? "ii-V motion"
                    : "diatonic functional move";

            transitions.Add(new TransitionCandidate(
                Chord: diatonic,
                Reasons: [reason],
                BaseScore: 1.1 + cadenceBonus - depth * 0.03,
                DeviceName: null,
                IsChromatic: !key.Contains(diatonic.RootPitchClass),
                IsSecondaryDominant: false,
                IsSubstitution: false));
        }

        if ((style.EnabledDeviceGroups & HarmonicDeviceGroup.SecondaryDominants) != 0 &&
            (settings.Colorfulness > 0.4 || settings.StylePack == StylePackPreset.BebopStandardJazz))
        {
            var target = transitions.First().Chord;
            var secondaryRoot = PitchMath.NormalizePitchClass(target.RootPitchClass + 7);
            var secondary = ChordBuilder.Build(secondaryRoot, "7");
            transitions.Add(new TransitionCandidate(
                Chord: secondary,
                Reasons: ["secondary dominant"],
                BaseScore: 1.15,
                DeviceName: SecondaryDominantDevice.DeviceName,
                IsChromatic: !key.Contains(secondary.RootPitchClass),
                IsSecondaryDominant: true,
                IsSubstitution: false));
        }

        if ((style.EnabledDeviceGroups & HarmonicDeviceGroup.TritoneSubstitutions) != 0 &&
            (settings.StylePack == StylePackPreset.BebopStandardJazz || settings.Colorfulness >= 0.6))
        {
            var target = transitions.First().Chord;
            var tritoneRoot = PitchMath.NormalizePitchClass(target.RootPitchClass + 1);
            var tritone = ChordBuilder.Build(tritoneRoot, "7");
            transitions.Add(new TransitionCandidate(
                Chord: tritone,
                Reasons: ["tritone substitution"],
                BaseScore: 1.02,
                DeviceName: TritoneSubstitutionDevice.DeviceName,
                IsChromatic: true,
                IsSecondaryDominant: false,
                IsSubstitution: true));
        }

        if ((style.EnabledDeviceGroups & HarmonicDeviceGroup.ModalInterchange) != 0 &&
            (settings.Brightness <= 0.45 || settings.Section == HarmonicSectionType.Bridge))
        {
            var borrowedDegree = minorFamily ? 6 : 4;
            var borrowedRoot = ResolveDegreeRoot(key, borrowedDegree);
            var borrowed = ChordBuilder.Build(borrowedRoot, "min");
            transitions.Add(new TransitionCandidate(
                Chord: borrowed,
                Reasons: ["borrowed chord"],
                BaseScore: 0.96,
                DeviceName: ModalInterchangeDevice.DeviceName,
                IsChromatic: !key.Contains(borrowed.RootPitchClass),
                IsSecondaryDominant: false,
                IsSubstitution: true));
        }

        if (settings.Brightness > 0.65)
        {
            var brightRoot = ResolveDegreeRoot(key, 2);
            var bright = ChordBuilder.Build(brightRoot, "maj");
            transitions.Add(new TransitionCandidate(
                Chord: bright,
                Reasons: ["brighter color", "Lydian-like lift"],
                BaseScore: 0.92,
                DeviceName: null,
                IsChromatic: !key.Contains(bright.RootPitchClass),
                IsSecondaryDominant: false,
                IsSubstitution: false));
        }

        // Guarantee at least one resolving option in simple mode within short horizon.
        if (settings.Colorfulness < 0.45 && depth <= 2)
        {
            var tonicRoot = ResolveDegreeRoot(key, 1);
            var anchor = ChordBuilder.Build(tonicRoot, minorFamily ? "min" : "maj");
            transitions.Add(new TransitionCandidate(
                Chord: anchor,
                Reasons: ["simple cadence anchor"],
                BaseScore: 1.08,
                DeviceName: null,
                IsChromatic: !key.Contains(anchor.RootPitchClass),
                IsSecondaryDominant: false,
                IsSubstitution: false));
        }

        if (performanceContext.IsBluesContext && performanceContext.PrimaryScaleCandidate is { } primaryScale)
        {
            var bluesRoot = primaryScale.RootPitchClass;
            var bluesI7 = ChordBuilder.Build(bluesRoot, "7");
            var bluesIV7 = ChordBuilder.Build(PitchMath.NormalizePitchClass(bluesRoot + 5), "7");
            var bluesV7 = ChordBuilder.Build(PitchMath.NormalizePitchClass(bluesRoot + 7), "7");
            var sequence = depth switch
            {
                0 => bluesI7,
                1 => bluesIV7,
                2 => bluesI7,
                3 => bluesV7,
                _ => bluesI7
            };

            transitions.Add(new TransitionCandidate(
                Chord: sequence,
                Reasons: ["blues grammar", "I7/IV7/V7 vocabulary"],
                BaseScore: 1.18 - depth * 0.02,
                DeviceName: "Blues Grammar",
                IsChromatic: !key.Contains(sequence.RootPitchClass),
                IsSecondaryDominant: false,
                IsSubstitution: false));

            transitions.Add(new TransitionCandidate(
                Chord: bluesIV7,
                Reasons: ["blues IV7 motion"],
                BaseScore: 1.08,
                DeviceName: "Blues Grammar",
                IsChromatic: !key.Contains(bluesIV7.RootPitchClass),
                IsSecondaryDominant: false,
                IsSubstitution: false));

            transitions.Add(new TransitionCandidate(
                Chord: bluesV7,
                Reasons: ["blues V7 turnaround"],
                BaseScore: 1.05,
                DeviceName: "Blues Grammar",
                IsChromatic: !key.Contains(bluesV7.RootPitchClass),
                IsSecondaryDominant: false,
                IsSubstitution: false));
        }

        return transitions
            .GroupBy(candidate => (candidate.Chord.RootPitchClass, candidate.Chord.Definition.Symbol), candidate => candidate)
            .Select(group => group.OrderByDescending(item => item.BaseScore).First())
            .OrderByDescending(item => item.BaseScore)
            .Take(10)
            .ToArray();
    }

    private static double ResolveStyleWeight(HarmonicStylePack style, TransitionCandidate transition)
    {
        if (string.IsNullOrWhiteSpace(transition.DeviceName))
        {
            return 1.0;
        }

        return style.DeviceWeights.TryGetValue(transition.DeviceName, out var weight)
            ? Math.Clamp(weight, 0.2, 2.5)
            : 1.0;
    }

    private static double ResolveConstraintMultiplier(
        HarmonicStyleConstraints constraints,
        DeviceUsageCounters counters,
        TransitionCandidate transition)
    {
        var multiplier = 1.0;
        if (transition.IsChromatic && counters.ChromaticBars >= constraints.MaxChromaticBarsPer8)
        {
            multiplier *= 0.25;
        }

        if (transition.IsSecondaryDominant && counters.SecondaryDominants >= constraints.MaxSecondaryDominantsPer8)
        {
            multiplier *= 0.25;
        }

        if (transition.IsSubstitution && counters.Substitutions >= constraints.MaxSubstitutionsPerCadence)
        {
            multiplier *= 0.35;
        }

        return multiplier;
    }

    private static double ComputeScaleContextBonus(ChordInstance chord, PerformanceContext performanceContext)
    {
        if (performanceContext.PrimaryScaleCandidate is null)
        {
            return 0;
        }

        var primaryScaleSet = performanceContext.PrimaryScaleCandidate.PitchClasses
            .Select(PitchMath.NormalizePitchClass)
            .ToHashSet();
        var coverage = ComputeScaleCoverage(chord, primaryScaleSet);
        var scaleBonus = Math.Clamp((coverage - 0.5) * 0.18, -0.18, 0.18);
        if (!performanceContext.IsBluesContext)
        {
            return scaleBonus;
        }

        var bluesBonus = 0d;
        var symbol = chord.Definition.Symbol;
        if (symbol.Contains('7', StringComparison.OrdinalIgnoreCase) &&
            !symbol.Contains("maj7", StringComparison.OrdinalIgnoreCase))
        {
            bluesBonus += 0.08;
        }

        var bluesRoot = performanceContext.PrimaryScaleCandidate.RootPitchClass;
        var relativeToBluesRoot = PitchMath.NormalizePitchClass(chord.RootPitchClass - bluesRoot);
        if (relativeToBluesRoot is 0 or 5 or 7)
        {
            bluesBonus += 0.05;
        }

        return Math.Clamp(scaleBonus + bluesBonus, -0.2, 0.28);
    }

    private static double ComputeScaleCoverage(ChordInstance chord, IReadOnlySet<int> scalePitchClasses)
    {
        var chordPitchClasses = chord.PitchClasses
            .Select(PitchMath.NormalizePitchClass)
            .Distinct()
            .ToArray();
        if (chordPitchClasses.Length == 0)
        {
            return 0;
        }

        var covered = chordPitchClasses.Count(scalePitchClasses.Contains);
        return (double)covered / chordPitchClasses.Length;
    }

    private static ChordInstance BuildDiatonicChord(KeyContext key, int degree, double colorfulness)
    {
        var rootPitchClass = ResolveDegreeRoot(key, degree);
        var minorFamily = IsMinorFamily(key.Mode);

        var symbol = degree switch
        {
            1 => minorFamily ? "min" : "maj",
            2 => minorFamily ? "m7b5" : (colorfulness > 0.55 ? "min7" : "min"),
            3 => minorFamily ? "maj" : (colorfulness > 0.55 ? "min7" : "min"),
            4 => minorFamily ? "min" : (colorfulness > 0.55 ? "maj7" : "maj"),
            5 => minorFamily ? "min" : "7",
            6 => minorFamily ? "maj" : (colorfulness > 0.55 ? "min7" : "min"),
            7 => minorFamily ? "7" : "dim",
            _ => "maj"
        };

        if (!ChordLibrary.TryGet(symbol, out _))
        {
            symbol = "maj";
        }

        return ChordBuilder.Build(rootPitchClass, symbol);
    }

    private static int ResolveDegreeRoot(KeyContext key, int degree)
    {
        var index = Math.Clamp(degree - 1, 0, key.ScalePitchClassesByDegree.Count - 1);
        return key.ScalePitchClassesByDegree[index];
    }

    private static int? ResolveScaleDegree(KeyContext key, int pitchClass)
    {
        return key.GetScaleDegree(pitchClass);
    }

    private static bool IsSameHarmony(ChordInstance left, ChordInstance right)
    {
        return left.RootPitchClass == right.RootPitchClass &&
               left.Definition.Symbol.Equals(right.Definition.Symbol, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMinorFamily(ModeType mode)
    {
        return mode is ModeType.NaturalMinor or
               ModeType.Dorian or
               ModeType.Phrygian or
               ModeType.Locrian or
               ModeType.HarmonicMinor or
               ModeType.MelodicMinor or
               ModeType.MinorPentatonic or
               ModeType.BluesMinor;
    }

    private static double ComputeVoiceLeadingCost(ChordInstance from, ChordInstance to)
    {
        var fromSet = from.PitchClasses.Select(PitchMath.NormalizePitchClass).Distinct().ToArray();
        var toSet = to.PitchClasses.Select(PitchMath.NormalizePitchClass).Distinct().ToArray();
        if (fromSet.Length == 0 || toSet.Length == 0)
        {
            return 4;
        }

        var commonTones = fromSet.Intersect(toSet).Count();
        var bassMotion = CircularDistance(from.RootPitchClass, to.RootPitchClass);
        var nearestMotion = toSet
            .Sum(target => fromSet.Min(source => CircularDistance(source, target)));

        var cost = nearestMotion * 0.45 + bassMotion * 0.35 - commonTones * 0.5;
        return Math.Max(0.05, cost);
    }

    private static int CircularDistance(int source, int target)
    {
        var diff = Math.Abs(PitchMath.NormalizePitchClass(source - target));
        return Math.Min(diff, 12 - diff);
    }

    private sealed record PathNode(
        ChordInstance CurrentChord,
        IReadOnlyList<HarmonicSuggestionStep> Steps,
        double Score,
        DeviceUsageCounters UsageCounters);

    private sealed record TransitionCandidate(
        ChordInstance Chord,
        IReadOnlyList<string> Reasons,
        double BaseScore,
        string? DeviceName,
        bool IsChromatic,
        bool IsSecondaryDominant,
        bool IsSubstitution);

    private sealed record PerformanceContext(
        IReadOnlyList<CandidateScaleSuggestion> ScaleCandidates,
        CandidateScaleSuggestion? PrimaryScaleCandidate,
        bool IsBluesContext);

    private sealed record DeviceUsageCounters(
        int ChromaticBars,
        int SecondaryDominants,
        int Substitutions)
    {
        public static DeviceUsageCounters Empty { get; } = new(0, 0, 0);

        public DeviceUsageCounters Add(TransitionCandidate transition)
        {
            return new DeviceUsageCounters(
                ChromaticBars: Math.Min(8, ChromaticBars + (transition.IsChromatic ? 1 : 0)),
                SecondaryDominants: Math.Min(8, SecondaryDominants + (transition.IsSecondaryDominant ? 1 : 0)),
                Substitutions: Math.Min(8, Substitutions + (transition.IsSubstitution ? 1 : 0)));
        }
    }
}
