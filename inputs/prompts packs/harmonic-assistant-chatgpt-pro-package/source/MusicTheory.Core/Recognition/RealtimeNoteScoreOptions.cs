namespace MusicTheory.Core.Recognition;

public sealed record RealtimeNoteScoreOptions(
    double WindowMs = 1800,
    double DecayMs = 1200,
    double NoteOnBoost = 1.0,
    double VelocityWeight = 0.35,
    double HoldBoostPerSecond = 0.9,
    double SustainHeldMultiplier = 0.35,
    double MinScoreToKeep = 0.03,
    int MaxTrackedNotes = 48,
    double BassScoreThreshold = 0.08);
