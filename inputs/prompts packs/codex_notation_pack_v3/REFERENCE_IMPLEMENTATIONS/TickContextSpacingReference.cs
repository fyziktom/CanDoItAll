// Reference skeleton: VexFlow-like TickContext spacing in C#.
// This is NOT wired into the repo automatically. Codex should adapt it.

using MusicTheory.Core.NotationEditor.Model;

namespace MusicTheory.Core.NotationEditor.Layout;

public sealed class TickContextSpacingReference
{
    public sealed class Slot
    {
        public required Rational Start { get; init; }
        public double MinLeft { get; set; }
        public double GlyphWidth { get; set; }
        public double MinRight { get; set; }
        public double Padding { get; set; } = 4;
        public double MinWidth => MinLeft + GlyphWidth + MinRight + Padding;
        public double X { get; set; }
    }

    public sealed class Plan
    {
        public required Slot[] Slots { get; init; }
        public required double MinTotalWidth { get; init; }
    }

    public static Plan BuildPlan(
        ScoreMeasure measure,
        Rational capacity,
        ScoreLayoutMeasure layoutMeasure,
        ScoreLayout layout,
        IReadOnlyDictionary<Guid, AccidentalPlacement[]> accidentalsByEventId)
    {
        // 1) Build unique starts.
        var starts = measure.Events
            .Select(e => e.Start)
            .Distinct()
            .OrderBy(t => t)
            .ToArray();

        var slots = new List<Slot>(starts.Length);

        foreach (var start in starts)
        {
            // Collect all events at this start.
            var atStart = layoutMeasure.Events.Where(e => e.Start == start).ToArray();

            // Compute min widths across all events at this slot.
            var minLeft = 0.0;
            var glyphWidth = 0.0;
            var minRight = 0.0;

            foreach (var ev in atStart)
            {
                // Baseline glyph widths (use layout constants).
                glyphWidth = Math.Max(glyphWidth, ev.IsRest ? layout.RestWidth : layout.NoteHeadWidth);

                // Dots: reserve right space.
                minRight = Math.Max(minRight, ev.DotCount * (layout.DotWidth + layout.DotSpacing));

                // Accidentals: reserve left space (column count derived from placements).
                if (!ev.IsRest && accidentalsByEventId.TryGetValue(ev.EventId, out var placements))
                {
                    // Count columns in placements, e.g. by distinct X offsets.
                    var columns = placements.Select(p => p.Column).Distinct().Count();
                    minLeft = Math.Max(minLeft, columns * layout.AccidentalColumnWidth);
                }

                // Optional: add flag space if not beamed.
            }

            slots.Add(new Slot
            {
                Start = start,
                MinLeft = minLeft,
                GlyphWidth = glyphWidth,
                MinRight = minRight
            });
        }

        // 2) Compute minimum width.
        var gapMin = layout.MinSlotGapPx;
        var minTotal = slots.Sum(s => s.MinWidth) + gapMin * Math.Max(0, slots.Count - 1);

        return new Plan
        {
            Slots = slots.ToArray(),
            MinTotalWidth = minTotal
        };
    }

    public static void AssignX(Plan plan, double contentLeft, double contentWidth, Rational capacity, double gapMin)
    {
        // Deterministic justification: distribute leftover by time deltas.
        if (plan.Slots.Length == 0)
        {
            return;
        }

        var minTotal = plan.MinTotalWidth;
        var target = contentWidth;

        // Start with minimum allocation.
        var gaps = Math.Max(0, plan.Slots.Length - 1);
        var extra = Math.Max(0, target - minTotal);

        // Precompute time delta weights.
        var weights = new double[gaps];
        var weightSum = 0.0;
        for (var i = 0; i < gaps; i++)
        {
            var dt = (plan.Slots[i + 1].Start - plan.Slots[i].Start);
            var w = Math.Max(0.001, dt.ToDouble()); // or convert by ticks
            weights[i] = w;
            weightSum += w;
        }

        var x = contentLeft;
        for (var i = 0; i < plan.Slots.Length; i++)
        {
            var slot = plan.Slots[i];
            slot.X = x + slot.MinLeft; // anchor at glyph center start
            x += slot.MinWidth;

            if (i < gaps)
            {
                var gapExtra = weightSum > 0 ? extra * (weights[i] / weightSum) : extra / gaps;
                x += gapMin + gapExtra;
            }
        }
    }
}
