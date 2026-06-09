# SB030 - Gate J: run detail/recovery large desktop proof

## Status
- Completed

## Objective
Gate J: run detail/recovery large desktop proof.

## Covered Inputs
- User wants real code review and real process runtime restoration.
- Current previous bundle is incomplete after SB012.
- Processes must run from UI/project/API/scheduler/workflow-origin paths, not from driver runtime hooks.

## Prerequisites
- Previous subbundle and phase gate must be completed.
- No downstream work may rely on report-only proof.

## Exact Source References
- `repo://codex/bundles/process-runtime-live-e2e-openai-hardening-v1/reviews/01-execution-report.md`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://tests/CanDoItAll.Tests.Integration`
- `repo://tests/CanDoItAll.Tests.Playwright`

## Deliverables
- Source changes or source-backed proof for this subbundle objective.
- Tests and scans appropriate to the changed surface.
- Updated execution report row.

## Dependency Impact
- If this subbundle is wrong, downstream phases may claim runtime restoration without actual process execution proof.

## Validation Depth
- Critical foundation gate with semantic adequacy proof, manifests, changed-file hashes, transcripts, positive and negative proof.

## Implementation Steps
1. Re-read exact sources before editing.
2. Implement the smallest safe change that closes the objective.
3. Add or update focused tests.
4. Run targeted tests.
5. Update proof artifacts and execution report.
6. Do not proceed if source scans fail.

## Scope Exceptions
- Do not introduce runtime driver host or execution-capable drivers.
- Do not broaden UI validation to small/medium/mobile.
- Live OpenAI proof is opt-in and may be skipped only with explicit reason.

## Do Not Do
- Do not use transient bundle paths from `src` or `tests`.
- Do not mutate process state through driver packages.
- Do not add registry/selector/DI auto-registration/manager command.
- Do not replace runtime proof with docs-only proof.

## Acceptance Checklist
- [x] Objective closed.
- [x] Tests pass.
- [x] Source scan passes.
- [x] No forbidden runtime host or driver mutation surface.
- [x] Execution report updated.
- [x] Critical manifest and semantic invariants completed.

## Completion Evidence
- Critical manifest: `bundle://proof/SB030/manifest.md`
- Semantic invariants: `bundle://proof/SB030/semantic-invariants.md`
- Test transcript: `bundle://proof/SB030/transcripts/run-detail-recovery-ui-test.txt`
- TRX: `bundle://proof/SB030/SB030-run-detail-recovery-ui.trx`
- Browser screenshots: `bundle://proof/SB030/screenshots/01-selected-run-summary-large-desktop.png`, `bundle://proof/SB030/screenshots/02-step-recovery-diagnostics-large-desktop.png`, `bundle://proof/SB030/screenshots/03-artifact-ledger-large-desktop.png`
- Red-team negative proof: `bundle://proof/SB030/red-team/shallow-ui-only-proof-rejected.md`

## Proof Required
- Build/test transcript paths.
- Source assertions.
- Anti-stub audit.
- No transient bundle-path scan.
- Semantic adequacy gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit and raw-note closure.

## Browser Validation Logging
- Large desktop Playwright proof required. Record route, viewport, actions, assertions and screenshot paths.

## Progression Gate
- Must pass before the next phase may start.

## Suggested Agent Prompt
Implement SB030: Gate J: run detail/recovery large desktop proof. Preserve all hard constraints and produce source-backed proof.
