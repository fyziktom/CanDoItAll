# Current state review

## What Codex improved well

Codex did not only add documentation. It implemented meaningful runtime hardening:

- The canonical CanDoItAll workflow model remains the persistence and UI model.
- `MafWorkflowCompiler` now validates the workflow before building and uses a native MAF `WorkflowBuilder` boundary.
- The compiler handles direct edges, predicate edges, switch routing, fan-out routing, and preview simulation hooks.
- `WorkflowExecutorContracts` now contains a stronger executor catalog/invoker boundary with duplicate executor checks, retry/timeout handling, redaction, payload limits, audit records, and approval-gate plumbing.
- `WorkflowExecutorModels` now models executor source, trust level, UI icon metadata, availability, configuration schema, permission policy, deterministic test mode, and simulation descriptors.
- Plugin manifests now carry workflow executor permission policy and deterministic test metadata.
- Gmail/Office365/Docker executors gained availability checks, simulation descriptors, and permission policies.
- Catalog save paths validate definitions before persistence.
- The previous bundle has targeted proof files and an explicit final architecture review.

## Remaining gaps that matter before building further

### G1 - MAF package baseline is already stale

The current repo still references MAF `1.6.2` stable packages and `1.6.2-preview.260521.1` A2A packages. Codex intentionally deferred package migration. NuGet showed `Microsoft.Agents.AI.Workflows` `1.8.0` as available on 2026-05-28. This must be handled as a separate first gate before more runtime behavior is built against older APIs.

### G2 - Human-in-loop is currently graph-level preemptive, not execution-position aware

`WorkflowRuntimeManager.StartAsync` currently checks whether any `HumanInput` node exists in the graph. If one is found, it immediately creates a waiting run and pending external request before the backend executes the graph.

This is too coarse. A graph like `Start -> LLM -> Conditional -> HumanInput -> End` should execute the preceding nodes and only pause if the route actually reaches the human node. It also bypasses MAF request/response and checkpoint behavior.

### G3 - Approval gate plumbing exists but no product gate is registered in the reviewed Agent Framework registrations

`IWorkflowExecutorApprovalGate` exists and the invoker rejects approval-required executors when no gate is registered. Docker executors require approval always, and Gmail/Office365 mark-processed executors require approval for external effects. The reviewed Agent Framework service registrations register the invoker but not a concrete approval gate. This is safe by default, but it means approved live execution cannot proceed through the product yet.

### G4 - Event persistence still loses native MAF fidelity

`MafInProcessWorkflowExecutionBackend` currently uses non-streaming `InProcessExecution.RunAsync`, converts MAF events after the run, stores native MAF event records with `NodeId: null`, uses `workflowEvent.ToString()` as the message, and extracts payload with reflection from a `Data` property. Progress events add node identity, but native output/error/request/superstep events are still not represented with enough fidelity for debugging, resume, or UI.

### G5 - Checkpointing is not implemented yet

The runtime descriptor mentions in-process and durable concepts, but the current backend does not wire a checkpoint manager/storage, does not persist checkpoint metadata, and does not support resume/rehydration flows. The previous bundle explicitly deferred durable production backends.

### G6 - Artifact policy is only partially applied

Configured file artifacts are created after successful completion for selected file/spreadsheet executors. But output artifacts, JSON/text artifacts, tool receipts, large event payloads, and raw started input payloads are not yet governed consistently by `WorkflowSettings.ArtifactPolicy`.

### G7 - Plugin observer registration is order-dependent

`AgentFrameworkModuleServiceCollectionExtensions` registers `IWorkflowExecutorExecutionObserver` with `TryAddScoped<NullWorkflowExecutorExecutionObserver>`. `PluginsModuleServiceCollectionExtensions` registers `PluginWorkflowExecutorExecutionObserver` also with `TryAddScoped`. Depending on composition order, the plugin runtime observer may silently lose to the null observer. This should be replaced by a deterministic composite or explicit override.

### G8 - Plugin manifest validation does not yet validate permission-policy consistency

`PluginManifestValidator` checks duplicate IDs and whether metadata features imply basic capabilities. It does not yet validate that executor permission flags are covered by plugin capabilities, for example:
- `RunsHostCommand` requires `PluginCapabilityKind.HostCommand`.
- `UsesSecrets` should require `SecretReference`, `OAuth2`, or a secret-backed connection.
- `UsesNetwork` should require `HttpClient`, `OAuth2`, or an equivalent declared capability.
- `WritesExternalData` should be highlighted for approval and external-effect governance.

### G9 - Backend catalog advertises more than the runtime actually registers

`WorkflowRuntimeBackendCatalog` lists InProcess, DurableTask, and AzureFunctions. The reviewed service registrations only register `MafInProcessWorkflowExecutionBackend`. `WorkflowRuntimeManager` correctly fails when a requested backend is not registered, but UI/catalog consumers can still see planned durable backends as if they were runtime backends. This must be made honest and user-visible.

### G10 - MAF source-generated executors are still not used

The compiler currently binds every CanDoItAll node as a function executor with `BindAsExecutor`. That is a pragmatic adapter for dynamic graphs, but MAF documentation recommends C# `[MessageHandler]` partial executors for source generation, compile-time validation, better performance, and Native AOT compatibility. The follow-up should either introduce static adapter executors for stable node families or create an ADR explaining why dynamic `BindAsExecutor` remains the correct boundary for graph-authored workflows.
