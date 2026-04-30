# Implementation Prompt

Implement the selected subbundle only. Read the root bundle README, `plan/01-phase-plan.md`, traceability, and the selected subbundle README first.

Keep the chart boundary CanDoItAll-owned:

- Product and sandbox pages should use `CanDoItAll.Components.Charts` models/components.
- Keep `ApexCharts` types inside the wrapper implementation except for unavoidable package registration internals.
- Use fresh options per chart instance.
- Use BaseLib layout components in the sandbox.
- Do not copy EnergoApp DTOs, services, Radzen controls, or domain copy.

After implementation, run the proof listed in the subbundle and update `reviews/01-execution-report.md` before moving on.
