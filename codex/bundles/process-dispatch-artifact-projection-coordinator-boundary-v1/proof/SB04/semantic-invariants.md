# SB04 Semantic Invariants

- Invariant ID: `SB04-INV-001`
- Source raw note: Continue smaller dispatcher isolation
- Expected behavior: The projection facade keeps the source-family order and the source scan proves no Core, driver, UI, or viewport-proof drift.
- Disallowed shallow implementation: A shallow pass would move text without proving source order or forbidden boundary tokens.
- Failing-first test: N/A - process/non-production refactor proof; source scans cover forbidden boundary drift and order regression.
- Passing test: `bundle://proof/shared/transcripts/unit-projection-tests.md` and `bundle://proof/shared/transcripts/integration-artifact-projection-tests.md` both exit 0 for projection filters.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionCoordinators.cs`, and `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`.
- Production assertions: The service facade delegates the seven projection source families in the required order and the coordinator file remains module-local under the dispatch service partial.
- Red-team negative case: `bundle://proof/shared/transcripts/source-scans.md` proves no Core, driver API, UI-file, or forbidden proof viewport drift for this bundle.
- Downstream dependency check: `bundle://proof/shared/transcripts/build.md` and focused projection transcripts prove downstream projection tests still pass after the boundary extraction.
