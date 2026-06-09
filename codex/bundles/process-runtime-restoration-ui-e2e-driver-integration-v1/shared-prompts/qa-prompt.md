# QA / Red-Team Prompt

Reject the bundle if it proves only package tests or docs while process UI/runtime remains untested.

Reject report-only closure. Require real source references and command transcripts.

Specific traps:
- Tests still read `codex/bundles/<bundle-name>`.
- UI process-start proof creates a run through API but not visible/selectable in UI.
- Scenario tests only assert non-empty output and not process status/artifact/finalizer behavior.
- Driver verification attaches diagnostics by mutating process state.
- Business-analysis scenario uses software-development-only assumptions.
- Runtime host/registry/selector/DI sneaks in under a neutral name.
