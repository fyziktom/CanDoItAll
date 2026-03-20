// NOTE: This is a reference snippet for Codex. Integrate into your actual model layer.
// All comments are in English by requirement.

using System;
using System.Collections.Generic;
using MusicTheory.Core.Models;

namespace MusicTheory.Core.NotationEditor.Theory;

/// <summary>
/// Helpers for computing key signature fifths and default accidentals per note letter.
/// The mapping is for major/minor keys with up to 7 sharps or flats.
/// </summary>
public static class KeySignatureMath
{
    // Order of sharps and flats in key signatures.
    private static readonly NoteLetter[] SharpOrder = { NoteLetter.F, NoteLetter.C, NoteLetter.G, NoteLetter.D, NoteLetter.A, NoteLetter.E, NoteLetter.B };
    private static readonly NoteLetter[] FlatOrder  = { NoteLetter.B, NoteLetter.E, NoteLetter.A, NoteLetter.D, NoteLetter.G, NoteLetter.C, NoteLetter.F };

    // Circle-of-fifths mapping for MAJOR keys to fifths count (C=0, G=+1, F=-1, etc).
    // This avoids tricky enharmonic keys beyond +/-7.
    private static readonly Dictionary<string, int> MajorKeyToFifths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"]  = 0,
        ["G"]  = 1,
        ["D"]  = 2,
        ["A"]  = 3,
        ["E"]  = 4,
        ["B"]  = 5,
        ["F#"] = 6,
        ["C#"] = 7,
        ["F"]  = -1,
        ["Bb"] = -2,
        ["Eb"] = -3,
        ["Ab"] = -4,
        ["Db"] = -5,
        ["Gb"] = -6,
        ["Cb"] = -7
    };

    // Natural minor shares key signature with its relative major.
    private static readonly Dictionary<string, string> MinorToRelativeMajor = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"]  = "C",
        ["E"]  = "G",
        ["B"]  = "D",
        ["F#"] = "A",
        ["C#"] = "E",
        ["G#"] = "B",
        ["D#"] = "F#",
        ["A#"] = "C#",
        ["D"]  = "F",
        ["G"]  = "Bb",
        ["C"]  = "Eb",
        ["F"]  = "Ab",
        ["Bb"] = "Db",
        ["Eb"] = "Gb",
        ["Ab"] = "Cb"
    };

    /// <summary>
    /// Compute fifths count for a key signature. Returns 0 if unknown (fallback to C major).
    /// </summary>
    public static int GetFifths(string tonicToken, bool isMinor)
    {
        if (string.IsNullOrWhiteSpace(tonicToken))
            return 0;

        if (!isMinor)
            return MajorKeyToFifths.TryGetValue(tonicToken, out var f) ? f : 0;

        if (MinorToRelativeMajor.TryGetValue(tonicToken, out var relMaj) &&
            MajorKeyToFifths.TryGetValue(relMaj, out var fifths))
            return fifths;

        return 0;
    }

    /// <summary>
    /// Build a default-accidental map (NoteLetter -> Accidental) from fifths.
    /// </summary>
    public static Dictionary<NoteLetter, Accidental> BuildDefaultAccidentals(int fifths)
    {
        var map = new Dictionary<NoteLetter, Accidental>();
        foreach (NoteLetter letter in Enum.GetValues(typeof(NoteLetter)))
            map[letter] = Accidental.Natural;

        if (fifths > 0)
        {
            for (var i = 0; i < Math.Min(7, fifths); i++)
                map[SharpOrder[i]] = Accidental.Sharp;
        }
        else if (fifths < 0)
        {
            for (var i = 0; i < Math.Min(7, -fifths); i++)
                map[FlatOrder[i]] = Accidental.Flat;
        }

        return map;
    }
}
