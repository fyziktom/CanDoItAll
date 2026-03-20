namespace MusicTheory.Core.Recognition;

public readonly record struct RealtimePitchClassScore(int PitchClass, double Score);

public sealed record RealtimeNoteScoreSnapshot(
    double TimestampMs,
    IReadOnlyList<RealtimePitchClassScore> RankedPitchClassScores,
    IReadOnlyDictionary<int, double> PitchClassScores,
    IReadOnlyDictionary<int, double> MidiNoteScores,
    int? BassPitchClass,
    bool TooManyNotes)
{
    public static RealtimeNoteScoreSnapshot Empty(double timestampMs = 0)
        => new(
            TimestampMs: timestampMs,
            RankedPitchClassScores: Array.Empty<RealtimePitchClassScore>(),
            PitchClassScores: new Dictionary<int, double>(),
            MidiNoteScores: new Dictionary<int, double>(),
            BassPitchClass: null,
            TooManyNotes: false);
}
