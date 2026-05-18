# 16 Prediction Error Salience Signals

## Status

- Completed
- Completion detail: Passed on 2026-05-16 after `01b-score-geometry-driver`, `15-cognitive-workspace-attention-router`, and `14-neuro-foundation-claim-evidence-ledger`.
- Critical foundation for recall activation, replay, probing, Epistemic Drive, and calibration.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
Add prediction expectation/error records and a durable multi-dimensional cognitive signal ledger without collapsing learning evidence into scalar priority.

## Covered Inputs

- Neuro patch FR-045, FR-046 and NFR-027, NFR-033.
- Patch findings C-04, H-04, and M-04.
- Existing v2 activation, probing, consolidation, Epistemic Drive, and confidence calibration design.

## Prerequisites

- `14-neuro-foundation-claim-evidence-ledger` provides evidence anchors, claims, and context frames.
- `15-cognitive-workspace-attention-router` provides workspace and attention decision ids.
- `01b-score-geometry-driver` provides salience signal, prediction-error severity, and activation score spaces.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\19-prediction-error-salience-signal-ledger.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\14-epistemic-drive-and-learning-orchestration.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\16-probing-regression-and-calibration-loop.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\EpistemicDriveContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Prediction expectation records.
- Prediction error records.
- Cognitive signal records and publication/query services.
- Signal consumption rules for activation, replay, probing, Epistemic Drive, procedure maturity, and answer-gate calibration.
- Vector/schema metadata requirements for salience and `KnowledgeNeedVector`.
- Score geometry integration for signal vectors, activation vectors, and consumer-specific scalar projections.

## Dependency Impact

- Recall activation can consume signals but cannot treat signal score as truth.
- Consolidation consumes prediction errors and signals as evidence.
- Replay scheduler prioritizes jobs from signal vectors.
- Probing publishes prediction errors and calibration-risk signals.
- Epistemic Drive consumes signals as evidence contributors.

## Validation Depth

- Unit tests for expectation/error classification and signal vector preservation.
- Unit tests proving signal magnitude/display priority is derived from score geometry.
- Integration tests for probe feedback, workflow failure, stale source, user correction, and confirmed useful procedure publishing expected signals.
- Negative tests proving salience cannot bypass source truth, access policy, or review policy.
- EF query/index tests for signal and prediction-error lists.
- Performance scan for signal publication/query hot paths.

## Implementation Steps

1. Add prediction expectation/error and signal entities/configurations.
2. Add signal publication/query services and deterministic test fixtures.
3. Add evidence anchor, actor, algorithm/profile version, and timestamp requirements.
4. Add integration seams for recall activation, consolidation, probing, replay, Epistemic Drive, and answer gate.
5. Add tests proving no scalar-only salience.

## Scope Exceptions

- Do not implement replay scheduling, probing feedback, or Epistemic Drive proposal generation here.
- Do not tune final activation weights without later profiling and regression evidence.
- Do not create local salience or activation score formulas outside score geometry.

## Do Not Do

- Do not collapse salience into one authoritative score.
- Do not let high salience create truth, approve memory changes, or bypass access policy.
- Do not store signals only as JSON metadata.
- Do not publish anonymous signals without actor/evidence/version traceability.

## Acceptance Checklist

- Prediction errors capture expected vs observed mismatch.
- Signal records preserve dimensions and evidence.
- Signal consumers are listed and bounded by policy.
- `KnowledgeNeedVector` has schema/version/normalization/evidence metadata.
- Wrong-scope Docker fixture can publish context-separation signal.

## Proof Required

- Build/test output.
- EF model/index proof.
- Signal vector preservation tests.
- Negative policy tests.
- Implementation report with deviations.

## Implementation Result

- Added typed prediction expectation/error and cognitive signal contracts, ids, signal source/consumer enums, suggested action enums, request/query/result DTOs, and ledger/engine interfaces.
- Added durable prediction expectation/error, signal, evidence-anchor link, error-signal link, and signal consumer policy records with EF configurations and query indexes.
- Added `CognitiveMemorySignalLedger`, implementing signal publication/query and prediction expectation/error observation. Publication requires actor, policy profile, evidence anchors, score components, and consumer policy. Signal consumer policies always record `CanCreateTruthDirectly = false`.
- Added `PredictionErrorSeverity` score space and expanded the salience signal score space with context-separation/wrong-scope dimensions. Prediction error severity uses score geometry-derived display severity; salience signals persist dimensions and score traces without scalar-only priority.
- Registered `ICognitiveMemorySignalLedger` and `ICognitiveMemoryPredictionErrorEngine` in module DI.
- Added SQLite/PostgreSQL migrations:
  - `src/CanDoItAll.Migrations.Sqlite/Migrations/20260516200140_AddCognitiveMemoryPredictionSignals.cs`
  - `src/CanDoItAll.Migrations.PostgreSql/Migrations/20260516200210_AddCognitiveMemoryPredictionSignals.cs`
- Added unit tests in `tests/CanDoItAll.Tests.Unit/CognitiveMemorySignalLedgerTests.cs` and integration tests in `tests/CanDoItAll.Tests.Integration/CognitiveMemorySignalPersistenceModelTests.cs`.

## Closure Proof

- `dotnet build .\src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj --no-restore` passed with zero warnings.
- `dotnet build .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore` passed with zero warnings.
- `dotnet build .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore` passed with zero warnings.
- `dotnet build .\src\CanDoItAll.Migrations.Sqlite\CanDoItAll.Migrations.Sqlite.csproj --no-restore` passed with zero warnings.
- `dotnet build .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --no-restore` passed with zero warnings.
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~CognitiveMemorySignalLedgerTests"` passed 4/4.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~CognitiveMemorySignalPersistenceModelTests"` passed 2/2.
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter "FullyQualifiedName~CognitiveMemory"` passed 59/59.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~CognitiveMemory"` passed 16/16.
- SQLite and PostgreSQL `dotnet ef migrations has-pending-model-changes` reported no model changes.
- Static production grep found no direct authoritative upsert/direct write/final scalar score/score-breakdown dictionary/signal-priority/salience-priority surfaces in `src/CanDoItAll.Modules.CognitiveMemory`.
- Hot-path performance scan over `src/CanDoItAll.Modules.CognitiveMemory/Signals` found no critical findings: 0 `IndexOf`, 0 `Substring`, 0 casing conversions, 0 `Replace`, 0 `params`, 0 regex, and 17/17 classes sealed. LINQ hits are bounded EF query shaping or small component/evidence shaping.
- `dotnet build .\CanDoItAll.slnx --no-restore` passed with zero warnings.

## Deviations

- No UI/browser proof was run because this subbundle is backend foundation only and no UI files changed.
- Replay scheduling, probing workflows, Epistemic Drive proposal generation, answer gate behavior, and final activation tuning remain intentionally gated to later subbundles.
- Salience signals do not persist a local scalar priority. Any future display magnitude must come from score geometry trace/projection; in this phase salience score space remains dimensional and trace-backed.

## Browser Validation Logging

- N/A for backend foundation.
- Browser proof is required later in dashboards/workbenches that expose signals, prediction errors, or answer-gate warnings.

## Progression Gate

- Do not proceed to recall, consolidation, replay, probing, Epistemic Drive, or answer gate phases until signals and prediction errors are durable, dimensional, and policy-safe.
- Reopen this subbundle if downstream code invents local scalar signal state.

## Suggested Agent Prompt

Implement Prediction Error and Salience Signal Ledger as auditable, dimensional evidence. Preserve source truth and policy boundaries; signals influence prioritization and calibration but never create truth directly.
