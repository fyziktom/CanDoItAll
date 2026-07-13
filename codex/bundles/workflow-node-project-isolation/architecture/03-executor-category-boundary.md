# Executor Category Boundary

## Executor Ownership Rule

Executors must have their own contracts, helpers, and implementation projects. MAF may adapt workflow execution to Microsoft Agents, but it must not own executor descriptors or default executor implementation classes.

## Proposed Default Executor Categories

| Category project | Current examples | Notes |
| --- | --- | --- |
| `WorkflowExecutors.Standard.Control` | `DelayWorkflowExecutor`, `HumanApprovalWorkflowExecutor`, `PlannedWorkflowExecutor` | Should own approval metadata and external request handoff rules. |
| `WorkflowExecutors.Standard.Transforms` | `JsonTransformWorkflowExecutor`, `MarkdownRenderWorkflowExecutor` | Should own JSON/markdown settings, generated regex helpers, and schema helpers. |
| `WorkflowExecutors.Standard.Workspace` | `WorkspaceFileWorkflowExecutor`, `SourceIngestionWorkflowExecutor` | Must preserve workspace path scope and artifact behavior. |
| `WorkflowExecutors.Standard.Network` | `HttpFetchWorkflowExecutor` | Must preserve network permissions, timeout, response limits, and side-effect metadata. |
| `WorkflowExecutors.Standard.Documents` | `SpreadsheetWorkflowExecutor` | Must keep spreadsheet/document dependencies and IO helpers out of generic transform projects. |
| `WorkflowExecutors.Standard.Media` | `ImageGenerationWorkflowExecutor` | Must preserve image/provider permissions, timeout, artifact capture, deterministic preview, and provider failure diagnostics. |
| `WorkflowExecutors.Standard.ProjectStructure` | `ProjectStructureWorkflowExecutor` | Should depend on a narrow project-structure runtime gateway contract, not Workbench UI. |
| `WorkflowExecutors.Plugins` | Docker, Gmail, Office365, runtime package executors | Must preserve plugin grants, source/trust metadata, secret/OAuth behavior, host-tool boundaries, and deterministic preview. |

## Shared Executor Helpers

Move or create helpers for:

- serializer options and JSON parsing;
- settings schema creation;
- descriptor source composition;
- catalog duplicate detection and diagnostics;
- payload redaction and secret masking;
- output caps and artifact capture policies;
- side-effect approval enforcement;
- deterministic test-mode simulation checks;
- source/trust/availability descriptor mapping.
- typed failure diagnostics and exception-to-diagnostic mapping;
- retryability classification and repair-hint creation;
- no-generic-error assertions for executor, tool/MCP, and plugin failures.

## SB06 Executor Foundation Update

- `CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions` now owns executor contracts and audit contracts.
- `CanDoItAll.AgentFramework.WorkflowExecutors.Core` now owns catalog composition, invoker behavior, descriptor factory/settings schema helpers, JSON helper, redaction/observability helpers, payload policy, side-effect policy, policy limits, typed executor diagnostics, retryability classification, repair hints, and executor DI registration.
- Built-in descriptor materialization and Cognitive Memory descriptor materialization now use `WorkflowExecutorDescriptorFactory`.
- SB07 resolved concrete default executor ownership by moving the built-in implementations into standard category projects. Plugin package/adapters remain in `CanDoItAll.Modules.Plugins` until SB08.

## SB07 Default Category Update

- `CanDoItAll.AgentFramework.WorkflowExecutors.Standard` now composes the seven standard category projects through `AddStandardWorkflowExecutors(...)`.
- Category projects own their concrete registrations and descriptor sources: Control, Transforms, Workspace, Network, Documents, Media, and ProjectStructure.
- MAF no longer owns concrete default executor files or built-in default executor registration details; it delegates to the standard aggregate registration.
- `CanDoItAll.Modules.AgentFramework` uses the same aggregate with scoped lifetime so module composition does not duplicate executor registrations.
- `BuiltInWorkflowExecutorDescriptors` and `WorkflowInputPayloadText` are executor-core helpers used by category projects, not MAF implementation details.
- `SourceIngestionWorkflowExecutor` and `ProjectStructureWorkflowExecutor` were split into focused helper files instead of being copied wholesale into category projects.
- Static ownership proof now blocks a new `WorkflowExecutors.Standard` monolith or direct MAF fallback for moved default executors.

## SB08 Plugin Boundary Update

- `CanDoItAll.AgentFramework.WorkflowExecutors.Plugins` now owns plugin executor descriptor projection, source/trust mapping, runtime package executor wrapping, and runtime package descriptor source registration.
- The boundary consumes `IPluginWorkflowExecutorGrantEvaluator` so grant/OAuth availability can remain strongly typed without referencing the plugin module persistence layer.
- `CanDoItAll.Modules.Plugins` keeps package installation, manifest stores, load-context discovery, grants, OAuth, audit persistence, and UI pages.
- Runtime plugin package scanning delegates discovered executor type registration to `PluginWorkflowExecutorRuntimeRegistration`; no MAF-only plugin executor fallback remains.
- Runtime package activation failures now carry plugin id, package id, executor type, operation, failure kind, retryability, repair hint, and redacted technical detail through `PluginWorkflowExecutorActivationException`.
- SB09 hardened combined default/plugin diagnostics, redaction, retryability, source context, file responsibility, and serializer performance before templates consume the descriptor layer.

## SB09 Executor Hardening Update

- `WorkflowExecutorHardeningCheckpointTests` proves combined descriptor parity and source context across default category descriptors, bundled plugin descriptors, runtime package descriptors, and Cognitive Memory feature-module descriptors.
- The hardening gate blocks MAF workflow fallback by asserting the MAF workflow folder contains only adapter/compiler/backend files.
- Category executor files are bounded by the SB09 responsibility line limit, with larger Source Ingestion and Project Structure logic kept split across helper files.
- Gmail and Office365 bundled workflow executors now share a single static serializer-options helper per plugin workflow file.
- Plugin activation and invocation diagnostics now include retryability, repair hints, redacted technical detail, and plugin/package context before SB10 template work starts.

## Plugin Consequences

- Bundled plugin executors currently implement `IWorkflowExecutor` directly. They should migrate to executor abstractions only after a compatibility bridge is available.
- Runtime plugin package discovery currently scans for `IWorkflowExecutor`. During migration, support old and new executor interfaces explicitly, with diagnostics that identify package id, plugin id, type name, and required restart state.
- Plugin manifest descriptors must stay compatible with `PluginWorkflowExecutorDescriptor`.
- `PluginWorkflowExecutorDescriptorSource` must remain semantically equivalent: source kind, package id, trust level, icon, grant availability, side effects, and deterministic test-mode metadata.
- Gmail and Office365 mark-processed executors are production side-effect paths. Positive proof must exercise production producer/lifecycle paths, not only seeded fixtures.
- Docker host command executors require approval and host command grant proof.
- Plugin load, DI activation, grant, OAuth, host-tool, MCP/tool, and executor execution failures must be surfaced as unavailable descriptors or failed executions with package id, plugin id, executor id, type name, operation/tool name, retryability, redacted technical detail, and repair hint.

## Anti-Patterns To Block

- A single `WorkflowExecutors.Standard` project becoming a new monolith without category folders/tests.
- Collapsing network, document, and media executors into one project when dependencies differ materially.
- Default executor descriptor ids copied into UI constants instead of descriptor catalog.
- Plugin grant failures represented as missing executors instead of unavailable descriptors with explicit reasons.
- Preview simulations that share production mutation code paths.
- JSON helper code repeated in every executor category.
- Exceptions converted to generic messages such as "executor start failed" or "plugin execution failed" without source, node, executor, plugin/package/tool, and repair context.
