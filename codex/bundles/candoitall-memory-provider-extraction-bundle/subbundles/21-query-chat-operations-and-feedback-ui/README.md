# 21 Query Chat Operations And Feedback Ui

## Status

- `Completed`

## Objective

- Add generic query/chat UI, operation status UI, event inbox UI, feedback ledger UI, and manual delayed feedback actions.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R06
- R08
- R12

## Prerequisites

- SB20 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryMemoryTab.razor`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemoryRecallTracesTab.razor`
- `bundle://architecture/05-ui-composition.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Add generic query/chat UI, operation status UI, event inbox UI, feedback submission UI, and context-pack detail viewer.
- Support sync results, async accepted operations, polling/status refresh, cancellation, warnings, citations/source refs, feedback handles, and delayed feedback notes.
- Add manual ingestion actions from the generic UI where the source adapter supports it.
- Expose operation and feedback ledgers in a way that helps users understand what provider returned what context and how later outcomes will be correlated.
- Add UI tests for provider selection, long-running operation, feedback submission, operation failure, and expired/forgotten feedback state.
- In zero-provider mode, query/chat, ingestion, cancellation, and feedback actions must be disabled or produce typed no-provider diagnostics through the shared handler; they must not select a hidden provider.

## Dependency Impact

- User-facing provider usage and feedback visibility depend on this surface.

## Validation Depth

- `UI feature`

## Implementation Steps

1. Implement query/chat form that builds Memory Protocol requests with provider selection and structured context metadata.
2. Render context packs with sections, citations/source refs, warnings, confidence, and context pack id.
3. Implement operation status and cancellation views using the generic operation ledger.
4. Implement feedback UI for immediate feedback and planned delayed outcome correlation.
5. Add Playwright/component proof for sync response, async status, failure state, and feedback submission.
6. Add Playwright/component proof for zero-provider action states and diagnostics.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- The UI can query a mock provider and display a context pack with feedback handle and source references.
- The UI can display async accepted operation state and final completion without blocking the browser.
- Feedback submission is tied to the delivered context pack id and operation id.
- Zero-provider action behavior is explicit and does not dispatch to native Cognitive Memory, Qdrant, OpenAI, or a mock provider.

## Proof Required

- Create `proof/SB21/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run the relevant component or Playwright tests and capture large-screen plus narrow-width screenshots where layout or provider switching is visible.
- Run UI/component tests for query, async operation, cancellation, feedback, and context detail views.
- Capture browser validation analytics for provider selection, status transition, and feedback submission.

## Browser Validation Logging

- Record route, viewport, Playwright actions, assertions, screenshot paths, and screenshot review questions in `reviews/01-execution-report.md`.

## Progression Gate

- Downstream subbundles may start only after SB21 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB21 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
