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

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AgentFramework.Maf.csproj](CanDoItAll.AgentFramework.Maf.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Runtime Proof Slices

MAF runtime regression proof is tracked by named slices so process automation and release checks do not treat one broad test run as complete coverage:

| Slice | Primary source | Regression proof |
| --- | --- | --- |
| Tool loop | `Runtime/MafAgentRuntime.cs`, `Runtime/MafRuntimeAgentFactory.cs`, and `Runtime/MafRuntimeToolInvocationResultClassifier.cs` | Tool-call snapshots, repeated-tool guards, tool signature hashing, and tool invocation result parsing tests. |
| Context provider | `Runtime/Capabilities/ContextCapabilityBuilder.cs` and `Runtime/Capabilities/RuntimeCapabilityComposer.cs` | Compaction/context provider attachment and suppression tests. |
| Finalizer | `Runtime/MafAgentRuntime.cs`, `Runtime/MafRuntimeAgentFactory.cs`, and `Runtime/MafFinalizerDriver.cs` | Required/shadow finalizer attachment, exact-once capture, post-streaming finalization, and JSON-only instruction tests. |
| Errors | `Runtime/MafAgentRuntime.cs`, `Runtime/MafRuntimeSessionBuilder.cs`, `Runtime/MafModelParametersBuilder.cs`, and `Runtime/MafRuntimeToolInvocationResultClassifier.cs` | Timeout clamping, bounded finalizer session serialization, incompatible approval continuation rejection, and nested tool failure parsing tests. |
| Approvals | `Runtime/MafRuntimeAgentFactory.cs` and capability policy code | Approval-required function wrapping, unusable approval-tool filtering, and policy-block static tests. |
| MCP | `Runtime/Capabilities/McpCapabilityBuilder.cs` | Browser MCP result bounding tests that remove image payloads and cap snapshot text. |
| A2A | `Runtime/Capabilities/A2ARemoteAgentToolFactory.cs` | Disabled endpoint, missing bearer secret, and invalid endpoint tests. |
| Workflow mapping | `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowCompiler.cs` and `Runtime/Handoffs/MafHandoffWorkflowFactory.cs` | MAF 1.15 workflow symbol reflection, handoff routing, depth guard, workflow response format, and status/event mapper source assertions. |
| Trace correlation | `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowCompiler.cs` and execution response models | Tool invocation traces, finalizer invocation traces, workflow audit scope, and OpenTelemetry package presence source assertions. |

## Architecture Notes

Keep AgentFramework model contracts, provider-neutral orchestration, and provider/runtime adapters separated. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly. MAF must not reference `CanDoItAll.Modules.*` projects or the `Workflows.MafAdapter` project; secret-runtime contracts arrive through the dependency-free `CanDoItAll.Security.Abstractions` foundation project and storage contracts through `CanDoItAll.Infrastructure`. First-party product tool ownership belongs in registered `IAgentRuntimeToolProvider` implementations.

### Runtime ports and the MAF adapter

Production callers consume MAF through four narrow runtime ports declared in `CanDoItAll.AgentFramework.Runtime.Abstractions` — `IAgentExecutionRuntime`, `IAgentContinuationRuntime`, `IProviderDiagnosticsRuntime`, `IProviderModelAdministrationRuntime` — never a single broad runtime interface (the pre-SB18 `IAgentRuntime` surface was deleted once every caller finished migrating to the ports). `Runtime/MafAgentRuntime.cs` is now a **pure composition root**: it builds exactly one set of native adapters (`MafAgentExecutionAdapter`, `MafAgentContinuationAdapter`, `MafProviderDiagnosticsAdapter`, `MafProviderModelAdministrationAdapter`) per runtime scope from `MafAgentRuntimeDependencies` and exposes them as the `ExecutionPort` / `ContinuationPort` / `DiagnosticsPort` / `ModelAdministrationPort` properties. It contains no streaming, session, finalizer, or response-assembly logic — that lives in `Runtime/Execution/MafStreamingTurnExecutor.cs` and its collaborators. Composition sites (Hosting's `AddAgentFrameworkCore`, the Modules.AgentFramework module registration, and `CanDoItAllAgentWorkspaceFactory`) construct/register the four ports directly against this composition root; process-mock and scenario-harness test/proof providers are port-level decorators (`ProcessMockExecutionDecorator`/`ProcessMockDiagnosticsDecorator`, `ScenarioHarnessExecutionDecorator`/`ScenarioHarnessDiagnosticsDecorator`) that own their own provider-matching branch and their own deterministic interception bodies — they no longer wrap a broad runtime interface.

## Process Automation Notes

- Process execution currently reaches MAF through the Processes module adapter layer, especially `AgentFrameworkProcessExecutionAdapter` and related launch/assignment services in `CanDoItAll.Modules.Processes`.
- A concrete direct `ProcessAgentRuntimeToolProvider` is not present in the current source tree. Direct `processes_*` tools should not be documented as available until that provider is reintroduced with typed models, policy classifications, approval behavior, and tests.
- First-party runtime tools are registered by their owning modules. The current provider
  keys cover Memory, ProjectStructure, ImageGeneration, Workflow, PromptGallery,
  PromptsCurator, WorkflowCurator, CapabilityCurator, HR, and Scheduler. Do not
  reintroduce hard-coded product-tool attachment methods into MAF.
- MAF process agents should use explicit process context, structured output/finalizer contracts, and approved project-structure/process API paths for run state. They should not infer process state from prompt text, template files, or database rows.
- Adopted MAF 1.15 surfaces are tracked by the proof slices above: tool loop, context providers, finalizer, errors, approvals, MCP bounding, A2A endpoint validation, workflow mapping, and trace correlation.
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
4. Check `IAgentToolInvocationPolicy` evaluation and `AgentToolInvocationPolicyMetadata` classification before changing approval behavior.

Do not repair missing process operations by adding a MAF project reference to the Processes module. Implement or remove the direct process tool provider at the owning boundary.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Process/MAF/provider implementation map: `docs/architecture/internal-communication.md`
