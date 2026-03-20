using MusicTheory.Core.Models;
using MusicTheory.Core.NotationEditor.Model;
using MusicTheory.Core.Theory;

namespace MusicTheory.Core.Recognition;

public sealed record CandidateScaleSuggestion(
    int RootPitchClass,
    ModeType Mode,
    string Name,
    string IntervalFormula,
    IReadOnlyList<int> PitchClasses,
    double Confidence);

public sealed record RealtimeChordCandidate(
    ChordInstance Chord,
    string DisplayName,
    string InversionLabel,
    int? BassPitchClass,
    string IntervalStructure,
    IReadOnlyList<int> MatchedPitchClasses,
    IReadOnlyList<int> MissingPitchClasses,
    IReadOnlyList<int> ContradictionPitchClasses,
    IReadOnlyDictionary<int, int> DuplicatePitchClassCounts,
    IReadOnlyList<CandidateScaleSuggestion> CompatibleScales,
    double Score,
    string Explanation);

public sealed record RealtimeChordDetectionOptions(
    DifficultyPreset Difficulty = DifficultyPreset.Intermediate,
    EnharmonicPreference EnharmonicPreference = EnharmonicPreference.Sharps,
    int MaxCandidates = 8,
    int MaxScaleSuggestions = 6,
    double SustainDecayMs = 1600,
    int DenseClusterThreshold = 10,
    double MinDetectionConfidence = 0.62,
    int MinMatchedPitchClasses = 3,
    int StartPitchClassCount = 3,
    int MaxPitchClassCount = 8,
    double MinPitchClassScoreToInclude = 0.05);

public sealed record RealtimeChordDetectionResult(
    RealtimeChordWindowSnapshot Snapshot,
    IReadOnlyList<RealtimeChordCandidate> Candidates,
    bool HasDenseClusterWarning,
    string? WarningMessage,
    IReadOnlyList<CandidateScaleSuggestion> InferredScaleContext);

public sealed class RealtimeChordDetectionService(
    IChordRecognitionEngine recognitionEngine,
    IChordVoicingAnalyzer voicingAnalyzer)
{
    private readonly IChordRecognitionEngine recognitionEngine = recognitionEngine;
    private readonly IChordVoicingAnalyzer voicingAnalyzer = voicingAnalyzer;

    public RealtimeChordDetectionResult Detect(
        RealtimeChordWindowSnapshot snapshot,
        RealtimeChordDetectionOptions? options = null)
    {
        return Detect(snapshot, scores: null, options);
    }

    public RealtimeChordDetectionResult Detect(
        RealtimeChordWindowSnapshot snapshot,
        RealtimeNoteScoreSnapshot? scores,
        RealtimeChordDetectionOptions? options = null)
    {
        var resolvedOptions = options ?? new RealtimeChordDetectionOptions();
        var hasDenseClusterWarning = snapshot.ActiveNotes.Count >= resolvedOptions.DenseClusterThreshold;
        var denseClusterWarningMessage = hasDenseClusterWarning
            ? "Too many notes are currently active for reliable chord naming."
            : null;
        var inferredScaleContext = BuildInferredScaleContext(snapshot, scores, resolvedOptions);

        var scoredSelection = BuildRankedPitchClassesForScoredDetection(snapshot, scores, resolvedOptions);
        if (scoredSelection.Count > 0)
        {
            var scoredAttempt = EvaluateWithScoredPitchClassSelection(snapshot, scores!, scoredSelection, resolvedOptions);
            return new RealtimeChordDetectionResult(
                Snapshot: snapshot,
                Candidates: scoredAttempt.Candidates,
                HasDenseClusterWarning: hasDenseClusterWarning,
                WarningMessage: denseClusterWarningMessage,
                InferredScaleContext: inferredScaleContext);
        }

        var weightedPitchClasses = BuildRecognitionPitchClasses(snapshot, resolvedOptions);
        if (weightedPitchClasses.Count == 0)
        {
            return new RealtimeChordDetectionResult(
                Snapshot: snapshot,
                Candidates: Array.Empty<RealtimeChordCandidate>(),
                HasDenseClusterWarning: hasDenseClusterWarning,
                WarningMessage: denseClusterWarningMessage,
                InferredScaleContext: inferredScaleContext);
        }

        var baselineAttempt = EvaluateRecognitionAttempt(
            snapshot,
            scores,
            weightedPitchClasses,
            resolvedOptions);

        return new RealtimeChordDetectionResult(
            Snapshot: snapshot,
            Candidates: baselineAttempt.Candidates,
            HasDenseClusterWarning: hasDenseClusterWarning,
            WarningMessage: denseClusterWarningMessage,
            InferredScaleContext: inferredScaleContext);
    }

    public ScoreDocument CreatePreviewScore(RealtimeChordCandidate candidate, EnharmonicPreference preference)
    {
        var score = new ScoreDocument
        {
            Metadata = new ScoreMetadata(
                Title: $"Preview {candidate.DisplayName}",
                Composer: "Realtime chord detector",
                Copyright: string.Empty,
                TempoText: "Moderato"),
            TimeSignature = TimeSignature.CommonTime,
            StaffMode = ScoreStaffMode.Grand,
            AutoRestFillEnabled = false
        };

        var measure = score.EnsureMeasure(0);
        measure.ChordSymbol = new ChordSymbol(candidate.DisplayName, candidate.InversionLabel);

        var trebleNotes = candidate.Chord.PitchClasses
            .Select(pc => NotePitch.FromPitchClass(pc, 4, preference))
            .OrderByDescending(note => note.MidiNumber)
            .ToArray();
        foreach (var note in trebleNotes)
        {
            measure.Events.Add(new NoteEvent
            {
                Start = Rational.Zero,
                Duration = Rational.One,
                Staff = NotationStaff.Treble,
                Voice = 0,
                Origin = EventOrigin.User,
                Pitch = note,
                BaseDuration = NotationDuration.Whole,
                DotCount = 0
            });
        }

        if (candidate.BassPitchClass is int bassPitchClass)
        {
            measure.Events.Add(new NoteEvent
            {
                Start = Rational.Zero,
                Duration = Rational.One,
                Staff = NotationStaff.Bass,
                Voice = 0,
                Origin = EventOrigin.User,
                Pitch = NotePitch.FromPitchClass(bassPitchClass, 2, preference),
                BaseDuration = NotationDuration.Whole,
                DotCount = 0
            });
        }
        else
        {
            measure.Events.Add(new RestEvent
            {
                Start = Rational.Zero,
                Duration = Rational.One,
                Staff = NotationStaff.Bass,
                Voice = 0,
                Origin = EventOrigin.Auto
            });
        }

        score.ReindexMeasures();
        return score;
    }

    private RealtimeChordCandidate BuildCandidate(
        ChordRecognitionCandidate candidate,
        RealtimeChordWindowSnapshot snapshot,
        int? bassPitchClass,
        IReadOnlyDictionary<int, int> duplicatePitchClassCounts,
        IReadOnlyList<NotePitch> observedNotes,
        RealtimeChordDetectionOptions options)
    {
        var voicing = voicingAnalyzer.Analyze(candidate.Chord, observedNotes, options.EnharmonicPreference, options.Difficulty);
        var weightedScore = candidate.Score
            + candidate.MatchedTones.Count * 1.9
            - candidate.MissingImportantTones.Count * 1.4
            - candidate.Contradictions.Count * 2.8
            - ComplexityPenalty(candidate.Chord, options.Difficulty)
            + BassAlignmentBonus(bassPitchClass, candidate.Chord.RootPitchClass);

        var scales = TonalScaleLibrary.GetCandidateScalesForChord(
                candidate.Chord,
                contextKeyPitchClass: bassPitchClass ?? candidate.Chord.RootPitchClass,
                maxResults: options.MaxScaleSuggestions)
            .Select(scale => new CandidateScaleSuggestion(
                RootPitchClass: scale.RootPitchClass,
                Mode: scale.Mode,
                Name: scale.Name,
                IntervalFormula: scale.IntervalFormula,
                PitchClasses: scale.PitchClasses,
                Confidence: scale.Score))
            .ToArray();

        return new RealtimeChordCandidate(
            Chord: candidate.Chord,
            DisplayName: voicing.DisplayName,
            InversionLabel: voicing.InversionLabel,
            BassPitchClass: voicing.BassPitchClass,
            IntervalStructure: candidate.Chord.Definition.IntervalFormula,
            MatchedPitchClasses: candidate.MatchedTones,
            MissingPitchClasses: candidate.MissingImportantTones,
            ContradictionPitchClasses: candidate.Contradictions,
            DuplicatePitchClassCounts: duplicatePitchClassCounts,
            CompatibleScales: scales,
            Score: weightedScore,
            Explanation: candidate.Explanation);
    }

    private DetectionAttempt EvaluateWithScoredPitchClassSelection(
        RealtimeChordWindowSnapshot snapshot,
        RealtimeNoteScoreSnapshot scores,
        IReadOnlyList<int> rankedPitchClasses,
        RealtimeChordDetectionOptions options)
    {
        var startCount = Math.Max(1, Math.Min(options.StartPitchClassCount, rankedPitchClasses.Count));
        var maxCount = Math.Max(startCount, Math.Min(options.MaxPitchClassCount, rankedPitchClasses.Count));

        DetectionAttempt? bestAttempt = null;
        DetectionAttempt? bestAcceptedAttempt = null;
        for (var selectedCount = startCount; selectedCount <= maxCount; selectedCount++)
        {
            var selectedPitchClasses = rankedPitchClasses.Take(selectedCount).ToArray();
            var attempt = EvaluateRecognitionAttempt(
                snapshot,
                scores,
                selectedPitchClasses,
                options);
            if (bestAttempt is null || IsBetterAttempt(attempt, bestAttempt))
            {
                bestAttempt = attempt;
            }

            if (IsAcceptedAttempt(attempt, options))
            {
                if (bestAcceptedAttempt is null || IsBetterAttempt(attempt, bestAcceptedAttempt))
                {
                    bestAcceptedAttempt = attempt;
                }
            }
        }

        return bestAcceptedAttempt ?? bestAttempt ?? DetectionAttempt.Empty;
    }

    private DetectionAttempt EvaluateRecognitionAttempt(
        RealtimeChordWindowSnapshot snapshot,
        RealtimeNoteScoreSnapshot? scores,
        IReadOnlyList<int> selectedPitchClasses,
        RealtimeChordDetectionOptions options)
    {
        if (selectedPitchClasses.Count == 0)
        {
            return DetectionAttempt.Empty;
        }

        var context = RecognitionContext.ForDifficulty(
            options.Difficulty,
            options.EnharmonicPreference,
            allowedChordSymbols: null,
            maxCandidates: Math.Max(options.MaxCandidates * 2, 10));
        var recognition = recognitionEngine.RecognizeChordFromSubset(selectedPitchClasses, context);
        if (recognition.Candidates.Count == 0)
        {
            return DetectionAttempt.Empty with { SelectedPitchClassCount = selectedPitchClasses.Count };
        }

        var selectedSet = selectedPitchClasses.ToHashSet();
        var bassPitchClass = ResolveBassPitchClass(snapshot, scores, selectedSet);
        var observedNotes = BuildObservedNotes(snapshot, selectedSet, bassPitchClass, options.EnharmonicPreference);
        var duplicatePitchClassCounts = FilterDuplicatePitchClassCounts(snapshot.DuplicatePitchClassCounts, selectedSet);
        var scoredCandidates = recognition.Candidates
            .Select(candidate => BuildCandidate(
                candidate,
                snapshot,
                bassPitchClass,
                duplicatePitchClassCounts,
                observedNotes,
                options))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, options.MaxCandidates))
            .ToArray();

        var confidence = ComputeDetectionConfidence(scoredCandidates, selectedPitchClasses, scores);
        return new DetectionAttempt(
            Candidates: scoredCandidates,
            Confidence: confidence,
            SelectedPitchClassCount: selectedPitchClasses.Count);
    }

    private static bool IsAcceptedAttempt(DetectionAttempt attempt, RealtimeChordDetectionOptions options)
    {
        if (attempt.Candidates.Count == 0)
        {
            return false;
        }

        var best = attempt.Candidates[0];
        return attempt.Confidence >= options.MinDetectionConfidence &&
               best.MatchedPitchClasses.Count >= Math.Max(1, options.MinMatchedPitchClasses);
    }

    private static bool IsBetterAttempt(DetectionAttempt candidate, DetectionAttempt baseline)
    {
        if (candidate.Candidates.Count == 0 && baseline.Candidates.Count > 0)
        {
            return false;
        }

        if (candidate.Candidates.Count > 0 && baseline.Candidates.Count == 0)
        {
            return true;
        }

        if (candidate.Candidates.Count == 0 && baseline.Candidates.Count == 0)
        {
            return candidate.Confidence > baseline.Confidence;
        }

        var candidateQuality = ComputeAttemptQuality(candidate);
        var baselineQuality = ComputeAttemptQuality(baseline);
        var qualityDelta = candidateQuality - baselineQuality;
        if (Math.Abs(qualityDelta) > 0.0001)
        {
            return qualityDelta > 0;
        }

        var confidenceDelta = candidate.Confidence - baseline.Confidence;
        return confidenceDelta > 0;
    }

    private static double ComputeAttemptQuality(DetectionAttempt attempt)
    {
        if (attempt.Candidates.Count == 0)
        {
            return attempt.Confidence;
        }

        var best = attempt.Candidates[0];
        return best.Score
            + (attempt.Confidence * 2.2)
            + (best.MatchedPitchClasses.Count * 0.85)
            - (best.ContradictionPitchClasses.Count * 0.45);
    }

    private static IReadOnlyList<int> BuildRankedPitchClassesForScoredDetection(
        RealtimeChordWindowSnapshot snapshot,
        RealtimeNoteScoreSnapshot? scores,
        RealtimeChordDetectionOptions options)
    {
        if (scores is null)
        {
            return Array.Empty<int>();
        }

        var ranked = new List<int>();
        var minScore = Math.Max(0, options.MinPitchClassScoreToInclude);
        foreach (var pair in scores.RankedPitchClassScores)
        {
            if (pair.Score < minScore)
            {
                continue;
            }

            var pitchClass = PitchMath.NormalizePitchClass(pair.PitchClass);
            if (!ranked.Contains(pitchClass))
            {
                ranked.Add(pitchClass);
            }
        }

        if (ranked.Count < options.StartPitchClassCount)
        {
            foreach (var pair in scores.RankedPitchClassScores)
            {
                var pitchClass = PitchMath.NormalizePitchClass(pair.PitchClass);
                if (!ranked.Contains(pitchClass))
                {
                    ranked.Add(pitchClass);
                }

                if (ranked.Count >= options.StartPitchClassCount)
                {
                    break;
                }
            }
        }

        foreach (var pitchClass in snapshot.ActivePitchClasses)
        {
            var normalized = PitchMath.NormalizePitchClass(pitchClass);
            if (!ranked.Contains(normalized))
            {
                ranked.Add(normalized);
            }
        }

        return ranked;
    }

    private static IReadOnlyList<NotePitch> BuildObservedNotes(
        RealtimeChordWindowSnapshot snapshot,
        IReadOnlySet<int> selectedPitchClasses,
        int? bassPitchClass,
        EnharmonicPreference preference)
    {
        var observed = snapshot.ActiveNotes
            .Where(note => selectedPitchClasses.Contains(note.PitchClass))
            .Select(note => NotePitch.FromMidiNumber(note.MidiNote, preference))
            .ToList();

        if (bassPitchClass is int bassPc && observed.All(note => PitchMath.NormalizePitchClass(note.MidiNumber) != bassPc))
        {
            observed.Add(NotePitch.FromPitchClass(bassPc, 2, preference));
        }

        return observed;
    }

    private static int? ResolveBassPitchClass(
        RealtimeChordWindowSnapshot snapshot,
        RealtimeNoteScoreSnapshot? scores,
        IReadOnlySet<int> selectedPitchClasses)
    {
        if (snapshot.ActiveNotes.Count > 0)
        {
            var selectedBass = snapshot.ActiveNotes
                .Where(note => selectedPitchClasses.Contains(note.PitchClass))
                .OrderBy(note => note.MidiNote)
                .Select(note => (int?)note.PitchClass)
                .FirstOrDefault();
            if (selectedBass.HasValue)
            {
                return selectedBass.Value;
            }
        }

        if (snapshot.BassPitchClass is int snapshotBass && selectedPitchClasses.Contains(snapshotBass))
        {
            return snapshotBass;
        }

        if (scores?.BassPitchClass is int scoredBass && selectedPitchClasses.Contains(scoredBass))
        {
            return scoredBass;
        }

        return selectedPitchClasses.Count == 0
            ? null
            : selectedPitchClasses.Min();
    }

    private static IReadOnlyDictionary<int, int> FilterDuplicatePitchClassCounts(
        IReadOnlyDictionary<int, int> counts,
        IReadOnlySet<int> selectedPitchClasses)
    {
        if (counts.Count == 0)
        {
            return counts;
        }

        var filtered = counts
            .Where(pair => selectedPitchClasses.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return filtered;
    }

    private static double ComputeDetectionConfidence(
        IReadOnlyList<RealtimeChordCandidate> candidates,
        IReadOnlyList<int> selectedPitchClasses,
        RealtimeNoteScoreSnapshot? scores)
    {
        if (candidates.Count == 0)
        {
            return 0;
        }

        var best = candidates[0];
        var secondScore = candidates.Count > 1 ? candidates[1].Score : best.Score - 0.75;
        var gap = best.Score - secondScore;
        var sigmoidGap = Sigmoid(gap / 2.5);

        var selectedScoreSum = 0d;
        var matchedScoreSum = 0d;
        if (scores is not null && scores.PitchClassScores.Count > 0)
        {
            foreach (var pitchClass in selectedPitchClasses)
            {
                selectedScoreSum += scores.PitchClassScores.GetValueOrDefault(PitchMath.NormalizePitchClass(pitchClass));
            }

            foreach (var pitchClass in best.MatchedPitchClasses)
            {
                matchedScoreSum += scores.PitchClassScores.GetValueOrDefault(PitchMath.NormalizePitchClass(pitchClass));
            }
        }

        var coverage = selectedScoreSum > 0
            ? Math.Clamp(matchedScoreSum / selectedScoreSum, 0, 1)
            : Math.Clamp((double)best.MatchedPitchClasses.Count / Math.Max(1, selectedPitchClasses.Count), 0, 1);
        return (sigmoidGap * 0.55) + (coverage * 0.45);
    }

    private static double Sigmoid(double value)
    {
        return 1.0 / (1.0 + Math.Exp(-value));
    }

    private static IReadOnlyList<CandidateScaleSuggestion> BuildInferredScaleContext(
        RealtimeChordWindowSnapshot snapshot,
        RealtimeNoteScoreSnapshot? scores,
        RealtimeChordDetectionOptions options)
    {
        var sourceScores = new Dictionary<int, double>();
        if (scores is not null && scores.PitchClassScores.Count > 0)
        {
            foreach (var pair in scores.PitchClassScores)
            {
                sourceScores[PitchMath.NormalizePitchClass(pair.Key)] = pair.Value;
            }
        }
        else
        {
            foreach (var note in snapshot.ActiveNotes)
            {
                sourceScores[note.PitchClass] = sourceScores.GetValueOrDefault(note.PitchClass) + 1.0;
            }
        }

        if (sourceScores.Count == 0)
        {
            return Array.Empty<CandidateScaleSuggestion>();
        }

        var impliedRoot = scores?.BassPitchClass ?? snapshot.BassPitchClass;
        return TonalScaleLibrary.GetCandidateScalesForPitchClassScores(
                sourceScores,
                impliedRootPitchClass: impliedRoot,
                maxResults: Math.Max(1, Math.Min(4, options.MaxScaleSuggestions)))
            .Select(scale => new CandidateScaleSuggestion(
                RootPitchClass: scale.RootPitchClass,
                Mode: scale.Mode,
                Name: scale.Name,
                IntervalFormula: scale.IntervalFormula,
                PitchClasses: scale.PitchClasses,
                Confidence: scale.Score))
            .ToArray();
    }

    private static IReadOnlyList<int> BuildRecognitionPitchClasses(
        RealtimeChordWindowSnapshot snapshot,
        RealtimeChordDetectionOptions options)
    {
        var grouped = snapshot.ActiveNotes
            .GroupBy(note => note.PitchClass)
            .Select(group =>
            {
                var weight = group.Sum(note => note.IsSustained
                    ? Math.Clamp(1.0 - (note.AgeMs / options.SustainDecayMs), 0.10, 1.0)
                    : 1.0);
                var hasPressedNote = group.Any(note => !note.IsSustained);
                return (PitchClass: group.Key, Weight: weight, HasPressedNote: hasPressedNote);
            })
            .Where(group => group.HasPressedNote || group.Weight >= 0.30)
            .OrderBy(group => group.PitchClass)
            .Select(group => group.PitchClass)
            .ToArray();

        return grouped;
    }

    private static double ComplexityPenalty(ChordInstance chord, DifficultyPreset difficulty)
    {
        if (difficulty != DifficultyPreset.Beginner)
        {
            return 0;
        }

        var chordToneCount = chord.Definition.Intervals.Count;
        return Math.Max(0, chordToneCount - 3) * 1.5;
    }

    private static double BassAlignmentBonus(int? bassPitchClass, int chordRootPitchClass)
    {
        if (!bassPitchClass.HasValue)
        {
            return 0;
        }

        return bassPitchClass.Value == chordRootPitchClass
            ? 1.2
            : 0;
    }

    private sealed record DetectionAttempt(
        IReadOnlyList<RealtimeChordCandidate> Candidates,
        double Confidence,
        int SelectedPitchClassCount)
    {
        public static DetectionAttempt Empty { get; } = new(Array.Empty<RealtimeChordCandidate>(), 0, 0);
    }
}
