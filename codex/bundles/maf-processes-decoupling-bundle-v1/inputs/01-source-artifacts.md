# Source Artifacts

Source code snapshot inspected from uploaded archive:

- Archive: `/mnt/data/CanDoItAll-development (3).zip`
- Top-level extracted folder: `CanDoItAll-development`
- Commit marker in archive listing: `11867f0d91ce32e15d458c4992deefd3ca660e2e`

Bundle-skill references inspected from the same snapshot:

- `codex/skills/bundles/candoitall-bundle-preparation/SKILL.md`
- `codex/skills/bundles/candoitall-bundle-preparation/references/subbundle-contract.md`
- `codex/skills/bundles/candoitall-bundle-preparation/references/bundle-validation-rubric.md`
- `codex/skills/bundles/candoitall-bundle-validator/SKILL.md`
- `codex/skills/bundles/candoitall-subbundle-validator/SKILL.md`

Primary source files inspected:

- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Maf/README.md`
- `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService*.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`
- `tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs`

Important observed facts:

- `CanDoItAll.AgentFramework.Maf.csproj` directly references `..\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj`.
- `MafAgentRuntime.ProcessTools.cs` contains `using CanDoItAll.Modules.Processes;`.
- `MafAgentRuntime.ProcessTools.cs` is about 919 lines and contains process tool construction, access checks, template DTOs, and process tool exceptions.
- `MafAgentRuntime.Capabilities.cs` creates `ProcessToolBuilder`, stores it in `RuntimeCapabilityComposition`, and calls `AttachInternalProcessToolsAsync`.
- Process dispatcher has 33 partial files under `src/CanDoItAll.Modules.Processes/Automation/Dispatch` and about 25,511 total lines.
