# Source Artifacts

- `repo://codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/reviews/01-execution-report.md`
- `repo://codex/bundles/process-core-contract-candidate-driver-readiness-prep-v1/architecture/07-core-extraction-readiness-scorecard.md`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Artifact Use

- Previous-bundle proof is treated as input only; active source scans and tests must be rerun in this bundle.
- Process dispatch source is the implementation surface for runtime/service refactoring.
- Architecture and focused integration tests are the first guardrail surface for forbidden Core, driver, and UI drift.
