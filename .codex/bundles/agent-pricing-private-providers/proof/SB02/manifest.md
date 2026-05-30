# SB02 Proof Manifest

## Status

- `Completed`

## Semantic Adequacy

- Agent run metrics now store cached input token counts and calculated USD cost: `repo://src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`.
- Execution run metric creation calculates cost from provider/model pricing through the shared pricing calculator: `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`.
- Process run actual cost synchronizes from execution metrics after finalized step transitions: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`.
- Live process observation prefers token-usage cost when metrics are available: `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs`.
- Workflow LLM component usage carries calculated pricing into workflow event payloads: `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`, and `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`.
- Changed-file SHA-256: `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` `7BE400E4C48BA2F69CE6952ADDBF3645A29D5AB59E00F9CAD5AA4D8831323A0A`.
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB02/transcripts/passing-tests.md`.
- Anti-stub transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.md`.
- Failing-first: N/A process exemption; the cost propagation change was implemented with focused pricing math coverage and source-backed process/workflow proof rather than a pre-existing failing executable test in this bundle.

## Validation

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter ProviderPricingTests -v minimal` passed with 4 tests, 0 failed, 0 skipped.
- `dotnet build CanDoItAll.slnx --no-restore -v minimal -clp:Summary` passed with 0 errors.
