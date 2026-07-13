# C# Current State Inventory

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260707120906-123d4de9`
- Solution: `repo://CanDoItAll.slnx`
- Scoped source projects: 9
- Scoped documents: 176
- Blocking errors: none
- Scoped dependency cycles: none reported

## Hotspots And Relevant Types

| Type or file | Evidence | Architecture concern |
| --- | --- | --- |
| `MafAgentRuntime` | CodeAnalytics reported 1470 lines | Large runtime coordinator still carries broad composition concerns. |
| `RuntimeCapabilityComposer.cs` | CodeAnalytics reported 1123 lines | Capability composition is improving but remains central to multiple policy concerns. |
| `WorkspaceRuntimePlugin.cs` | CodeAnalytics reported 818 lines | Common workspace plugin includes domain-specific image prompts. |
| `ProcessRuntimeEngine` | CodeAnalytics reported 54 source members | Process runtime is broad and should not absorb MAF-specific policy details. |

## Current MAF Projects

| Project | Current role | Notes |
| --- | --- | --- |
| `CanDoItAll.AgentFramework.Models` | Agent/runtime DTOs | `AgentRuntimeContextIntent` lives here and currently has no scoped capability override. |
| `CanDoItAll.AgentFramework.Core` | Execution metadata, workspace policies, capability proof helpers | Builds `AgentRuntimeContextIntent` from execution metadata. |
| `CanDoItAll.AgentFramework.Maf` | MAF wrapper runtime, capability composition, workspace plugin | Owns current domain leak and current access planning. |
| `CanDoItAll.AgentFramework.Capabilities.Abstractions` | Capability descriptors, selectors, rules, diagnostics | Existing type system should be reused for MAF policy mapping. |
| `CanDoItAll.AgentFramework.Capabilities.Access` | Evaluator implementation | Deny/require behavior exists; allowlist semantics need care. |
| `CanDoItAll.AgentFramework.Tooling` | Runtime tool-provider contracts | Provider descriptors and metadata exist, but provider key is not yet a capability selector. |

## Current Process Projects

| Project | Current role | Notes |
| --- | --- | --- |
| `CanDoItAll.Processes.Contracts` | Process DTO contracts | Candidate home for process-neutral authoring/runtime scope DTOs if shared externally. |
| `CanDoItAll.Processes.Abstractions` | Process IDs and simple value types | Existing `CapabilityTag` here is process-driver capability, not MAF runtime capability. |
| `CanDoItAll.Processes.Core` | Process domain/core behavior | Should remain MAF-independent. |
| `CanDoItAll.Processes.Templates` | Template document loading | Needs step scope document fields and validation. |
| `CanDoItAll.Processes.Application` | Launch service and brief composition | Builds runtime assignments and process prompts. |
| `CanDoItAll.Processes.Runtime` | Runtime step assignment model and stores | Needs effective step scope persisted. |
| `CanDoItAll.Modules.Processes` | AgentFramework process integration | Correct place to translate process scope into MAF metadata and prompt fragments. |

## Existing Tests To Extend

| Test file | Use |
| --- | --- |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs` | Extend capability access and operation filtering coverage. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs` | Extend prompt/scope composition tests. |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs` | Add end-to-end process assignment and dispatch proof. |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs` | Check development-specific prompts remain seeded only in domain owners. |
