using MusicTheory.Core.Theory;

namespace MusicTheory.Core.Recognition;

public sealed class RealtimeNoteScoreTracker(RealtimeNoteScoreOptions? options = null)
{
    private readonly RealtimeNoteScoreOptions options = options ?? new RealtimeNoteScoreOptions();
    private readonly Dictionary<int, TrackedNoteState> trackedNotes = new();
    private readonly object sync = new();

    private bool sustainPedalDown;
    private double lastTimestampMs;

    public RealtimeNoteScoreOptions Options => options;

    public void Reset()
    {
        lock (sync)
        {
            trackedNotes.Clear();
            sustainPedalDown = false;
            lastTimestampMs = 0;
        }
    }

    public void Apply(RealtimeMidiEvent midiEvent)
    {
        var timestampMs = NormalizeTimestamp(midiEvent.TimestampMs);
        lock (sync)
        {
            lastTimestampMs = Math.Max(lastTimestampMs, timestampMs);

            switch (midiEvent.Type)
            {
                case RealtimeMidiEventType.NoteOn:
                    ApplyNoteOn(midiEvent.MidiNote, midiEvent.Value, timestampMs);
                    break;
                case RealtimeMidiEventType.NoteOff:
                    ApplyNoteOff(midiEvent.MidiNote, timestampMs);
                    break;
                case RealtimeMidiEventType.SustainPedal:
                    ApplySustain(midiEvent.SustainDown ?? (midiEvent.Value ?? 0) >= 64, timestampMs);
                    break;
            }

            PruneInternal(timestampMs);
        }
    }

    public RealtimeNoteScoreSnapshot GetSnapshot(double nowMs)
    {
        var timestampMs = NormalizeTimestamp(nowMs);
        lock (sync)
        {
            var effectiveNowMs = Math.Max(lastTimestampMs, timestampMs);
            if (trackedNotes.Count == 0)
            {
                return RealtimeNoteScoreSnapshot.Empty(effectiveNowMs);
            }

            var midiScores = new Dictionary<int, double>(trackedNotes.Count);
            var pitchClassScores = new Dictionary<int, double>(12);
            var bassCandidates = new List<(int MidiNote, double Score, bool IsActive)>(trackedNotes.Count);

            foreach (var state in trackedNotes.Values)
            {
                AdvanceStateTo(state, effectiveNowMs);
                if (state.Score <= 0)
                {
                    continue;
                }

                midiScores[state.MidiNote] = state.Score;
                var pitchClass = PitchMath.NormalizePitchClass(state.MidiNote);
                pitchClassScores[pitchClass] = pitchClassScores.GetValueOrDefault(pitchClass) + state.Score;
                bassCandidates.Add((state.MidiNote, state.Score, state.IsHeld || state.IsSustained));
            }

            var rankedPitchClassScores = pitchClassScores
                .Select(pair => new RealtimePitchClassScore(pair.Key, pair.Value))
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.PitchClass)
                .ToArray();
            var bassPitchClass = ResolveBassPitchClass(bassCandidates);
            var tooManyNotes = trackedNotes.Count > ResolveMaxTrackedNotes();

            PruneInternal(effectiveNowMs);

            return new RealtimeNoteScoreSnapshot(
                TimestampMs: effectiveNowMs,
                RankedPitchClassScores: rankedPitchClassScores,
                PitchClassScores: pitchClassScores,
                MidiNoteScores: midiScores,
                BassPitchClass: bassPitchClass,
                TooManyNotes: tooManyNotes);
        }
    }

    private void ApplyNoteOn(int? midiNote, int? velocity, double timestampMs)
    {
        if (!TryNormalizeMidi(midiNote, out var note))
        {
            return;
        }

        var state = GetOrCreateState(note, timestampMs);
        AdvanceStateTo(state, timestampMs);

        var velocityFactor = ResolveVelocityFactor(velocity ?? 96);
        state.Score += ResolveNoteOnBoost() * velocityFactor;
        state.IsHeld = true;
        state.IsSustained = false;
        state.LastUpdateMs = timestampMs;
        state.LastActivityMs = timestampMs;
        state.LastVelocity = Math.Clamp(velocity ?? 96, 0, 127);
    }

    private void ApplyNoteOff(int? midiNote, double timestampMs)
    {
        if (!TryNormalizeMidi(midiNote, out var note))
        {
            return;
        }

        if (!trackedNotes.TryGetValue(note, out var state))
        {
            return;
        }

        AdvanceStateTo(state, timestampMs);
        state.IsHeld = false;
        state.IsSustained = sustainPedalDown;
        state.LastUpdateMs = timestampMs;
        state.LastActivityMs = timestampMs;
    }

    private void ApplySustain(bool isDown, double timestampMs)
    {
        if (sustainPedalDown == isDown)
        {
            return;
        }

        sustainPedalDown = isDown;
        foreach (var state in trackedNotes.Values)
        {
            AdvanceStateTo(state, timestampMs);
            if (state.IsHeld)
            {
                state.IsSustained = false;
            }
            else
            {
                state.IsSustained = isDown;
            }

            state.LastUpdateMs = timestampMs;
            state.LastActivityMs = timestampMs;
        }
    }

    private TrackedNoteState GetOrCreateState(int midiNote, double timestampMs)
    {
        if (trackedNotes.TryGetValue(midiNote, out var existing))
        {
            return existing;
        }

        var created = new TrackedNoteState
        {
            MidiNote = midiNote,
            Score = 0,
            LastUpdateMs = timestampMs,
            LastActivityMs = timestampMs,
            IsHeld = false,
            IsSustained = false,
            LastVelocity = 96
        };
        trackedNotes[midiNote] = created;
        return created;
    }

    private void AdvanceStateTo(TrackedNoteState state, double nowMs)
    {
        var dtMs = Math.Max(0, nowMs - state.LastUpdateMs);
        if (dtMs <= 0)
        {
            return;
        }

        var decayMs = ResolveDecayMs();
        state.Score *= Math.Exp(-dtMs / decayMs);

        var holdMultiplier = state.IsHeld
            ? 1.0
            : state.IsSustained
                ? ResolveSustainHeldMultiplier()
                : 0.0;
        if (holdMultiplier > 0)
        {
            var dtSeconds = dtMs / 1000d;
            state.Score += dtSeconds * ResolveHoldBoostPerSecond() * holdMultiplier;
        }

        state.LastUpdateMs = nowMs;
    }

    private int? ResolveBassPitchClass(IReadOnlyList<(int MidiNote, double Score, bool IsActive)> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var threshold = Math.Max(ResolveMinScoreToKeep(), ResolveBassScoreThreshold());
        var active = candidates
            .Where(candidate => candidate.IsActive && candidate.Score >= threshold)
            .OrderBy(candidate => candidate.MidiNote)
            .FirstOrDefault();
        if (active.MidiNote >= 0)
        {
            return PitchMath.NormalizePitchClass(active.MidiNote);
        }

        var fallback = candidates
            .Where(candidate => candidate.Score >= threshold)
            .OrderBy(candidate => candidate.MidiNote)
            .FirstOrDefault();
        return fallback.MidiNote >= 0
            ? PitchMath.NormalizePitchClass(fallback.MidiNote)
            : null;
    }

    private void PruneInternal(double nowMs)
    {
        if (trackedNotes.Count == 0)
        {
            return;
        }

        var minScore = ResolveMinScoreToKeep();
        var windowMs = ResolveWindowMs();
        foreach (var state in trackedNotes.Values)
        {
            AdvanceStateTo(state, nowMs);
        }

        var removableByDecay = trackedNotes
            .Where(pair =>
                !pair.Value.IsHeld &&
                !pair.Value.IsSustained &&
                pair.Value.Score < minScore &&
                (nowMs - pair.Value.LastActivityMs) >= windowMs)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var note in removableByDecay)
        {
            trackedNotes.Remove(note);
        }

        var maxTracked = ResolveMaxTrackedNotes();
        if (trackedNotes.Count <= maxTracked)
        {
            return;
        }

        var overflow = trackedNotes.Count - maxTracked;
        var removableOverflow = trackedNotes
            .Where(pair => !pair.Value.IsHeld && !pair.Value.IsSustained)
            .OrderBy(pair => pair.Value.Score)
            .ThenBy(pair => pair.Value.LastActivityMs)
            .Take(overflow)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var note in removableOverflow)
        {
            trackedNotes.Remove(note);
        }
    }

    private double ResolveDecayMs()
    {
        return Math.Max(20, options.DecayMs);
    }

    private double ResolveWindowMs()
    {
        return Math.Max(100, options.WindowMs);
    }

    private double ResolveNoteOnBoost()
    {
        return Math.Max(0.01, options.NoteOnBoost);
    }

    private double ResolveHoldBoostPerSecond()
    {
        return Math.Max(0, options.HoldBoostPerSecond);
    }

    private double ResolveSustainHeldMultiplier()
    {
        return Math.Clamp(options.SustainHeldMultiplier, 0, 1);
    }

    private double ResolveMinScoreToKeep()
    {
        return Math.Max(0.0001, options.MinScoreToKeep);
    }

    private int ResolveMaxTrackedNotes()
    {
        return Math.Clamp(options.MaxTrackedNotes, 12, 128);
    }

    private double ResolveBassScoreThreshold()
    {
        return Math.Max(0, options.BassScoreThreshold);
    }

    private double ResolveVelocityFactor(int velocity)
    {
        var normalizedVelocity = Math.Clamp(velocity, 0, 127) / 127d;
        var weight = Math.Clamp(options.VelocityWeight, 0, 1);
        return (1 - weight) + (weight * normalizedVelocity);
    }

    private static bool TryNormalizeMidi(int? midiNote, out int normalized)
    {
        normalized = midiNote ?? -1;
        return normalized is >= 0 and <= 127;
    }

    private static double NormalizeTimestamp(double timestampMs)
    {
        return double.IsFinite(timestampMs) && timestampMs >= 0
            ? timestampMs
            : 0;
    }

    private sealed class TrackedNoteState
    {
        public int MidiNote { get; set; }
        public double Score { get; set; }
        public double LastUpdateMs { get; set; }
        public double LastActivityMs { get; set; }
        public bool IsHeld { get; set; }
        public bool IsSustained { get; set; }
        public int LastVelocity { get; set; }
    }
}
