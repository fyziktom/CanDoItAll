# Structured Input

## Core Objective

- Verify and harden the phase-one cognitive memory quality implementation.
- Convert code-review findings into regression tests before refactoring.
- Keep the smallest correct implementation surface while improving separation of concerns.
- Prove repeatable, idempotent, policy-safe, and failure-aware behavior.

## Success Criteria

- Follow-up bundle is prepared and passes prepared-stage validation.
- Implementation subbundles can be executed without rediscovering the code-review findings.
- Every hardening area has a concrete owner, source references, proof commands, and progression gate.

## Hard Constraints

- No direct implementation in the preparation pass.
- Do not revert the last commit wholesale; work with the added contracts and persistence unless a specific piece is proven wrong.
- Do not introduce economic memory governance, pricing, lending, attention markets, or budget-governance features.
- Do not use magic strings for durable reason codes, mode policy ids, warning codes, or command identifiers when a typed constant/enum/named options object is reasonable.
- Do not silently fall back for unsupported dream modes; reject or explicitly route them through typed mode policies.
- Keep C# code readable, strongly typed, and split by responsibility.
- Preserve existing tests and add adversarial coverage where the prior implementation only has happy-path coverage.

## Allowed Side Effects

- Add or refactor files inside `src\CanDoItAll.Modules.CognitiveMemory\Quality`.
- Update module DI registration when services are split.
- Add focused unit/integration tests under existing test projects.
- Add follow-up migrations only if schema changes are genuinely required.
- Update prior bundle closure docs if follow-up execution changes the completion claim.

## Source Artifacts

- Prior bundle and execution report.
- Last commit `228737d90acad18d96b9673949cdb5bd785f3fc6`.
- New `Quality` contracts, services, entities, configurations, migrations, and tests.
- Targeted test output captured in `inputs/01-source-artifacts.md`.

## Input Coverage Signals

- The user's distrust of the prior completion claim must remain visible through closure.
- Passing tests must be preserved but explicitly treated as insufficient.
- Code-review findings must map to requirements and subbundles.
- The no-economic-model constraint from the prior bundle remains in force.

## Dependency And Sequencing Signals

- Regression tests and audit must come first.
- Cluster ID/idempotency must be fixed before dream-run and aggregate proof can be trusted.
- Dream lifecycle/failure handling must be fixed before aggregate application and end-to-end corpus closure.
- Aggregate provenance and validation must be fixed before recall synthesis proof can be trusted.
- Refactoring should happen after tests expose current defects, or with no behavior change and targeted proof.

## Validation Expectations

- Add failing-before/fixed-after unit and integration tests for every defect-class requirement.
- Run targeted unit tests for `CognitiveMemoryQualityFoundationTests` and `CognitiveMemoryRecallOrchestratorTests`.
- Run targeted integration tests for quality persistence and consolidation persistence.
- Run all `FullyQualifiedName~CognitiveMemory` unit and integration tests before closure.
- Build `src\CanDoItAll.Modules.CognitiveMemory`, `src\CanDoItAll.Migrations.Sqlite`, and `src\CanDoItAll.Migrations.PostgreSql`.
- Run the follow-up bundle validator at prepared and completed stages.

## Evidence Contract

- Test commands and results recorded in `reviews/01-execution-report.md`.
- Any schema change has migration project build proof.
- Any unsupported scope has an explicit exception and follow-up path.
- Any final residual risk names the exact requirement or subbundle it affects.

## UI Validation Strategy

- N/A for preparation and current implementation because reviewed changes are API/domain/persistence-only.
- If implementation adds Blazor UI, the affected subbundle must add a large-screen browser pass, screenshot review questions, and narrower-width follow-up.

## Browser Validation Analytics

- N/A unless UI is added during implementation.

## Working Assumptions

- Existing `CognitiveMemoryQuality*` contracts are the intended starting point.
- Existing module registration is acceptable, but service implementations should be split if that reduces complexity without adding artificial layers.
- Browser proof is not required for the current API/domain-only changes unless a follow-up implementation adds UI.
- The implementation can use deterministic synthesis and validation components for tests even if future semantic providers are added.

## Primary Risks

- Repeat cluster planning can return transient cluster IDs when an existing cluster hash is skipped, which can break downstream dream-run FK writes.
- Dream runs save `Running` state before downstream work and do not appear to mark failed runs as failed when later persistence or validation fails.
- `PersistChanges = false` is not a true dry-run contract today because dream runs and downstream records are still persisted.
- Several explicit consolidation modes still share broad default behavior instead of a deliberate mode policy.
- Recall synthesis currently copies selected section lines into a bullet list; this is not the same as a grounded synthesized brief.
- The test suite mostly proves first-run success and does not stress repeat execution, multi-project boundaries, unsupported modes, partial failures, or policy-redaction edge cases.
