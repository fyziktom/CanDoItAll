# SB030 Semantic Invariants

## Invariants

- Invariant ID: `SB030-INV-001`
- Source raw note: `Prove contract map is docs/tests only and production source unchanged except tests/guards.`
- Expected behavior: Core rehearsal remains documentation/test-only, active guards target this bundle, and production source contains no Core project or process-driver runtime API.
- Disallowed shallow implementation: Checking an older bundle, adding production interface/DI examples to docs, creating a production Core project, or collapsing SB028/SB029 proof rows.
- Failing-first test: `N/A - docs/tests-only rehearsal; no production behavior change was intended.`
- Passing test: `bundle://proof/SB030/transcripts/core-rehearsal-architecture-test.txt`
- Changed source files: `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/architecture/04-core-candidate-contract-map.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/architecture/05-future-core-allow-deny-list.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/analysis/04-static-wrapper-inventory.md`, `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/inventories/01-core-candidate-inventory.md`, `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB030/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: Creating `src/CanDoItAll.Processes.Core`, adding `public interface` or `AddScoped` examples to the Core rehearsal docs, or deleting separate SB028/SB029 rows fails SB030 proof.
- Downstream dependency check: `SB031` may start driver evidence vocabulary documentation because Core rehearsal stayed docs/tests only.

## Raw Note Closure

- Core rehearsal closure: `Solved for SB030 with build, active-bundle architecture guard, and source proof.`
- No production Core: `Solved for Gate J; final Core decision remains owned by SB036.`
- Do not rush Process Core: `Partially solved without creating Core; final decision remains owned by SB036.`
