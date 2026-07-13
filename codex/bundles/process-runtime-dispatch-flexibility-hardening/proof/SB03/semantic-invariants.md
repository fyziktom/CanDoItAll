# SB03 Semantic Invariants

- Invariant ID: SB03-PROMPT-DRIVER
- Source raw note: N003, N005, N007
- Expected behavior: Process step brief and prompt fragment composition can be replaced per driver/model without editing the generic dispatcher.
- Disallowed shallow implementation: Leaving prompt assembly as private helper methods that cannot be mocked or selected through a driver contract.
- Failing-first test: N/A - process refactor preserves production behavior; adversarial negative proof is represented by boundary tests and dependency scans rather than an intentionally committed failing test transcript.
- Passing test: build transcript bundle://proof/SB07/transcripts/build-modules-processes.txt; focused runtime test transcript bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt; integration/e2e transcript bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt.
- Changed source files: repo://src/Processes/CanDoItAll.Processes.Application/ProcessPromptCompositionContracts.cs; repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverExecutionContracts.cs; repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs.
- Production assertions: DriverProcessStepBriefBuilder selects IProcessPromptCompositionDriver implementations and a fake-driver unit test proves replacement.
- Red-team negative case: A prompt path that hard-codes software-delivery instructions into generic enterprise prompts fails prompt-focused tests.
- Downstream dependency check: SB05 launch-context isolation and SB07 generic enterprise prompt proof depend on the driver prompt boundary.
