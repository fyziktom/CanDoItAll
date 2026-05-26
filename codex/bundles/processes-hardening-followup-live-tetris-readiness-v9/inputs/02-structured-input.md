# Structured Input

## Objectives

- Make the bundle structurally executable under the bundle validator.
- Replace app-topic-specific instructions with generic Blazor WASM PWA application delivery language.
- Split seeded regression data from live-run profile data so a fresh live run is not pre-completed by seeded artifacts or transitions.
- Harden templates, process API skill guidance, assignment/tool validation, work briefs, artifact proof, writeback proof, and live-run preflight around reusable Blazor WASM PWA delivery.

## Hard constraints

- Preserve PostgreSQL-only runtime assumptions.
- Do not hardcode any app topic into process runtime code, process templates, reusable skills, or process API examples.
- Keep product mutation limited to implementation and repair steps.
- Keep validation, revalidation, writeback, and escalation steps non-mutating except for controlled external actions.
- Require build/test/runtime/browser/screenshot/console/writeback evidence or explicit typed blockers.

## Validation expectations

- Prepared-stage bundle validator passes before implementation begins.
- Source assertions prove no topic-specific app instructions remain in process templates or process API skill guidance.
- Focused integration/unit/component tests cover template governance, run profile separation, tool requirements, writeback, and runtime health.
- Final completed-stage bundle validator passes or records a concrete blocker with proof.
