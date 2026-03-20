// Template: RealtimeNoteScoreTracker
// This is a helper snippet to speed up implementation.
// Adjust names/paths to match the repository conventions.

using System.Collections.Concurrent;
using MusicTheory.Core.Models;

namespace MusicTheory.Core.Recognition;

public sealed class RealtimeNoteScoreTracker
{
    private readonly RealtimeNoteScoreOptions options;

    // MIDI note -> state
    private readonly ConcurrentDictionary<int, NoteState> notes = new();

    private volatile bool sustainDown;

    public RealtimeNoteScoreTracker(RealtimeNoteScoreOptions options)
    {
        this.options = options;
    }

    public void Reset()
    {
        notes.Clear();
        sustainDown = false;
    }

    public void Apply(RealtimeMidiEvent midiEvent)
    {
        switch (midiEvent.Type)
        {
            case RealtimeMidiEventType.NoteOn:
                ApplyNoteOn(midiEvent.Note, midiEvent.Velocity, midiEvent.TimestampMs);
                break;
            case RealtimeMidiEventType.NoteOff:
                ApplyNoteOff(midiEvent.Note, midiEvent.TimestampMs);
                break;
            case RealtimeMidiEventType.Sustain:
                ApplySustain(midiEvent.SustainDown, midiEvent.TimestampMs);
                break;
        }
    }

    public RealtimeNoteScoreSnapshot GetSnapshot(double nowMs)
    {
        // Lazily update note scores on read, prune old/low-score notes,
        // aggregate pitch class scores, and return sorted list.
        // Keep this method allocation-light; consider pooling.
        throw new NotImplementedException();
    }

    private void ApplyNoteOn(int midiNote, int velocity, double nowMs)
    {
        var state = notes.GetOrAdd(midiNote, _ => new NoteState(midiNote, nowMs));
        lock (state)
        {
            UpdateState(state, nowMs);
            state.IsPressed = true;
            state.IsSustained = false;
            var v = Math.Clamp(velocity / 127.0, 0.0, 1.0);
            state.Score += options.NoteOnBoost * (options.VelocityWeight * v + (1.0 - options.VelocityWeight));
        }
    }

    private void ApplyNoteOff(int midiNote, double nowMs)
    {
        if (!notes.TryGetValue(midiNote, out var state))
        {
            return;
        }

        lock (state)
        {
            UpdateState(state, nowMs);
            state.IsPressed = false;
            if (sustainDown)
            {
                state.IsSustained = true;
            }
        }
    }

    private void ApplySustain(bool down, double nowMs)
    {
        sustainDown = down;
        if (!down)
        {
            // When sustain is released, previously sustained notes stop being held.
            foreach (var kvp in notes)
            {
                var state = kvp.Value;
                lock (state)
                {
                    UpdateState(state, nowMs);
                    state.IsSustained = false;
                }
            }
        }
    }

    private void UpdateState(NoteState state, double nowMs)
    {
        var dt = nowMs - state.LastUpdateMs;
        if (dt <= 0)
        {
            return;
        }

        var decayMs = Math.Max(1.0, options.DecayMs);
        state.Score *= Math.Exp(-dt / decayMs);

        if (state.IsPressed || state.IsSustained)
        {
            var heldMultiplier = state.IsPressed ? 1.0 : options.SustainHeldMultiplier;
            var dtSeconds = dt / 1000.0;
            state.Score += dtSeconds * options.HoldBoostPerSecond * heldMultiplier;
        }

        state.LastUpdateMs = nowMs;
    }

    private sealed class NoteState
    {
        public NoteState(int midiNote, double nowMs)
        {
            MidiNote = midiNote;
            LastUpdateMs = nowMs;
        }

        public int MidiNote { get; }
        public double Score { get; set; }
        public bool IsPressed { get; set; }
        public bool IsSustained { get; set; }
        public double LastUpdateMs { get; set; }
    }
}
