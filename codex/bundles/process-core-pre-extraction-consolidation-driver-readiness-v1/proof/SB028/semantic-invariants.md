# SB028 Semantic Invariants

## Invariants

- Invariant ID: `SB028-INV-001`
- Source raw note: `Create bundle-only contract map for first future Core project; no production Core project.`
- Expected behavior: The active bundle owns a docs/test-only Core candidate map that identifies possible pure future Core surfaces and denied application/infrastructure/compatibility dependencies without creating production code.
- Disallowed shallow implementation: Creating `CanDoItAll.Processes.Core`, defining public Core interfaces, adding DI/registration examples, or treating EF, storage, workspace, transition, AgentFramework, claim, finalizer, or adapter side effects as Core candidates.
- Failing-first test: `N/A - documentation/test-only rehearsal; no production behavior change was intended.`
- Passing test: `bundle://proof/SB028/transcripts/core-contract-map-source-assertions.txt`
- Changed source files: `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/architecture/04-core-candidate-contract-map.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/inventories/01-core-candidate-inventory.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/analysis/04-static-wrapper-inventory.md`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB028/transcripts/core-contract-map-source-assertions.txt`
- Red-team negative case: Adding a production Core project, public interface, DI registration, or EF/transition/storage dependency to the candidate map fails SB028 proof.
- Downstream dependency check: `SB029` may validate the allow/deny architecture guard because the Core contract map exists and is docs/tests only.

## Raw Note Closure

- Draft test-only Core candidate contract map: `Solved for SB028 with documentation-only contract map.`
- No production Core: `Partially solved with source assertions; SB030 owns critical closure.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
