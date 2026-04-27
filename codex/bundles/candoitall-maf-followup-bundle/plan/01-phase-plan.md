# Phase Plan

This plan extends the prepared bundle with the execution tracking required by the bundle execution workflow. The existing `requirements/requirements.md`, `audit/current-state-audit.md`, and subbundle READMEs remain the source of truth for implementation details.

## Execution Order

| Phase | Subbundle | Status | Critical foundation | Requirements |
|---|---|---|---:|---|
| 01 | `01-finalizer-runtime-mode-alignment` | Completed | Yes | R01, R02 |
| 02 | `02-tool-policy-exception-boundary` | Completed | Yes | R03 |
| 03 | `03-provider-feature-consistency` | Completed | Yes | R04, R05 |
| 04 | `04-hardening-test-suite-reconciliation` | Completed | Yes | R06, R07, R12 |
| 05 | `05-repair-service-contract` | Completed | No | R08 |
| 06 | `06-process-context-output-validation` | Completed | Yes | R09 |
| 07 | `07-tool-composition-approval-failfast` | Completed | No | R10 |
| 08 | `08-workflow-checkpoint-claims-and-roadmap` | Completed | No | R11 |
| 09 | `09-verification-document-truthfulness` | Completed | Yes | R06, R12 |

## Dependency Map

```mermaid
flowchart TD
    S01["01 Finalizer mode alignment"] --> S04["04 Hardening tests"]
    S02["02 Tool policy exception boundary"] --> S04
    S03["03 Provider feature consistency"] --> S04
    S04 --> S05["05 Repair service contract"]
    S04 --> S06["06 Process context validation"]
    S04 --> S07["07 Tool composition approval fail-fast"]
    S05 --> S09["09 Verification document truthfulness"]
    S06 --> S09
    S07 --> S09
    S08["08 Workflow checkpoint claims and roadmap"] --> S09
```

## Gate Rules

- Phase 01 must pass finalizer-mode proof before any verification document can claim hardening coverage.
- Phase 02 must pass policy-exception proof before tool-composition validation can rely on block diagnostics.
- Phase 03 must pass provider-matrix and transport round-trip proof before approval/tool capability decisions are trusted.
- Phase 04 must prove that the focused hardening tests exist and are discoverable.
- Phase 06 must prove process-context validation without introducing markdown/text fallbacks.
- Phase 09 is the final documentation and release-proof phase and cannot close until all prior applicable proof is recorded.
