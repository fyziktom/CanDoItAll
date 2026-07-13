# SB01 Semantic Invariants

- Invariant ID: SB01-DRV-BOUNDARY
- Source raw note: N001, N006, N007, N008
- Expected behavior: Generic Processes code exposes and consumes typed driver ports while AgentFramework runtime integration stays below the module boundary.
- Disallowed shallow implementation: Only renaming the old ProcessRuntimeIntegrationServices helpers or adding comments while dispatch still calls concrete adapter code directly.
- Failing-first test: N/A - process refactor preserves production behavior; adversarial negative proof is represented by boundary tests and dependency scans rather than an intentionally committed failing test transcript.
- Passing test: build transcript bundle://proof/SB07/transcripts/build-modules-processes.txt; focused runtime test transcript bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt; integration/e2e transcript bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt.
- Changed source files: repo://src/Processes/CanDoItAll.Processes.Application/ProcessPromptCompositionContracts.cs; repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverExecutionContracts.cs; repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs.
- Production assertions: Driver ports exist in Processes abstractions/application, module DI wires the AgentFramework implementation, and dependency scans show no Processes-to-MAF reference.
- Red-team negative case: A direct AgentFramework/MAF reference from src/Processes or a standard strategy bypassing IProcessStepExecutionDriver fails the audit and boundary unit tests.
- Downstream dependency check: SB02-SB06 depend on this port layout and SB07 validated the dependency direction with source and project scans.
