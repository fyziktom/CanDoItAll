# SB11 Semantic Invariants

- Invariant ID: `SB11_INV_001`
- Source raw note: RN-001 and RN-003.
- Expected behavior: Database requirement, upstream materialization, and stranded recovery are classified as explicit pre-execution route decisions while their existing side-effect coordinators remain in `DispatchAsync`.
- Disallowed shallow implementation: A planner that executes EF writes, transitions, recovery runs, finalizer calls, logging, or service calls.
- Failing-first test: N/A - non-critical planner foundation; focused route tests and anti-side-effect source scan prove the boundary.
- Passing test: `bundle://proof/SB11/transcripts/sb11-route-planner-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`.
- Production assertions: `bundle://proof/SB11/source-assertions/pre-execution-route-planner.md`.
- Red-team negative case: `bundle://proof/SB11/transcripts/sb11-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB12 can gate route parity by checking planner route tokens and absence of side-effect APIs.

- Invariant ID: `SB11_INV_002`
- Source raw note: RN-001, RN-003, and RN-004.
- Expected behavior: Subprocess, workflow-handled, and direct agent execution routes preserve existing dispatcher order and remain runtime/service-only.
- Disallowed shallow implementation: Routing subprocesses or workflows through direct agent execution, or treating an unhandled workflow outcome as completed dispatch.
- Failing-first test: N/A - non-critical planner foundation; subprocess/workflow/agent route classification tests prove behavior.
- Passing test: `bundle://proof/SB11/transcripts/sb11-route-planner-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `bundle://proof/SB11/source-assertions/pre-execution-route-planner.md`.
- Red-team negative case: `bundle://proof/SB11/transcripts/sb11-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB12 route parity and line-count gate must keep route planning decision-only.
