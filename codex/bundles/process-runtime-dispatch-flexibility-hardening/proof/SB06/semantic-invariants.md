# SB06 Semantic Invariants

- Invariant ID: SB06-DISPATCH-DRIVER-ROUTER
- Source raw note: N001, N007
- Expected behavior: Runtime dispatch delegates step execution and branch signaling through replaceable ports instead of hard-coded concrete services.
- Disallowed shallow implementation: Adding a driver interface but continuing to instantiate or call the concrete adapter/branch service inside dispatch strategy code.
- Failing-first test: N/A - process refactor preserves production behavior; adversarial negative proof is represented by boundary tests and dependency scans rather than an intentionally committed failing test transcript.
- Passing test: build transcript bundle://proof/SB07/transcripts/build-modules-processes.txt; focused runtime test transcript bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt; integration/e2e transcript bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt.
- Changed source files: repo://src/Processes/CanDoItAll.Processes.Application/ProcessPromptCompositionContracts.cs; repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverExecutionContracts.cs; repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs.
- Production assertions: StandardProcessAdapterStrategyFactory consumes IProcessStepExecutionDriver and ProcessRuntimeDispatchApplicationService consumes IProcessRuntimeBranchSignalRouter.
- Red-team negative case: A fake step-execution driver not receiving the dispatch request fails the boundary tests.
- Downstream dependency check: SB07 validates driver-dispatched step execution, subprocess behavior, and architecture scans.
