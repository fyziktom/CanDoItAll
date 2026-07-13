# Failure Diagnostics And Error State Boundary

## Reason For This Boundary

Workflow isolation will increase the number of projects and adapter seams. If failures remain encoded as exception text, each seam can lose context and users or agents will see generic messages that are not repairable. The target architecture must make failure diagnostics a first-class workflow contract before executor and plugin adoption proceeds.

Current source evidence:

- `WorkflowExecutorInvoker` records start/completed/failed audit events, redacts exception messages, and wraps final failures in `WorkflowExecutorInvocationException`.
- `WorkflowFailureDisplayFormatter` currently infers user messages by parsing exception text.
- `RuntimePackageWorkflowExecutor` delegates plugin executor execution directly to the inner executor and projects descriptor source metadata separately.
- `WorkflowEventRecord` stores `Message` and `PayloadJson`; structured failure details must therefore be carried in a stable payload contract and rendered through typed helpers.

## Required Diagnostic Envelope

Every validation, runtime, executor, external tool/MCP, plugin, persistence, artifact, approval, timeout, and cancellation failure must preserve:

| Field | Requirement |
| --- | --- |
| `RunId` | Present when a run has been created. |
| `WorkflowId` and `VersionId` | Present for runtime and execution failures. |
| `NodeId` | Present for node-scoped validation, execution, external request, artifact, or UI failures. |
| `ExecutorId` | Present for executor descriptor, settings, invocation, timeout, payload, plugin, and side-effect failures. |
| `FailureKind` | Strongly typed enum or value object. No stringly typed category comparisons. |
| `SourceKind` | Built-in, bundled plugin, local package, remote package, MAF adapter, external tool/MCP, persistence, or validation. |
| `PluginId`, `PackageId`, `ExecutorTypeName` | Present for plugin/package-provided executors when known. |
| `OperationName` | Present for external tool, MCP server/tool, host command, provider, Graph/Gmail/Docker operation, artifact operation, or persistence operation. |
| `Attempt`, `MaxAttempts`, `TimeoutSeconds` | Present for executor invocation and retry-controlled operations. |
| `Retryability` | Explicit `Retryable`, `NotRetryable`, `RetryAfterExternalAction`, or `Unknown`. |
| `UserMessage` | User-safe, concise, repairable message. Must not include secrets, raw tokens, full file contents, or stack traces. |
| `RepairHint` | Concrete next action, for example "grant plugin permission", "refresh OAuth connection", "fix JSON path", "increase timeout", "check workspace path", or "install package dependency". |
| `TechnicalMessage` | Redacted technical detail suitable for agent repair and local logs. |
| `CorrelationId` | Links UI/API event, audit sink, secure log entry, and artifact when a raw provider/tool error is captured. |
| `ArtifactReference` | Optional reference to a capped failure artifact when raw provider output is too large or sensitive for inline display. |

## Failure Kinds To Cover

Implementation may add more specific values, but these minimum categories must be represented and tested:

- `ValidationFailed`
- `MissingExecutorId`
- `ExecutorNotRegistered`
- `ExecutorUnavailable`
- `InvalidExecutorSettings`
- `UnsafeRetryPolicy`
- `ApprovalGateMissing`
- `ApprovalDenied`
- `ApprovalExpired`
- `Timeout`
- `Cancelled`
- `PayloadTooLarge`
- `WorkspacePathDenied`
- `ArtifactWriteFailed`
- `CheckpointPersistFailed`
- `RuntimeStoreFailed`
- `SerializationFailed`
- `TemplateLoadFailed`
- `TemplateMaterializationFailed`
- `ExternalToolInvocationFailed`
- `McpServerUnavailable`
- `McpToolFailed`
- `ProviderUnavailable`
- `RateLimited`
- `ExternalServiceUnavailable`
- `PluginLoadFailed`
- `PluginDependencyMissing`
- `PluginActivationFailed`
- `PluginGrantMissing`
- `PluginOAuthMissing`
- `PluginOAuthExpired`
- `PluginSecretMissing`
- `PluginExecutionFailed`
- `Unknown`

## Boundary Rules

- Do not rely on `WorkflowFailureDisplayFormatter` parsing exception strings as the primary diagnostic contract. It may remain only as a backward-compatible display fallback while typed payloads are adopted.
- Do not create fallback execution paths for missing executors, unavailable plugins, missing grants, or failed package activation. Surface explicit unavailable descriptors or failed execution diagnostics.
- Do not log secrets, OAuth tokens, authorization headers, prompt payloads, email contents, file contents, or host-command sensitive arguments in user-facing diagnostics.
- Do not reduce plugin failures to "plugin failed" or executor failures to "executor start failed." The diagnostic must identify the known plugin/package/executor/tool context.
- Do not make diagnostics UI-only. API, event feed, audit sink, proof artifacts, and browser display must all consume the same typed failure payload.
- Do not make retryability implicit. It must be computed by a helper that is tested for timeout, rate limit, missing grant, invalid settings, cancellation, and external service failures.

## Subbundle Ownership

| Area | Owning subbundles | Required proof |
| --- | --- | --- |
| Base diagnostic contracts and builders | SB02 | Serialization compatibility, redaction boundaries, typed construction tests. |
| Validation/catalog diagnostics | SB03 | Invalid graph, unknown executor, invalid settings, invalid routing, and unsafe retry policy tests. |
| Runtime/event/store diagnostics | SB04 | Failure event payload, cancellation, timeout, artifact/checkpoint/store failure tests. |
| Foundation gate | SB05 | No-generic-error audit and diagnostic payload compatibility proof. |
| Executor diagnostic adapter and redaction helpers | SB06 | Exception-to-diagnostic, retryability, repair-hint, and secret masking tests. |
| Default executor categories | SB07 | Per-category failure matrix, including invalid settings, path denied, payload too large, timeout, cancellation, provider/service failure, and artifact failure. |
| Plugin executor boundary | SB08 | Package load, DI activation, grant, OAuth, secret, host-tool, and plugin execution diagnostic tests. |
| Executor/plugin gate | SB09 | Combined no-generic-error, redaction, source/trust/plugin/package context, and retryability proof. |
| Template diagnostics | SB10 | Template file, template key, workflow key, node id, executor id, YAML path, and setting path included in failures. |
| MAF adapter diagnostics | SB11 | Compiler, backend, LLM component, external tool/MCP, event normalization, and handoff failures mapped to typed diagnostics. |
| API/UI/Workbench display | SB12 | Component/browser proof shows user-safe message, repair hint, node/executor/plugin/tool context, and no raw secret leakage. |
| Adoption gate and closure | SB13-SB14 | No old string-parsing-only path remains except documented backward-compatible rendering of legacy payloads. |

## SB03 Diagnostic Execution Update

- Validation/catalog failures now use `WorkflowFailureDiagnosticMapper` to create typed `WorkflowFailureDiagnosticEnvelope` values with workflow or executor source context, retryability, repair hints, and redacted technical detail.
- Catalog save failures preserve exact `InvalidOperationException` compatibility for existing callers and tests, with diagnostics attached under `WorkflowFailureDiagnosticMapper.ExceptionDataKey`.
- `WorkflowFailureDisplayFormatter` now has a typed diagnostic overload; exception-string display remains only as backward-compatible rendering until SB12/SB13 adoption removes old UI/API paths.
- Runtime, executor, plugin, external tool, and UI diagnostic adoption remains owned by SB04-SB14.

## SB04 Diagnostic Execution Update

- Runtime backend start failures now use `WorkflowRuntimeFailureDiagnosticMapper` to attach typed runtime diagnostics with backend source context, workflow id/version, retryability, repair hints, and redacted technical detail.
- Runtime cancellation events now include a typed cancellation diagnostic serialized into the event payload inline JSON.
- Approval-denied external request responses now include a typed approval diagnostic serialized into the failure event payload inline JSON, while preserving the existing redacted run summary.
- Store failures remain explicit and are not hidden behind in-memory fallback; SB04 tests prove a store write exception propagates unchanged.
- Executor, plugin, external tool, MAF adapter, API/UI display, and Workbench diagnostic adoption remains owned by SB05-SB14.

## SB05 Diagnostic Hardening Update

- `WorkflowFoundationHardeningCheckpointTests` now guards typed diagnostic ownership in the foundation layer: diagnostic envelope, repair hint, redacted technical detail, validation/runtime exception data keys, and event payload redaction.
- Static diagnostics review proves the workflow foundation source has no loose `Dictionary<string, object>` diagnostic payloads and no generic error phrases such as "unknown error" or "something went wrong".
- File-size/responsibility review split diagnostic-adjacent runtime/catalog helper types into focused files so later executor/plugin diagnostics do not build on copied foundation monoliths.
- Executor, plugin, external tool, MAF adapter, API/UI display, and Workbench diagnostic adoption remains owned by SB06-SB14.

## SB06 Executor Diagnostic Update

- `WorkflowExecutorFailureDiagnosticMapper` now attaches typed `WorkflowFailureDiagnosticEnvelope` values to executor failures with retryability, repair hints, redacted technical detail, workflow/node/executor context, and source/plugin/package context where descriptor metadata exists.
- `WorkflowExecutorInvoker` now emits typed diagnostics for missing executor id, unregistered executor, unavailable descriptor, missing implementation, approval gate missing, approval denied, timeout, payload too large, and unexpected invocation failures while preserving exact exception compatibility for existing callers.
- Executor redaction, payload caps, audit scope, JSON helpers, and policy/side-effect helpers now live in executor core instead of MAF/Core-owned workflow files.
- Plugin and default category-specific diagnostics remain owned by SB07-SB09; API/UI/Workbench rendering remains SB12-SB13.

## SB07 Default Executor Diagnostic Update

- Default executor implementations now live in category projects while preserving existing executor failure behavior, descriptor metadata, side-effect descriptors, deterministic preview behavior, cancellation, timeout, payload cap, and policy tests.
- Category isolation proof verifies MAF no longer has a fallback bucket for moved default executors, so default executor failures continue through executor core diagnostics instead of hidden adapter-local behavior.
- Source Ingestion and Project Structure helper splits keep path/input resolution, provider calls, result shaping, and support logic separable for SB09 diagnostic hardening.
- Plugin-specific diagnostics, package adapter failures, grant/OAuth/secret failures, host-tool diagnostics, and UI/API diagnostic rendering remain SB08-SB13 work.

## SB08 Plugin Executor Diagnostic Update

- Runtime package executor activation now raises `PluginWorkflowExecutorActivationException` with plugin id, package id, executor type name, and operation context.
- Plugin descriptor projection preserves source/trust, grant/OAuth availability, permission policy, side-effect descriptors, and deterministic test-mode metadata through the executor descriptor catalog.
- Package loading still reports startup/package manifest failures explicitly from the plugin module; executor wrapping/source mapping now lives in the plugin executor boundary.
- Bundled plugin proof covers Docker host-tool executor builds, Gmail/Office365 OAuth payload and side-effect receipt behavior, plugin package catalog integration, and source/trust descriptor metadata.
- SB09 remains responsible for combined plugin/default no-generic-error hardening, finer plugin failure classification, retryability classification, audit event review, and redaction review before UI/API adoption.

## SB09 Executor And Plugin Diagnostic Hardening Update

- `PluginWorkflowExecutorActivationException` now exposes a strongly typed activation failure kind, retryability, repair hint, and redacted technical detail in addition to plugin id, package id, executor type name, and operation.
- `WorkflowExecutorHardeningCheckpointTests` proves plugin invocation failures carry workflow node id, executor id, plugin id, package id, retryability, repair hint, and redacted technical detail through executor core diagnostics.
- Combined descriptor parity proof covers default, bundled plugin, runtime package, and Cognitive Memory feature-module executor descriptors before template loading consumes descriptor metadata.
- No-generic-error and anti-stub scans now cover executor core, plugin boundary, default category projects, plugin module package/grant surfaces, and bundled plugin executors.
- UI/API display of these typed diagnostics remains SB12/SB13 work, but the executor/plugin diagnostic contract is hardened for downstream adoption.

## SB10 Template Diagnostic Update

- Template loading and preview fixture parsing now throw `WorkflowTemplatePackException` with `WorkflowTemplateDiagnostic` instead of silently skipping invalid templates.
- Template diagnostics include failure kind, template file, template key, workflow key, YAML path, node id, executor id, and a repair hint when that context is known.
- Descriptor-aware validation reports missing executors and invalid executor settings against the executor catalog proven by SB09.
- Negative tests cover malformed YAML, missing executor, invalid routing, invalid input parameter, invalid runtime policy, invalid executor settings, and malformed preview simulation JSON.
- UI/API display of template diagnostics remains SB12/SB13 work; SB10 proves the typed diagnostic source contract is available before those surfaces adopt it.

## SB11 MAF Adapter Diagnostic Update

- MAF compile failures now use `MafWorkflowAdapterFailureDiagnostics` to create typed `WorkflowFailureDiagnosticEnvelope` payloads with workflow id, version id, run id, runtime backend source, retryability, redacted technical detail, repair hint, and correlation id.
- Validation-driven compile failures reuse the workflow validation diagnostic mapper and add backend context; non-validation compile failures are classified as runtime-backend failures with repair guidance.
- `WorkflowCompilationFailed` events serialize the typed diagnostic inline while preserving the existing failed run/checkpoint state.
- Host/module composition no longer keeps a legacy MAF built-in executor alias, so missing executor/plugin/tool failures remain explicit through executor catalog and plugin diagnostics rather than hidden by fallback registration.
- UI/API display of MAF adapter diagnostics remains SB12/SB13 work; SB11 proves the adapter source contract and runtime payload are available before adoption.

## SB12 API UI Workbench Diagnostic Update

- `WorkflowFailureDisplayFormatter` now resolves `WorkflowFailureDiagnosticEnvelope` values from `WorkflowEventRecord.PayloadJson` and returns typed user-safe messages before falling back to redacted legacy message text.
- The workflow page event list, event detail, failed run summary, and technical detail sections consume typed event diagnostics through the formatter rather than displaying raw event messages.
- The workflow canvas editor, Workbench workflow-node dialogs, and Workbench cached workflow metadata failure display route exception-derived messages through the shared formatter for redaction.
- Workbench workflow-node status summaries now carry runtime event payload JSON and prefer the latest typed error or executor-failed event diagnostic over message-only summaries.
- SB12 component/unit proof covers typed diagnostic rendering and Workbench status resolution; large-screen browser proof covers workflow shell and Workbench workflow-node adoption paths.
- SB13 remains responsible for the adoption hardening checkpoint: no hidden fallback paths, no generic UI/API/Workbench failure display, file-size/responsibility review, and focused performance scan.

## SB13 Adoption Hardening Diagnostic Update

- `WorkflowAdoptionHardeningCheckpointTests` now guards typed diagnostic display adoption by rejecting raw workflow event message display, message-only Workbench status assignments, and UI/Workbench-local `WorkflowFailureDiagnosticEnvelope` deserialization.
- The no-generic-error audit passed for workflow API/UI/Workbench adoption files. Raw legacy message fallback remains allowed only inside `WorkflowFailureDisplayFormatter`, where it is redacted and treated as backward-compatible display behavior.
- The architecture/no-fallback audit passed for API/UI/Workbench adoption files, so diagnostic display no longer depends on direct MAF compiler/backend/event/LLM fallback paths.
- The file-size/responsibility review records pre-existing large UI files as approved exceptions and verifies non-trivial diagnostic parsing remains centralized in workflow core rather than copied into Blazor pages or Workbench files.
- SB14 final closure must keep these guard tests and document diagnostic conventions for future workflow, executor, plugin, UI, and Workbench additions.

## SB14 Final Diagnostic Closure Update

- Final diagnostics ownership is documented in `docs/workflow-maf-hardening.md`: `WorkflowFailureDiagnosticEnvelope` is the shared contract, typed diagnostics are preferred over exception-message parsing, and `WorkflowFailureDisplayFormatter` is the only UI/Workbench display boundary for redacted fallback text.
- Future validation, runtime, executor, plugin, template, MAF adapter, external tool/MCP, API, UI, and Workbench failures must preserve retryability, repair hint, redacted technical detail, and the most specific available workflow/node/executor/plugin/package/tool/operation/backend context.
- Missing executor, unavailable plugin, failed package activation, missing grant/OAuth/secret, unavailable durable backend, artifact write, checkpoint write, and external tool/MCP failures must fail explicitly. Silent fallback behavior is not an acceptable regression.
- Blazor pages and Workbench code must not deserialize diagnostic envelopes or duplicate diagnostic parsing. If new display state is needed, extend workflow core display helpers and prove it with unit/component/browser coverage.
- Existing legacy message fallback remains allowed only for historical events and only after redaction. New runtime events must carry typed payload diagnostics.
