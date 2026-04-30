# QA Prompt

Validate the implemented subbundle against the raw notes and screenshots.

For code:

- Build the changed projects.
- Run targeted tests when present.
- Confirm the sandbox page does not use `ApexChart` or `ApexPointSeries` directly.
- Confirm `AddCanDoItAllCharts()` and chart asset inclusion are the host-facing setup.

For browser proof on `/groups/charts`:

- Navigate in a real browser to the route.
- Capture desktop and mobile screenshots.
- Assert Apex-generated chart DOM/SVG content exists and is nonblank.
- Review the screenshots with these questions:
  - Can all text be read without zooming?
  - Are legends, labels, toolbars, charts, and summary context unclipped?
  - Is anything overlapping or visually colliding?
  - Are the examples aligned with the existing sandbox visual system?
  - Do the examples prove pie, single-line, multi-line, area fill, color tuning, labels, and units?

Record browser-validation analytics, gate results, and raw-note closure while the proof is fresh.
