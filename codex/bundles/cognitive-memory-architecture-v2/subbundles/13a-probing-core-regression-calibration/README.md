# 13a Probing Core Regression Calibration

## Status

- Ready after score geometry, recall traces, consolidation evidence intake, review records, and MAF/tool boundaries are available.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
- Implement the non-UI probing core before the Dialogue Workbench: durable probe sessions, turns, feedback evidence, correction gating, regression tests, and confidence calibration.

## Covered Inputs

- Requirements FR-032, FR-033, FR-034, FR-036, FR-037, FR-038, NFR-020, NFR-021, NFR-022, and NFR-023.
- `architecture/15-interactive-memory-probing.md`.
- `architecture/16-probing-regression-and-calibration-loop.md`.
- `contracts/csharp/InteractiveMemoryProbingContracts.cs`.

## Prerequisites

- `05-recall-orchestrator` must persist trace evidence with selected/excluded candidates and budget/access decisions.
- `06-consolidation-engine` must expose an evidence intake path that can later consume probe findings.
- `08-human-review-ui` or its backend services must provide review item creation and decision state.
- `01a-common-drivers-helpers-and-ef-guardrails` must be closed so probe state, evaluator profiles, evidence kinds, and status fields are strongly typed.
- `01b-score-geometry-driver` must provide probe assessment, calibration-risk, and regression-value score spaces.
- `14-neuro-foundation-claim-evidence-ledger` must provide claim-level correction candidates, evidence anchors, context frames, and mutation authority.
- `15-cognitive-workspace-attention-router` must provide probe workspace frames.
- `16-prediction-error-salience-signals` must provide prediction error and signal publication.
- `17-temporal-replay-scheduler` must provide replay job semantics for probe regression replay.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\15-interactive-memory-probing.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\16-probing-regression-and-calibration-loop.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\InteractiveMemoryProbingContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\probing-test-matrix.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Context\AgentContextContributionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContextModelRegistry.cs

## Deliverables

- Probe session, turn, feedback, finding, correction, confidence calibration, regression test case, and regression test run records.
- Probe session service for start, ask, feedback, close, and state transitions.
- Feedback-to-evidence publisher that creates review, gap, contradiction, supersession, calibration, and regression artifacts without mutating active memory.
- Regression test creation and replay service using recall traces and deterministic constraints.
- Docker context-separation regression fixture for production/test/local/CI Docker behavior.

## Dependency Impact

- `13-interactive-memory-probing-workbench` depends on this subbundle for backend state and service contracts.
- `12-epistemic-drive-engine` consumes published probe evidence and calibration signals from this subbundle.
- Recall and taxonomy may need reopening if regression tests expose wrong-scope or overconfident retrieval behavior.

## Validation Depth

- Unit tests for session lifecycle, feedback action validation, correction risk classification, calibration classification, and regression constraint evaluation.
- Integration tests for manual question -> recall trace -> probe turn -> feedback -> review/regression/evidence artifacts.
- Negative tests proving direct canonical memory mutation is impossible from probe feedback.
- Replay tests for the Docker context-separation fixture.
- EF tests for indexes on project/session/state/created timestamps, trace ids, review links, and regression test state.
- Score geometry tests for probe assessment traces and scalar-only rejection.

## Implementation Steps

1. Add durable probe core records and EF configurations.
2. Add probe session service and feedback service.
3. Wire `AskAsync` to `IRecallOrchestrator` with trace required.
4. Add correction evidence and review handoff.
5. Add confidence calibration records and classification.
6. Add regression test case creation and replay.
7. Add Docker context-separation fixture and tests.
8. Publish probe evidence refs for later Epistemic Drive consumption.

## Do Not Do

- Do not build the Dialogue Workbench UI here.
- Do not generate broad question queues here beyond deterministic regression fixtures.
- Do not mutate active canonical memory from feedback or correction records.
- Do not use Qdrant as a required dependency for probing core tests.
- Do not store only a final score; preserve score evaluation traces, findings, evidence refs, trace ids, and calibration dimensions.

## Acceptance Checklist

- Every probe answer has a recall trace id and source/access warnings.
- Feedback creates review/regression/evidence artifacts according to risk.
- High-risk corrections remain draft/review-only.
- Regression tests replay recall and store pass/fail results linked to new traces.
- Calibration records distinguish overconfidence, missing source, wrong scope, and redaction-limited answers.
- Probe answer metadata references score evaluation traces rather than untyped score breakdowns.

## Proof Required

- Build/test proof.
- Probe core integration report.
- Created review item proof.
- Created regression test proof.
- Docker context-separation replay proof.

## Browser Validation Logging

- No browser proof is required for backend-only probing core work.
- Browser proof belongs to `13-interactive-memory-probing-workbench`.

## Progression Gate

- Proceed to the Dialogue Workbench only after backend probe flows can run end-to-end without UI and direct truth mutation is rejected by tests.
- Proceed to Epistemic Drive only after probe evidence refs can be consumed without JSON-only lookup or stringly typed adapters.

## Suggested Agent Prompt

- Implement the backend probing core, regression replay, and confidence calibration services. Keep user corrections as evidence and review artifacts only. Do not build the UI in this subbundle.
