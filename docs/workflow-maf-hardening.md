# Workflow MAF Hardening

CanDoItAll persists workflow definitions in its domain model and executes them through a Microsoft Agent Framework adapter boundary. The domain model remains the canonical shape for storage, API, and UI editing; MAF is the runtime execution boundary.

## Authoring Rules

- Workflow template packs live under `Templates/Workflows/manifest.yaml` and referenced YAML workflow files.
- Template graphs are converted to canonical `WorkflowGraph` instances and semantically validated during template pack load.
- Workflow definitions are validated before persistence. Invalid nodes, edges, components, executor settings, or unavailable executors fail the save operation instead of being stored for a later publish-time failure.
- User-managed workflow definitions must not be overwritten by example seeding unless they carry the configured managed seed marker.

## Current Project Ownership

- `CanDoItAll.AgentFramework.Models` owns persisted workflow model contracts and JSON compatibility. Do not move serialized workflow ids, nodes, edges, ports, runtime policy, run state, events, checkpoints, artifacts, or value-shape contracts without migration proof.
- `CanDoItAll.AgentFramework.Workflows.Abstractions` owns workflow service contracts and typed failure diagnostic envelopes.
- `CanDoItAll.AgentFramework.Workflows.Builder` owns test and template graph builders. New template materialization paths should use these builders instead of hand-building graph dictionaries.
- `CanDoItAll.AgentFramework.Workflows.Core` owns validation, catalog services, runtime policy validation, routing compilation, payload policy, preview simulation rendering, process bridge, test runner support, and user-facing failure display formatting.
- `CanDoItAll.AgentFramework.Workflows.Runtime` owns runtime manager/store contracts, in-memory runtime stores, event sinks, checkpoints, external request runtime support, artifact content stores, event payload helpers, node progress, and runtime diagnostics.
- `CanDoItAll.AgentFramework.Workflows.Templates` owns manifest/YAML parsing, template DTOs, input materialization, graph materialization, preview fixtures, descriptor-aware template validation, and template diagnostics.
- `CanDoItAll.AgentFramework.Workflows.MafAdapter` owns MAF-specific workflow compilation, in-process backend execution, event normalization, LLM component invocation, handoff workflow creation, adapter registration, and adapter diagnostics.
- `CanDoItAll.AgentFramework.Maf` remains the agent runtime adapter project. It must not regain workflow compiler/backend ownership, default workflow executor implementations, or workflow template loading.
- `CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions` owns executor contracts, descriptors, catalog/invoker/approval contracts, execution context, and audit contracts.
- `CanDoItAll.AgentFramework.WorkflowExecutors.Core` owns executor catalog composition, invoker, policy limits, side-effect safety, payload/redaction helpers, observability, JSON/settings helpers, descriptor factory, shared built-in descriptor constants, and executor diagnostics.
- Default executors live in category projects under `CanDoItAll.AgentFramework.WorkflowExecutors.Standard.*`; the aggregate `CanDoItAll.AgentFramework.WorkflowExecutors.Standard` project composes the category registrations.
- `CanDoItAll.AgentFramework.WorkflowExecutors.Plugins` owns plugin executor descriptor projection, grant evaluation boundary contracts, source/trust mapping, runtime package wrapping, and plugin activation diagnostics. `CanDoItAll.Modules.Plugins` keeps plugin persistence, package loading, grants, OAuth, connections, logs, and UI.
- Feature-module executors stay with their owning feature modules and reference executor abstractions/core directly. Do not force domain executors into the standard executor category projects.

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

## Adding Workflow Templates

- Add or update the template YAML under `Templates/Workflows` and keep the manifest entry stable.
- Materialize through `WorkflowTemplatePackLoader`, which validates templates against `IWorkflowExecutorCatalog` when a catalog is supplied.
- Template failures must throw `WorkflowTemplatePackException` with file, template key, workflow key, YAML path, node id, executor id, and repair hint when known.
- UI pages and Workbench code must consume template services; they must not own YAML DTOs, duplicate graph materialization, or silently skip malformed templates.
- Validation must include positive load/materialization proof and at least one negative test for malformed YAML, unknown executor, invalid settings, invalid routing, invalid input, or invalid preview fixture behavior when that area changes.

## Adding Default Executors

- Choose the narrowest standard category project: Control, Transforms, Workspace, Network, Documents, Media, or ProjectStructure. Add a new category only when dependencies or ownership justify it.
- Describe the executor through `WorkflowExecutorDescriptorFactory` or the existing category descriptor source patterns. Preserve stable executor ids and value-shape compatibility.
- Register implementations through the category service collection extension and aggregate through `AddStandardWorkflowExecutors(...)`. Do not reintroduce MAF-owned default executor registration aliases.
- Declare `PermissionPolicy`, approval behavior, side-effect risk, payload limits, timeout behavior, and `DeterministicTestMode` explicitly.
- Add category isolation tests and descriptor parity tests. If the executor touches external services, tests must use deterministic fakes unless live credentials are intentionally configured outside the default proof path.

## Adding Plugin Executors

- Bundled plugin executors live in their plugin projects and reference executor abstractions/core. Runtime package executors are wrapped through `CanDoItAll.AgentFramework.WorkflowExecutors.Plugins`.
- Plugin manifests must declare capabilities and connection metadata that match the executor permission policy. External writes require approval; host commands require host-command capability and always-required approval.
- Grant, OAuth, secret, package activation, dependency, and execution failures must carry plugin id, package id when known, executor type when known, operation, retryability, redacted technical detail, and a repair hint.
- Plugin executor tests must prove descriptor projection, source/trust metadata, grant behavior, approval behavior, redaction, deterministic fake execution, cancellation, and failure diagnostics without live external mutation in the default path.

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
- UI/browser proof for this initiative is large-screen-only. Small and medium viewport checks are intentionally skipped while the product target remains large-screen desktop.

## Diagnostic And File Responsibility Rules

- `WorkflowFailureDiagnosticEnvelope` is the shared diagnostic contract. Validation, runtime, executor, plugin, template, MAF adapter, external tool/MCP, API, UI, and Workbench failures must preserve typed context instead of depending on exception-message parsing.
- Diagnostics must include retryability, a concrete repair hint, redacted technical detail, and the most specific available workflow, node, executor, plugin, package, tool, operation, backend, or artifact context.
- User-facing UI and Workbench text must go through `WorkflowFailureDisplayFormatter`. Blazor pages and Workbench files must not deserialize diagnostic envelopes directly or duplicate diagnostic parsing.
- Secret values, OAuth tokens, authorization headers, prompt payloads, email contents, file contents, and sensitive host-command arguments must be masked before display, event payload storage, audit messages, and artifact summaries.
- Do not introduce silent fallback paths for missing executors, unavailable plugins, failed package activation, missing grants, unavailable durable backends, artifact writes, or checkpoint writes. Fail explicitly with typed diagnostics.
- Avoid copied monoliths. New non-trivial workflow, executor, plugin, template, adapter, or Workbench logic needs an obvious owner and focused helper/service files. Existing large UI page files and legacy Workbench project-structure orchestration services are approved exceptions only for their current responsibilities; new parsing, diagnostics, runtime, template, adapter, and executor behavior belongs outside those large files.
