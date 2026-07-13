# SB06 - Domain Driver Isolation For .NET And Lifecycle Behavior

## Status

- Status: `Completed`

## Objective

Remove .NET/software-delivery lifecycle and tool-plan decisions from generic adapter/runtime/MAF receipt writer code.

## Covered Inputs

- User specifically called out `IsDotNetRuntimeLifecycleTool`.
- User required domain behavior to be isolated in process drivers.
- GPTPro domain leakage findings.

## Prerequisites

- SB01 baseline complete.
- SB02 driver/tool classifier contracts complete.
- SB03 receipt pipeline available where receipt classification is moved.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptWriter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.DotNetSetupRuntime.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupRuntimeExecutor.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupToolPlanGuard.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`

## Dependency Impact

- Likely driver abstraction and composition changes.
- CodeAnalytics dependency/cycle proof required.

## Validation Depth

- Direct unit tests for generic executor catalog.
- Direct unit tests for .NET executor selection.
- Lifecycle extractor tests.
- Fake extension seam tests.
- Source assertions.

## Do Not Do

- Do not add another adapter `if dotnet` branch.
- Do not keep `IsDotNetRuntimeLifecycleTool`.
- Do not move .NET logic into generic runtime under a new name.

## Acceptance Checklist

- [ ] Adapter no longer directly references `IDotNetSolutionSetupRuntimeExecutor`.
- [ ] Receipt writer no longer hardcodes .NET lifecycle tools.
- [ ] .NET behavior is selected through driver/classifier seam.
- [ ] Fake domain driver can be added without adapter edit.

## Proof Required

- Proof manifest with source assertions.
- Direct tests.
- Composition smoke.
- Dependency proof.
- No-new-partial proof.

## Browser Validation Logging

- Not applicable except final process E2E in SB07.

## Progression Gate

- SB07 cannot pass until domain-free source assertions pass.

## Suggested Agent Prompt

Implement SB06 only. Move .NET lifecycle/tool-plan behavior behind driver/tool classifier contracts and remove generic hardcodes.

## Goal

Remove .NET/software-delivery lifecycle and tool-plan knowledge from generic adapter/runtime/MAF receipt writer code. Rehome it behind driver-owned or tool-classifier contracts.

## Scope

- Direct adapter dependency on `IDotNetSolutionSetupRuntimeExecutor`.
- `AgentFrameworkProcessExecutionAdapter.DotNetSetupRuntime.cs`.
- `DotNetSolutionSetupRuntimeExecutor`.
- `DotNetSolutionSetupToolPlanGuard`.
- `WorkspaceCommandReceiptWriter.IsDotNetRuntimeLifecycleTool`.
- .NET-specific receipt/evidence classification in adapter partials.

## Implementation Steps

1. Use SB01 characterization tests for current .NET setup and receipt writer behavior.
2. Add/complete `IProcessRuntimeOwnedStepExecutor` or equivalent driver-owned contract from SB02.
3. Move .NET setup execution selection behind driver metadata/catalog/factory.
4. Update adapter to ask generic runtime-owned executor catalog/pipeline whether a step is handled, without naming .NET.
5. Move `.NET` setup executor implementation into a domain driver/module implementation behind the contract.
6. Add `IToolReceiptLifecycleFactExtractor` or equivalent receipt writer extension seam.
7. Move `workspace_dotnet_run`/`workspace_dotnet_stop` lifecycle fact extraction into a .NET extractor implementation.
8. Update `WorkspaceCommandReceiptWriter` to call registered extractors or generic lifecycle fact provider.
9. Move adapter .NET receipt/evidence classification into domain receipt classifier/policy where possible.
10. Add direct unit tests and source assertions.
11. Run targeted tests, build, and CodeAnalytics dependency/cycle proof.

## C# Architecture Impact

This is the primary domain-leak cleanup phase. It changes ownership, not just file placement.

## Boundary Ownership

Allowed:

- .NET implementation in a process driver/domain module.
- Tool protocol constants in tool catalog.
- Generic receipt writer calling an abstraction.

Forbidden:

- Adapter checking .NET step keys.
- Generic runtime checking `.NET` tool names to make process-domain decisions.
- MAF core receipt writer containing one-off `IsDotNetRuntimeLifecycleTool`.

## Dependency Direction

The adapter/module composition may reference the concrete .NET implementation. Runtime and driver abstractions must not. `AgentFramework.Core` must not reference process module or driver implementation to get .NET lifecycle facts.

## Pattern Decision

Use Strategy selected through driver/tool classifier registration. Use Factory Method/catalog for runtime-owned step executor selection.

## Testability Contract

Required direct tests:

- Adapter handles runtime-owned execution through generic executor catalog.
- Unknown runtime-owned executor request produces explicit no-match or diagnostic without silent fallback.
- .NET executor selected by driver metadata.
- Fake domain executor can be added without editing adapter.
- Receipt writer calls lifecycle fact extractor.
- .NET lifecycle extractor preserves startup receipt and loopback URL facts.
- `WorkspaceCommandReceiptWriter` no longer contains `IsDotNetRuntimeLifecycleTool`.

## Partial Class Policy

Delete:

- `AgentFrameworkProcessExecutionAdapter.DotNetSetupRuntime.cs` if its only purpose is adapter-owned .NET execution.

Shrink:

- `ProductCompletionReceipts.cs` and `ManagedArtifactEvidence.cs` to remove .NET tool classification where moved.

No new partials.

## Architecture Proof Required

- Source assertion that adapter no longer references `IDotNetSolutionSetupRuntimeExecutor` or `.NET` step keys.
- Source assertion that `WorkspaceCommandReceiptWriter` has no `IsDotNetRuntimeLifecycleTool`.
- Source assertion for allowed/forbidden `.NET` tool-name occurrences.
- CodeAnalytics dependency/cycle proof.
- Direct unit tests and composition smoke.
- No-new-partial proof.
