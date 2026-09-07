# Pre-timing chart geometry adjudication

Real 1600 x 1000 Web browser probe (Normal and Long) confirms the inherited bottom legend clips its second row: second label bottom 767.375, inner overflow-hidden chart wrapper bottom 755.375. The Surface now correctly owns its scoped card geometry, but that alone does not correct Apex bottom-legend sizing. Failure screenshots and full ancestor bounds are retained in browser-o02-layout.

Use the existing public chart option to place this compact donut legend at the top. This bounded legibility correction avoids an application CSS override of library internals, a duplicate compatibility stylesheet, or another sibling chart change. Re-run normal and long geometry before accepting this correction; no timing series has started. The chart, provider labels, totals and interactions remain the same.

First O02 component run: 38/42 pass. Four failures are test setup defects, not product regressions: three late DI mutations after the harness initialized, and one DOM event retrieved outside the renderer dispatcher during independent header completion. Correct fixture construction/dispatch and retain that failed execution.
