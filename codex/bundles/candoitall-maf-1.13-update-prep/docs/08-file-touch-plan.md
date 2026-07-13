# File Touch Plan

## Expected package files

| File | Expected change |
| --- | --- |
| `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | MAF stable package version bump. |
| `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | MAF stable package version bump plus dependency-floor alignment. |
| package lock files, if present | Update generated lock graph only. |

## Expected source hotspots if compile breaks occur

| File/folder | Likely break category | Fix rule |
| --- | --- | --- |
| `Runtime/MafAgentRuntime.cs` | `AgentResponse`, `AgentResponseUpdate`, approval content, finalizer response assembly, session serialization. | Compatibility-only fixes. Preserve finalizer and approval semantics. |
| `Runtime/MafRuntimeAgentFactory.cs` | `AIAgent.AsBuilder`, middleware, `ChatClientAgentOptions`, `ApprovalRequiredAIFunction`, logging/OTel, tool policy context. | Preserve policy decisions, audit tags, and runtime tool ownership. |
| `Runtime/MafRuntimeSessionBuilder.cs` | `AgentSession`, `ChatClientAgentRunOptions`, `ResponseContinuationToken`, response format APIs, history providers. | Preserve governed-process isolated session behavior and approval continuation behavior. |
| `Runtime/Capabilities/RuntimeCapabilityComposer.cs` | Skills source API, skill approval defaults, disposable skills, compaction, A2A, MCP, FileAccess/FileMemory. | Preserve access plan, workspace scope, and capability filtering. |
| `Runtime/Providers/MafProviderStreamingRunner.cs` | Streaming overloads and content snapshot types. | Keep provider lane gate, timeout behavior, and snapshot safety. |
| `Runtime/Workflows/*` | Handoff/workflow APIs. | Preserve current handoff workflow composition. |
| `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/**` | Workflow builder/runtime/declarative/checkpoint APIs. | Adapt only existing adapter behavior. |
| tests under `tests/Unit` and `tests/Integration` | Assertions about MAF API type names or behavior changed by package update. | Update tests to reflect intended current behavior, not to mask regressions. |

## Files that should usually not change

| File/folder | Reason |
| --- | --- |
| `src/Processes/CanDoItAll.Processes.Core/**` | Generic process kernel should not know MAF package details. |
| `src/Processes/CanDoItAll.Processes.Runtime/**` | Dispatch semantics should not change for package update. |
| `src/App/CanDoItAll.Web/Api/ProcessesApi.cs` | Route set should not expand in phase 1. |
| `src/Modules/CanDoItAll.Modules.Processes/**` | Only minimal adapter/test changes if compile requires. No new process direct tool provider. |
| `src/Modules/CanDoItAll.Modules.Workbench/**` | Project-structure bridge should not be redesigned. |
| `src/Memory/**` | Memory branch abstractions should not chase MAF package APIs unless Mem0 package restore/compile fails. |

## Diff budget

The ideal package update diff is small. If the source-code diff becomes large, split work:

1. Package update commit.
2. One compile-fix commit per affected adapter seam.
3. Test update commit.
4. Evidence doc commit.

If Codex starts refactoring large classes because they are large, stop. Large-class refactoring is a separate architecture task.
