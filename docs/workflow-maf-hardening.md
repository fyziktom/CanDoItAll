# Workflow MAF Hardening

CanDoItAll persists workflow definitions in its domain model and executes them through a Microsoft Agent Framework adapter boundary. The domain model remains the canonical shape for storage, API, and UI editing; MAF is the runtime execution boundary.

## Authoring Rules

- Workflow template packs live under `Templates/Workflows/manifest.yaml` and referenced YAML workflow files.
- Template graphs are converted to canonical `WorkflowGraph` instances and semantically validated during template pack load.
- Workflow definitions are validated before persistence. Invalid nodes, edges, components, executor settings, or unavailable executors fail the save operation instead of being stored for a later publish-time failure.
- User-managed workflow definitions must not be overwritten by example seeding unless they carry the configured managed seed marker.

## Runtime Boundary

- `IWorkflowMafCompiler` is the adapter seam between canonical workflow definitions and native MAF workflow instances.
- `MafInProcessWorkflowExecutionBackend` is the preview/in-process backend and records node progress as workflow event records with stable node ids.
- Runtime policy is enforced before backend dispatch. A workflow that requires durable production execution and disables in-process previews cannot be started through the in-process backend.
- Durable production backends must be registered explicitly. Missing backends fail predictably with a runtime error.

## MAF Executor Binding Strategy

- CanDoItAll workflow definitions are dynamic user-authored graphs, so the MAF compiler intentionally uses `BindAsExecutor` as the adapter boundary for runtime node handlers.
- Source-generated MAF executors are not the default for graph-authored workflows because saved workflow shapes, node ids, plugin executor ids, and routing metadata are not known at compile time.
- Source-generated executors remain a future option for static, code-owned workflow families only when benchmark or Native AOT evidence justifies the added generated-code path.

## Backend Catalog And Production Honesty

- `WorkflowRuntimeBackendDescriptor` exposes `Availability`, `IsRegistered`, `IsRunnable`, and `AvailabilityReason` so API and UI callers can distinguish runnable backends from planned capabilities.
- The current host registers only the `InProcess` backend as runnable. `DurableTask` and `AzureFunctions` remain planned and unavailable until real durable implementations are registered.
- Workflow save, settings save, test-run, and start paths validate runtime policy against the registered backend catalog. An unavailable durable backend fails explicitly; the runtime must not silently fall back to in-process execution.
- New workflow settings, example seed settings, and template metadata default to in-process preview execution with durable production disabled.
- `/api/workflows/contract` exposes the current workflow control route list and the boundary that agent skill, tool, and MCP setup belongs to the Agents API.
- `/api/workflows/runtime-backends` and the workflow editor runtime selector expose planned durable backends as disabled with an availability reason.

## Checkpoint Trust Boundary

- Workflow checkpoints are persisted as metadata records through `IWorkflowCheckpointStore`; the default in-process backend captures metadata-only checkpoints at terminal and waiting-for-input runtime boundaries.
- Metadata-only checkpoints intentionally do not contain raw native MAF state, workflow input payloads, executor outputs, secrets, or user-uploaded blobs.
- The `PayloadReference` for in-process checkpoints is a private metadata marker, not a resumable state location. Normal API/UI responses must expose the checkpoint metadata and `ResumeAvailability`, not raw checkpoint payloads.
- Resume is explicitly marked `NotSupported` for metadata-only checkpoints. Enable resume only after a durable backend can write and read trusted runtime state from private infrastructure storage.
- Checkpoint blobs, when a future durable backend adds them, are a trust boundary: never load them from user-controlled locations, never accept uploaded checkpoint payloads as runtime state, and never expose the raw blob in normal UI.

## Payload And Artifact Policy

- `IWorkflowPayloadPolicyService` is the single policy boundary for workflow runtime payload storage.
- Payloads are redacted before inline bounding and before artifact metadata is created.
- Runtime input, node output, executor errors, external requests, event payloads, plugin log messages/details, and tool receipts must use bounded inline payloads.
- Oversized or capture-enabled payloads create safe artifact records with summary/reference metadata; normal workflow records must not contain raw unbounded payload blobs.
- Default artifact policy allows JSON, text, file, tool receipt, and preview simulation artifact records.
- Current in-process artifacts are safe references and summaries. Durable storage for full redacted artifact blobs requires an explicitly registered backend and must not silently fall back to in-process storage.

## Executor Contract

Workflow executors must expose a `WorkflowExecutorDescriptor` with:

- stable executor id, display metadata, source, availability, input/result shapes, settings schema, and default execution policy;
- `PermissionPolicy` describing required capabilities and approval requirement;
- `DeterministicTestMode` describing whether preview or fake execution can prove behavior without live external services.

Executor descriptors default to no capabilities, no approval requirement, and no deterministic mode so legacy serialized descriptors remain safe and explicit.

## Plugin Executor Governance

Plugin executor audit records are composed through `IWorkflowExecutorExecutionAuditSink` and `CompositeWorkflowExecutorExecutionObserver`. Plugin audit logging must be registered as a sink, not as the process-wide observer, so module registration order cannot disable runtime audit persistence.

Plugin manifests are rejected when workflow executor permission policies do not match manifest capabilities or connection metadata:

- network or external data access requires `HttpClient` or `OAuth2` capability;
- secret access requires `SecretReference` or `OAuth2` capability plus secret or OAuth connection metadata;
- external writes require approval;
- host commands require `HostCommand` capability and `AlwaysRequired` approval;
- deterministic test mode must be declared consistently by capability flags and executor metadata.

Deterministic fake-mode proof for Gmail, Office365, and Docker must use preview simulation templates. Default tests must not invoke live external services, mutate email state, or execute Docker/host commands.

## Approval Policy

`WorkflowExecutorInvoker` enforces `WorkflowExecutorPermissionPolicy` before executor implementation code runs.

- `NotRequired` executors run without an approval gate.
- `RequiredForExternalEffect` and `AlwaysRequired` executors require an `IWorkflowExecutorApprovalGate`.
- If no gate is registered, invocation fails before side effects.
- If approval is denied, invocation fails before side effects and denial text is redacted before it is included in the exception message.

Current bundled policy intent:

- Gmail and Office365 download executors read external data, use network/secrets, and support deterministic preview without approval.
- Gmail and Office365 mark-processed executors write external data and require approval for external effects.
- Docker workflow executors run host commands and always require approval.

## Testing And Troubleshooting

- Use template loader tests to prove every repository template remains loadable and semantically valid.
- Use compiler/backend tests to prove representative graphs compile and emit stable runtime events.
- Use plugin executor tests with fakes for success, failure, cancellation, redaction, approval denial, and artifact paths.
- Live Gmail, Office365, and Docker proof remains optional unless the required local services and secrets are configured; deterministic fake proof is the required baseline.
- A save response containing `Workflow definition save failed validation` means the definition was rejected before persistence. Fix the reported node, edge, component, or executor issue and retry the save.
