# 32 Test Suite Rebalance With Mock Providers

## Status

- `Completed`

## Objective

- Rebalance tests into generic memory, driver contract, source gateway, MAF generic integration, native service, UI, and architecture guard suites.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R19
- R02
- R03

## Prerequisites

- SB31 completed

## Exact Source References

- `repo://tests/Support/CanDoItAll.Tests.Support/CognitiveMemory/CognitiveMemoryFakes.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/CognitiveMemoryModuleRegistrationTests.cs`
- `bundle://architecture/07-testing-and-mocking-strategy.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Rebalance tests into generic memory unit tests, driver contract tests, Source Gateway tests, MAF generic integration tests, UI/component tests, native service tests, migration tests, and architecture guard tests.
- Replace old native-only fakes with mock providers that implement the generic provider protocol and deterministic async/event/feedback behavior.
- Retire or rewrite tests that accidentally enforce native module coupling in base app startup.
- Add tests for zero-provider, two-provider, provider selection, source ingestion, delayed feedback, event loop guard, and native remote provider.
- Add test naming and fixture documentation so future subbundles do not copy old bad patterns.
- Add tests for current MAF registration paths and source snapshot contract compatibility, not only generic memory DTO behavior.
- Ensure mock providers are explicit test/provider profiles and never the implicit fallback for empty provider configuration.

## Dependency Impact

- Final e2e proof depends on reliable generic and native test suites.

## Validation Depth

- `Test foundation`

## Implementation Steps

1. Inventory existing `CognitiveMemory*` tests and classify them as generic, native, migration compatibility, UI, or retire/rewrite.
2. Build mock provider fixtures for sync context, async accepted operation, delayed completion, events, source request, feedback request, failure, timeout, and capability mismatch.
3. Move/rewrite tests to target the correct layer and remove tests that require native memory in base startup.
4. Add architecture guard tests for all non-negotiable dependency rules.
5. Run focused suites and record remaining skipped/deferred tests with owners and reasons.
6. Add negative tests proving zero-provider paths do not call native Cognitive Memory, Qdrant, OpenAI, or mock provider fixtures.

## Scope Exceptions

- No scope exceptions.

## Completion Notes

- Added `repo://tests/Memory/CanDoItAll.Memory.Tests/GenericMockMemoryProviderFixture.cs` as the generic test-only provider fixture for immediate context, accepted async operations, status polling, feedback delivery, provider events, outbox delivery, health, and UI surface metadata.
- Added `repo://tests/Memory/CanDoItAll.Memory.Tests/MemoryTestSuiteRebalanceCheckpointTests.cs` to prove explicit mock profiles, two-provider role selection, immediate runtime dispatch, delayed operation completion, delayed feedback delivery, provider event dedupe/outbox drain, generic test/native dependency boundaries, and test inventory documentation.
- Added `repo://docs/cognitive-memory/operations/memory-test-suite-rebalance.md` and linked it from the Cognitive Memory docs to classify generic tests, retained legacy native tests, component/browser tests, and ownership of `CognitiveMemoryModuleRegistrationTests.cs` and `CognitiveMemoryFakes.cs`.
- Validation proof is captured in `bundle://proof/SB32/manifest.md` and `bundle://proof/SB32/semantic-invariants.md`.

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
- The test suite proves generic provider behavior without native Cognitive Memory installed.
- Native behavior is tested in the native repo/service layer, not by base app coupling.
- Architecture guard tests catch forbidden references, Qdrant base dependency, and duplicate dispatch paths.
- Tests fail if source snapshot contracts are forked incompatibly or MAF memory uses parallel registration paths.

## Proof Required

- Create `proof/SB32/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run unit, integration, component, and architecture guard test suites relevant to memory migration.
- Capture a test inventory diff showing which old tests were moved, rewritten, retired, or kept as native tests.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB32 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB32 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
