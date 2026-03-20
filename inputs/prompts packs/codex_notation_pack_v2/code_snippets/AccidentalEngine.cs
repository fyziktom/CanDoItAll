// NOTE: This is a reference snippet for Codex. Integrate into your actual layout engine.
// All comments are in English by requirement.

using System;
using System.Collections.Generic;
using System.Linq;
using MusicTheory.Core.Models;
using MusicTheory.Core.NotationEditor.Model;

namespace MusicTheory.Core.NotationEditor.Layout;

/// <summary>
/// Computes which accidentals must be displayed for notes in a measure
/// given the effective key signature at that measure.
/// </summary>
public sealed class AccidentalEngine
{
    public IReadOnlyList<AccidentalLayout> ComputeAccidentals(
        IEnumerable<NoteEvent> notesInMeasure,
        KeySignatureContext key,
        bool resetAtBarline = true)
    {
        // Track state per staff (and optionally per voice).
        // Keyed by (staff, note letter, octave) => current accidental in effect.
        var state = new Dictionary<(NotationStaff Staff, NoteLetter Letter, int Octave), Accidental>();

        // Initialize with key signature defaults.
        foreach (NotationStaff staff in Enum.GetValues(typeof(NotationStaff)))
        {
            foreach (NoteLetter letter in Enum.GetValues(typeof(NoteLetter)))
            {
                // Use octave wildcard by initializing when first encountered,
                // or pre-initialize common octave ranges if you want.
                // Here we initialize lazily.
            }
        }

        var result = new List<AccidentalLayout>();

        // Ensure deterministic order.
        foreach (var note in notesInMeasure.OrderBy(n => n.Start).ThenBy(n => n.Pitch.Octave).ThenBy(n => n.Pitch.Name.Letter))
        {
            var letter = note.Pitch.Name.Letter;
            var octave = note.Pitch.Octave;
            var desired = note.Pitch.Name.Accidental;

            // Determine the baseline accidental from key signature (for this letter).
            var keyDefault = key.GetDefaultAccidental(letter);

            var keyState = (note.Staff, letter, octave);

            // If we never saw this pitch in the measure, assume key default as initial state.
            if (!state.TryGetValue(keyState, out var current))
            {
                current = keyDefault;
            }

            // Decide if we must show an accidental glyph.
            // The accidental is shown when the desired accidental differs from the current state.
            if (desired != current)
            {
                // Emit an accidental to be placed near this notehead.
                // The actual X/Y placement is done elsewhere (needs notehead geometry).
                result.Add(new AccidentalLayout(
                    NoteId: note.Id,
                    Staff: note.Staff,
                    Letter: letter,
                    Octave: octave,
                    Accidental: desired));

                // Update state for the rest of the measure.
                state[keyState] = desired;
            }
            else
            {
                // Keep current state; no glyph.
                state[keyState] = current;
            }
        }

        return result;
    }
}

/// <summary>
/// Output from AccidentalEngine. Layout should map this to glyph + exact X/Y.
/// </summary>
public readonly record struct AccidentalLayout(
    Guid NoteId,
    NotationStaff Staff,
    NoteLetter Letter,
    int Octave,
    Accidental Accidental);

/// <summary>
/// Effective key signature at a measure. This is a minimal API.
/// </summary>
public sealed class KeySignatureContext
{
    private readonly Dictionary<NoteLetter, Accidental> defaults;

    public KeySignatureContext(Dictionary<NoteLetter, Accidental> defaults)
    {
        this.defaults = defaults;
    }

    public Accidental GetDefaultAccidental(NoteLetter letter)
        => defaults.TryGetValue(letter, out var acc) ? acc : Accidental.Natural;
}
