# Phase plan

## Subbundle dependency map

```mermaid
graph TD
  SB01[SB01 Baseline and code-first closure] --> SB02[SB02 Business PG automation reconciliation]
  SB02 --> SB03[SB03 Runtime-host operator readback]
  SB03 --> SB04[SB04 Project-structure multi-team UI E2E]
  SB04 --> SB05[SB05 Live OpenAI template smoke]
  SB05 --> SB06[SB06 Scheduler/workflow launch and verification jobs]
  SB06 --> SB07[SB07 Representative regression matrix]
  SB07 --> SB08[SB08 Release decision and red-team closure]
```

## Critical subbundles
All subbundles are critical. They are larger implementation areas, not micro-edits.

## Code-first gate
SB08 must run:
`git diff --numstat <explicit-start-sha>...HEAD`

Group changed lines:
- `src/`
- `tests/`
- `docs/`
- `codex/bundles/`

Final closure requires:
- `(src + tests changed lines) >= 5 × codex/bundles changed lines`
- docs are reported separately and do not count as implementation
- no generated per-SB proof trees beyond concise transcripts and execution report updates

## Browser validation
Large desktop only. Required for SB03 if UI route/component changes and SB04 project/project-structure launch. No mobile/small/medium proof.
