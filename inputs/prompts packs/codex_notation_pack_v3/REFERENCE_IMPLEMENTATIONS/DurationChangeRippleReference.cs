// Reference skeleton: duration change that respects InsertMode ripple.
// This is NOT wired into the repo automatically. Codex should adapt it.

using MusicTheory.Core.NotationEditor.Model;

namespace MusicTheory.Core.NotationEditor.Commands;

public static class DurationChangeRippleReference
{
    /// <summary>
    /// Change the rhythmic duration of a chord-stack (all events at the same Start) and apply InsertMode semantics.
    /// </summary>
    public static void ChangeClusterDuration(
        ScoreDocument score,
        int measureIndex,
        NotationStaff staff,
        int voice,
        Rational start,
        Rational newDuration,
        InsertMode mode)
    {
        // Guard clauses omitted for brevity.
        var measure = score.Measures[measureIndex];
        var capacity = ScoreContext.GetMeasureCapacity(score, measureIndex);

        // 1) Find the chord stack (cluster).
        var cluster = measure.Events
            .Where(e => e.Staff == staff && e.Voice == voice && e.Start == start)
            .ToList();

        if (cluster.Count == 0)
        {
            return;
        }

        // 2) Determine old duration (expect all equal, but be robust).
        var oldDuration = cluster.Max(e => e.Duration);
        if (oldDuration == newDuration)
        {
            return;
        }

        var oldEnd = start + oldDuration;
        var newEnd = start + newDuration;
        var delta = newDuration - oldDuration;

        // 3) Update the cluster durations.
        foreach (var e in cluster)
        {
            // NOTE: in the repo, NoteEvent needs BaseDuration/DotCount updates.
            // RestEvent might need BaseDuration/DotCount added or derived.
            e.Duration = newDuration;
        }

        // 4) Apply InsertMode semantics.
        if (mode == InsertMode.Replace)
        {
            // Remove any events starting inside the new occupied region.
            measure.Events.RemoveAll(e =>
                e.Staff == staff &&
                e.Voice == voice &&
                e.Start >= start &&
                e.Start < newEnd &&
                e.Start != start);
        }
        else if (mode == InsertMode.InsertAndShift)
        {
            if (delta > Rational.Zero)
            {
                foreach (var e in measure.Events)
                {
                    if (e.Staff != staff || e.Voice != voice)
                    {
                        continue;
                    }

                    if (e.Start >= oldEnd && e.Start != start)
                    {
                        e.Start += delta;
                    }
                }
            }
            // If delta < 0, do not pull events left by default; keep gap for rests.
        }
        else if (mode == InsertMode.Split)
        {
            // Split/trim any overlapped events, preserving tails after newEnd.
            // The repo already has SplitInMeasure(...) which Codex can reuse.
        }

        // 5) Reflow across measures and auto-rest fill.
        ReflowEngine.NormalizeFrom(score, measureIndex);
        AutoRestFillEngine.RecomputeAll(score);

        // 6) Optional: validate invariants in debug builds.
        // ValidateMeasureVoice(score, measureIndex, staff, voice, capacity);
    }
}
