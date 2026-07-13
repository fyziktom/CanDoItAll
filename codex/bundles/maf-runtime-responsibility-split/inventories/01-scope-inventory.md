# Scope Inventory

## Source Files

| Area | File | Lines | Planned disposition |
| --- | --- | ---: | --- |
| Main runtime | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | 3,436 | Reduce to orchestration plus public runtime entrypoints. |
| Agent factory | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | 1,912 | Inspect for collaborator reuse; avoid expanding unless needed. |
| Session partial | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` | 576 | Extract to session builder/collaborator. |
| Model parameters partial | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.ModelParameters.cs` | 177 | Extract to model parameters builder. |
| Context manifest partial | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.ContextManifest.cs` | 105 | Extract to context manifest builder. |
| Input attachments | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.InputAttachments.cs` | 205 | Keep compatible with session serialization and attachment support tests. |
| Provider health partial | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafAgentRuntime.ProviderHealth.cs` | 58 | No direct change unless orchestration wiring requires it. |
| Shared kernel | `repo://src/Foundation/CanDoItAll.SharedKernel` | N/A | Candidate for stable hash helper. |
| Process hashers | `repo://src/Processes/CanDoItAll.Processes.Builder/ProcessPlanHasher.cs`, `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplateHashing.cs` | N/A | Review before adding shared hash helper to avoid duplicate or incompatible APIs. |

## Test Files

| Test surface | References | Required future use |
| --- | --- | --- |
| MAF runtime unit tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntime*.cs` | Expand for helpers/builders and preserve existing behavior. |
| Finalizer policy tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs` | Keep and add driver-focused tests around invocation capture/selection/recovery boundaries. |
| Execution integration tests | `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs` | Preserve run tracking, required finalizer, usage, transcript, and context manifest behavior. |
| Recovery integration tests | `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRecoveryIntegrationTests.cs` | Preserve session repair and failed-run recovery behavior. |
| Capability filtering integration tests | `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs` | Ensure runtime collaborator wiring does not change capability exposure. |
| Playwright agent tests | `repo://tests/Playwright/CanDoItAll.Tests.Playwright/AiAgentFlowTests.cs`, `repo://tests/Playwright/CanDoItAll.Tests.Playwright/AgentCapabilitySetupFlowPlaywrightTests.cs` | UI validation for agent chat and capability setup. |
| Playwright shell tests | `repo://tests/Playwright/CanDoItAll.Tests.Playwright/WorkflowShellSmokeTests.cs`, `repo://tests/Playwright/CanDoItAll.Tests.Playwright/ProcessShellSmokeTests.cs` | UI validation for workflow and process runtime screens. |

## Static Scan Targets

- `MafAgentRuntime.cs` should shrink materially. SB07 must set the exact threshold after SB01 inventory, but the initial target is below 1,500 lines unless implementation documents a stronger reason.
- No new MAF helper or builder file should exceed 700 lines without a documented split.
- Search for `ComputeStableHash`, `FormatArgumentValue`, and `partial class MafAgentRuntime` after extraction. Remaining occurrences must be justified.
- Search for `TODO`, `NotImplementedException`, fixture-only branching, and empty stub methods in new runtime collaborators.
