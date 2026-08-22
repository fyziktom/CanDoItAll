# Package and Code Impact Map

## Package edit owner

| Path | Current role | Planned action |
|---|---|---|
| `src/MAF/MicrosoftAgentFramework.Packages.props` | Stable/preview version source | Set stable `1.18.0`, preview `1.18.0-preview.260818.1` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Maf/*.csproj` | Core MAF/A2A/OpenAI/Workflows consumer | Restore/build; no independent version literals |
| `src/MAF/Common/CanDoItAll.AgentFramework.Hosting/*.csproj` | Hosting A2A preview consumer | Restore/build; adapt isolation rename only if used |
| `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/*.csproj` | Workflow MAF consumer | Restore/build; later HITL implementation |

SB00 must search all `*.csproj`, `*.props`, `*.targets`, `packages.lock.json`, generated NuGet assets, and test projects for `Microsoft.Agents.AI` and literal `1.17`.

## Agent/tool surfaces

| Surface | Why affected | Owner |
|---|---|---|
| `MafChatClientAgentOptionsFactory.cs` | New concurrency option | SB02 |
| `MafProviderAgentFactory.cs` | Provider-specific `AsAIAgent`/client factories | SB01/SB02 |
| `MafRuntimeAgentFactory.cs` | Agent option/application composition | SB01/SB02 |
| `MafStreamingTurnExecutor.cs` | Streaming agent execution and custom clients | SB02 |
| approval continuation driver/session serialization | MAF approval fixes and replay behavior | SB02 |
| usage/telemetry compatibility projection | Upstream usage aggregation fixes | SB02 |
| skill discovery | Upstream file-skill hardening | SB02 only if resolved package path applies |

## Workflow surfaces

| Surface | Baseline problem | Planned owner |
|---|---|---|
| `MafWorkflowCompiler.cs` | HumanInput throws pending exception; one executor binding per node | SB03 |
| `WorkflowExternalRequestRuntime.cs` | Approval gate throws as pause | SB03/SB04 |
| `MafInProcessWorkflowExecutionBackend.cs` | Non-streaming RunAsync; no real checkpoint; resume false | SB03 |
| `WorkflowRuntimeContracts.cs` | Good base, lacks payload/operation recovery detail | SB04 |
| `WorkflowRuntimeManager.cs` | Marks answered before recoverable continuation | SB04 |
| `PersistentWorkflowStores.cs` | Metadata only, no MAF JSON payload/operation ledger | SB04 |
| service registration | Must wire checkpoint adapter, authorizer, operation store, dedup store | SB03–SB05 |
| `WorkflowsApi.cs` | Existing route; raw JSON string; thin auth/idempotency | SB05 |
| API docs | Contract does not describe completed HITL | SB05/SB06 |

## Expected new focused classes

Names are suggestions; use repository naming conventions.

- `MafWorkflowHitlBindingCompiler`
- `MafWorkflowStreamingRunDriver`
- `MafWorkflowExternalResponseDriver`
- `MafJsonCheckpointStoreAdapter`
- `IWorkflowBackendCheckpointPayloadStore`
- `WorkflowBackendCheckpointPayload`
- `IWorkflowExternalResponseOperationStore`
- `WorkflowExternalResponseOperation`
- `WorkflowExternalResponseService`
- `IWorkflowExternalRequestAuthorizer`
- `WorkflowExternalRequestResponseValidator`
- `IWorkflowExecutorInvocationDeduplicationStore`
- persistent implementations and EF mappings
- focused API DTOs/result mapper

Avoid creating all functionality inside the existing compiler, backend, manager, API, or persistent-store files.

## Discovery commands

Use equivalent repository tools where available:

```bash
rg -n "Microsoft\.Agents\.AI|1\.17\.0|preview\.260804" --glob '*.csproj' --glob '*.props' --glob '*.targets' --glob 'packages.lock.json'
rg -n "SessionIsolationKeyProvider|SessionIsolationKeyProviderOptions|AddSessionIsolationKeyProvider|ClaimsIdentitySessionIsolationKeyProvider"
rg -n "new ChatClientAgentOptions|AllowConcurrentInvocation|FunctionInvokingChatClient|UseProvidedChatClientAsIs|AsAIAgent"
rg -n "ToolApprovalAgent|ToolApprovalAgentOptions|StoreInvocableFunctionCallsForFutureTurns"
rg -n "WorkflowExternalRequestPendingException|WorkflowNodeKind\.HumanInput|SupportsExternalResponseResume|SubmitExternalResponseAsync"
rg -n "WorkflowCheckpointRecord|IWorkflowCheckpointStore|PersistentWorkflowRunStore|RespondedAtUtc"
rg -n "MapPost\(.*external-requests|pending-requests|WorkflowExternalRequestResponseApiRequest"
```

Record surprising results in SB00 before changing scope.
