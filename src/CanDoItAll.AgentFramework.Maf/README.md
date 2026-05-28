# CanDoItAll.AgentFramework.Maf

## Purpose

Microsoft Agent Framework adapter that connects CanDoItAll execution runs to provider runtimes, tools, skills, and MCP servers.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.Tools.Documents/CanDoItAll.Tools.Documents.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj`
- `../CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`

Framework references:

- None

Direct package references:

- `Azure.AI.OpenAI (2.9.0-beta.1)`
- `ExcelDataReader (3.8.0)`
- `Microsoft.Agents.AI (1.6.2)`
- `Microsoft.Agents.AI.A2A (1.6.2-preview.260521.1)`
- `Microsoft.Agents.AI.Mem0 (1.0.0-preview.251028.1)`
- `Microsoft.Agents.AI.OpenAI (1.6.2)`
- `Microsoft.Agents.AI.Workflows (1.6.2)`
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
| Workflow mapping | `Runtime/Workflows/MafWorkflowCompiler.cs` and `Runtime/MafHandoffWorkflowFactory.cs` | MAF 1.6 workflow symbol reflection, handoff routing, depth guard, workflow response format, and status/event mapper source assertions. |
| Trace correlation | `Runtime/Workflows/MafWorkflowCompiler.cs` and execution response models | Tool invocation traces, finalizer invocation traces, workflow audit scope, and OpenTelemetry package presence source assertions. |

## Architecture Notes

Keep AgentFramework model contracts, persistence, provider-neutral orchestration, and provider/runtime adapters separated. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly.

## Process Automation Notes

- Internal process tools are composed by `MafAgentRuntime.ProcessToolBuilder` when process services are available. Read tools such as `processes_run_detail_get`, `processes_template_baseline_scenarios_list`, and `processes_template_live_run_profiles_list` must remain approval-free; mutation tools such as transitions, assignment resolution, artifact recording, definition saves, and template imports require approval wrappers unless governed automation explicitly suppresses approvals.
- MAF process agents should use the process API/tool surface for run state, artifacts, assignments, manager directives, and live-run profiles. They should not infer process state from prompt text, template files, or database rows.
- Adopted MAF 1.6 surfaces are tracked by the proof slices above: tool loop, context providers, finalizer, errors, approvals, MCP bounding, A2A endpoint validation, workflow mapping, and trace correlation.
- Deferred or guarded surfaces must fail predictably. A2A endpoints require valid configuration and bearer secrets, browser MCP payloads are bounded, incompatible approval continuations are rejected, and workflow handoff depth is guarded.
- Process automation that records final delivery must produce current-run evidence and let Processes validate the artifact and transition. MAF finalizers should not mark process steps complete by prose-only conclusion text.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
