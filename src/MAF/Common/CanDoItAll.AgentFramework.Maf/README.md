# CanDoItAll.AgentFramework.Maf

## Purpose

Microsoft Agent Framework adapter that connects CanDoItAll execution runs to provider runtimes, tools, skills, and MCP servers.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.AgentFramework.Providers/CanDoItAll.AgentFramework.Providers.csproj`
- `../CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `../CanDoItAll.Tools.Documents/CanDoItAll.Tools.Documents.csproj`
- `../CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`

Framework references:

- None

Direct package references:

- `Azure.AI.OpenAI (2.9.0-beta.1)`
- `ExcelDataReader (3.8.0)`
- `Microsoft.Agents.AI (1.8.0)`
- `Microsoft.Agents.AI.A2A (1.8.0-preview.260528.1)`
- `Microsoft.Agents.AI.Mem0 (1.0.0-preview.251028.1)`
- `Microsoft.Agents.AI.OpenAI (1.8.0)`
- `Microsoft.Agents.AI.Workflows (1.8.0)`
- `ModelContextProtocol (1.1.0)`
- `OllamaSharp (5.4.25)`
- `OpenTelemetry.Api (1.15.3)`
- `PdfPig (0.1.14)`

## Runtime Proof Slices

MAF runtime regression proof is tracked by named slices so process automation and release checks do not treat one broad test run as complete coverage:

| Slice | Primary source | Regression proof |
| --- | --- | --- |
| Tool loop | `Runtime/MafAgentRuntime.cs` and `Runtime/MafAgentRuntime.AgentFactory.cs` | Tool-call snapshots, repeated-tool guards, tool signature hashing, and tool invocation result parsing tests. |
| Context provider | `Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs` | Compaction/context provider attachment and suppression tests. |
| Finalizer | `Runtime/MafAgentRuntime.cs` and `Runtime/MafAgentRuntime.AgentFactory.cs` | Required/shadow finalizer attachment, exact-once capture, post-streaming finalization, and JSON-only instruction tests. |
| Errors | `Runtime/MafAgentRuntime.cs`, `Runtime/MafAgentRuntime.Session.cs`, and `Runtime/MafAgentRuntime.ModelParameters.cs` | Timeout clamping, bounded finalizer session serialization, incompatible approval continuation rejection, and nested tool failure parsing tests. |
| Approvals | `Runtime/MafAgentRuntime.AgentFactory.cs` and capability policy code | Approval-required function wrapping, unusable approval-tool filtering, and policy-block static tests. |
| MCP | `Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | Browser MCP result bounding tests that remove image payloads and cap snapshot text. |
| A2A | `Runtime/Capabilities/A2ARemoteAgentToolFactory.cs` | Disabled endpoint, missing bearer secret, and invalid endpoint tests. |
| Workflow mapping | `Runtime/Workflows/MafWorkflowCompiler.cs` and `Runtime/MafHandoffWorkflowFactory.cs` | MAF 1.8 workflow symbol reflection, handoff routing, depth guard, workflow response format, and status/event mapper source assertions. |
| Trace correlation | `Runtime/Workflows/MafWorkflowCompiler.cs` and execution response models | Tool invocation traces, finalizer invocation traces, workflow audit scope, and OpenTelemetry package presence source assertions. |

## Architecture Notes

Keep AgentFramework model contracts, provider-neutral orchestration, and provider/runtime adapters separated. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly. MAF currently allows direct references to `CanDoItAll.Modules.Security` and `CanDoItAll.Modules.Workspace`; first-party product tool ownership belongs in registered `IAgentRuntimeToolProvider` implementations.

## Process Automation Notes

- Process execution currently reaches MAF through the Processes module adapter layer, especially `AgentFrameworkProcessExecutionAdapter` and related launch/assignment services in `CanDoItAll.Modules.Processes`.
- A concrete direct `ProcessAgentRuntimeToolProvider` is not present in the current source tree. Direct `processes_*` tools should not be documented as available until that provider is reintroduced with typed models, policy classifications, approval behavior, and tests.
- Project-structure tools live in Workbench as `ProjectStructureAgentRuntimeToolProvider`; image-generation tools live in the AgentFramework module as `ImageGenerationAgentRuntimeToolProvider`. Do not reintroduce hard-coded first-party product tool attachment methods into MAF.
- MAF process agents should use explicit process context, structured output/finalizer contracts, and approved project-structure/process API paths for run state. They should not infer process state from prompt text, template files, or database rows.
- Adopted MAF 1.8 surfaces are tracked by the proof slices above: tool loop, context providers, finalizer, errors, approvals, MCP bounding, A2A endpoint validation, workflow mapping, and trace correlation.
- Deferred or guarded surfaces must fail predictably. A2A endpoints require valid configuration and bearer secrets, browser MCP payloads are bounded, incompatible approval continuations are rejected, and workflow handoff depth is guarded.
- Process automation that records final delivery must produce current-run evidence and let Processes validate the artifact and transition. MAF finalizers should not mark process steps complete by prose-only conclusion text.

## Runtime Tool Provider Observability

MAF records runtime-provider ownership at attach and invocation time. The provider attach progress message includes each provider key, display name, and attached tool count. During invocation, MAF tags the activity and `AgentToolInvocationTrace` with `RuntimeToolProviderKey` and `RuntimeToolProviderName` when the tool came from `IAgentRuntimeToolProvider` metadata.

Workspace receipts written inside a provider-owned tool invocation inherit the same optional provider key/name through the Core audit context. Provider-native receipt projections copy those optional fields from the source receipt when available and leave them empty for existing persisted runs.

## Runtime Tool Provider Troubleshooting

When a run appears to need direct process tools:

1. Confirm whether the operation can use `/api/processes` or project-structure bridge tools instead.
2. Confirm the current `IEnumerable<IAgentRuntimeToolProvider>` contains only the expected registered providers for that scope.
3. Treat missing direct `processes_*` tools as a product gap, not a MAF attachment failure, unless a concrete process runtime tool provider has been reintroduced.
4. Check `AgentToolInvocationPolicy` classification before changing approval behavior.

Do not repair missing process operations by adding a MAF project reference to the Processes module. Implement or remove the direct process tool provider at the owning boundary.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Process/MAF/provider implementation map: `docs/processes-maf-providers-implementation-map.md`
