# Current State

## Runtime Size And Responsibility Concentration

| File | Lines | Current responsibility signal |
| --- | ---: | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | 3,436 | Primary runtime orchestration plus finalizer repair/recovery, session persistence helpers, provider usage, approval cache, repeated-tool guard, argument formatting, stable hashing, JSON conversion, and failure diagnostics. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | 1,912 | Agent creation and related runtime wiring. Must be checked for duplicate builder opportunities after the main split. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | 1,121 | Existing partial split still mixes capability composition and configuration; this supports the user's point that partial classes alone are insufficient. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs` | 1,048 | Adjacent large runtime plugin surface. Out of scope unless implementation proves it must change for runtime extraction. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` | 576 | Session restore/create, prompt input messages, streaming snapshotting, run options, response format application, and history-mode decisions. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.ModelParameters.cs` | 177 | Model-compatible `ChatOptions`, temperature policy, reasoning effort mapping, model resolution, and retry messages. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.ContextManifest.cs` | 105 | Context assembly manifest creation and token estimates. |

## Main Runtime Method Clusters

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs:60` starts the public run path; `:112`, `:264`, and `:352` contain core run and approval execution.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs:893` through `:1842` contains required-finalizer repair, JSON repair, finalizer output normalization, finalizer invocation selection, streamed finalizer capture, and finalizer payload serialization.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs:1861` defines `StreamedFinalizerInvocationRecorder` inside the runtime.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs:1919` through `:2672` contains early-finalizer short-circuit, provider-failure finalizer recovery, process artifact recovery, required-finalizer response building, and usage observation wiring.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs:2804` through `:3047` contains session serialization skip logic and request-scoped attachment stripping.
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs:3054` through `:3428` contains approval message creation, response text resolution, background continuation, runtime session key resolution, repeated-tool guard, argument summaries, `FormatArgumentValue`, `ComputeStableHash`, and JSON argument conversion.

## Candidate Shared Helper State

- `repo://src/Foundation/CanDoItAll.SharedKernel` has lightweight common/file/time primitives and no external package references. It is a plausible target for reusable stable text hashing if adding `System.Security.Cryptography` and `System.Text` dependencies remains acceptable.
- `repo://src/Processes/CanDoItAll.Processes.Builder/ProcessPlanHasher.cs` already exposes `ComputeContentHash(string content)` returning `sha256:<lowercase-hex>`.
- `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateHashing.cs` already computes canonical JSON hashes using `SHA256`.
- `MafAgentRuntime.ComputeStableHash` currently truncates argument summaries using a short SHA-256 prefix. The implementation phase must decide whether the shared helper should return a full `sha256:` value, short display hash, or both as separate strongly named methods.

## Existing Tests To Preserve

- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeImageAnalysisModelTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeProviderHealthTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolInvocationResultTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRecoveryIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceExecutionEvidenceIntegrationTests.cs`

## UI And Host Proof Surfaces

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor` declares `@page "/agents"`.
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor` declares `@page "/agents/workflows"`.
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/AiAgentFlowTests.cs` already navigates `/agents?tab=agents`.
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/AgentCapabilitySetupFlowPlaywrightTests.cs` already navigates `/agents?tab=capabilities&agentId=...`.
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs` covers `/agents/workflows`.
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/ProcessShellSmokeTests.cs` covers process shell behavior that can be affected by finalizer and process-run runtime responses.
