# CanDoItAll.Modules.Processes

## Purpose

Canonical process runtime module for templates, process runs, step transitions, work briefs, governed outcomes, artifacts, and AI-agent dispatch.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.Components.Mermaid/CanDoItAll.Components.Mermaid.csproj`
- `../CanDoItAll.Components.WebGlLib/CanDoItAll.Components.WebGlLib.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Modules.Activity/CanDoItAll.Modules.Activity.csproj`
- `../CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `JsonViewer.Blazor (0.0.6)`
- `Markdig (1.1.2)`
- `Microsoft.AspNetCore.Components.Web (10.0.5)`

## Architecture Notes

Processes owns the lifecycle above Workflows and Agent Framework execution. A process definition describes roles, step contracts, dependencies, artifact expectations, and governance rules. A process run owns step state, assignments, artifacts, manager directives, recovery, read models, and operator diagnostics. Workflows and agents execute underneath that boundary; they do not replace process lifecycle ownership.

The runtime is organized around these surfaces:

- **Definition and template authoring**: `ProcessesService`, definition child partials, `ProcessDefinitionLinter`, and the template catalog services load and validate process definitions, step operation contracts, artifact expectations, branch outcomes, and role requirements.
- **Launch planning and staffing**: `ProcessesService.Launch.*` builds launch plans, resolves candidate roles, applies process-manager overrides, and preserves project-structure launch context.
- **Run lifecycle and transitions**: `ProcessesService.Runtime.*`, `ProcessRuntimeProgressionPlanner`, `ProcessStepTransitionGuard`, and `ProcessStepRunBlockState` own run start, step transitions, stop/rerun, dependency compatibility, and block-state classification.
- **Dispatch and finalization**: `ProcessRunAutomationDispatchService.*` claims executable steps, builds execution metadata and prompts, validates tool outputs, projects artifacts, finalizes step completion, and creates recovery packets. External target grounding and stale path inspection are delegated to `ProcessExternalTargetGroundingService` so prompt rules, invocation metadata, and final-delivery validation consume the same typed normalization path.
- **Agent runtime tool provider**: `ProcessAgentRuntimeToolProvider` owns the 23 direct process tools exposed to MAF through `IAgentRuntimeToolProvider`. `ProcessesModuleServiceCollectionExtensions` registers it with DI. MAF composes it when the Processes module is loaded; MAF does not own process tool construction or reference this module directly. The provider is purpose-aware: read tools require process read access, mutation tools require explicit process write access, and no-access contexts expose no process tools.
- **Artifacts and lineage**: `ProcessArtifactIdentityService`, `ProcessArtifactProjectionLineage`, artifact projection/finalizer code, and runtime read queries own identity hash, content hash, lineage, projection source, trust status, and expectation satisfaction. Retention remains definition-driven through artifact expectation `RetentionDays`; cleanup must be explicit, dry-run first, and must preserve lineage metadata needed to explain stale, duplicate, or hash-mismatched evidence.
- **Observation and manager chat**: `ProcessObservationService`, `ProcessObservationDashboardState`, `ProcessManagerChatService`, and `ProcessManagerAgentResolver` expose run inspection, manager directives, chat, approvals, diagnostics, selected-run context, and explainable manager-agent resolution.
- **Project-structure integration**: `IProcessProjectStructureBridge`, `ProcessProjectStructureContext`, and the Workbench bridge synchronize process runs into project-structure nodes and feed grounded launch/output context back into Processes.
- **Operator UI**: `ProcessWorkspace*`, `LiveProcessesDashboard`, and run-detail components render authoring, launch, active runs, artifacts, assignments, execution telemetry, manager chat, and operator recovery surfaces.
- **Background runtime**: `ProcessCatalogWarmupWorker`, `ProcessRunRecoveryWorker`, and `ProcessOutboxDrainWorker` run only when the local runtime lane allows hosted workers.

Keep business behavior inside typed services and module contracts. MCP tools, Workbench bridges, templates, and UI components should call those services instead of duplicating runtime rules.

## Refactor Boundaries

The current source already has useful service boundaries, but several cross-cutting policies still deserve tighter ownership:

- **Artifact status projection** should be one typed mapping consumed by finalizer validation, runtime read models, health/recovery classification, API projection, and UI detail loaders.
- **Artifact identity and storage** should stay behind `ProcessArtifactIdentityService` and projection-lineage helpers, with race/recovery tests proving stale or invalid records cannot block later valid artifacts.
- **Output grounding** stays behind `ProcessExternalTargetGroundingService`; new consumers should use its typed target, scaffold, alias-pruning, stale-reference inspection, and prompt-redaction results instead of parsing external-target text locally.
- **Run folder projection** stays behind Workbench `ProjectStructureProcessRunFolderProjectionPolicy`, which projects current-run managed roots and generated or external-delivery output roots while collapsing or ignoring tool receipt internals and noisy child folders.
- **Manager resolution** stays behind `ProcessManagerAgentResolver`, which returns reason codes, confidence, candidate summaries, and ambiguity diagnostics while keeping configured and selected-run assignments ahead of fallback scoring.
- **Proof/test harness** should use named process-runtime slices instead of one broad timeout-prone filter.

Avoid introducing interfaces for trivial one-implementation helpers. Add an abstraction only when it protects a real module boundary, improves testability, or removes meaningful duplication.

## Runtime Invariants

- Processes own lifecycle, dependencies, artifacts, recovery, and closure.
- Workflows and agents execute process steps under Processes.
- Required artifacts cannot be treated as satisfied when validation reports missing, invalid, stale, wrong-producer, placeholder-only, unavailable, or hash-mismatched evidence.
- Artifact cleanup must not delete the only lineage record for a required artifact unless the owning expectation retention policy has expired and the operation records auditable cleanup evidence.
- Final delivery proof may reference an external target only when project-structure grounding produced a credible current-run target.
- Manager chat must prefer configured manager and selected-run assignment before fallback.
- Project-structure projection should expose navigable run/product folders without mirroring every artifact subdirectory.
- Live-run profiles must not seed transitions or artifacts that a real runtime path should produce.

## Supported Process Launch Surfaces

Supported UI routes:

- `/processes` opens the global process workspace.
- `/projects/{projectId}/processes` opens the process workspace scoped to a project.
- `/processes/live` and `/projects/{projectId}/processes/live` open the live process dashboard.

Supported HTTP API launch paths are under `/api/processes`:

- `POST /runs/start` starts a published definition directly through `ProcessesService.StartRunAsync`.
- `POST /launch-plans` creates a launch plan through `ProcessesService.CreateLaunchPlanAsync`.
- `POST /launch-plans/{launchPlanId}/hr-match`, `POST /launch-plans/{launchPlanId}/submit-approval`, `POST /launch-plans/approval-decisions`, and `POST /launch-plans/{launchPlanId}/provision` prepare a launch plan for execution.
- `POST /launch-plans/execute` executes a ready launch plan through `ProcessesService.ExecuteLaunchPlanAsync`, which delegates to the normal run-start path.
- `GET /templates`, `GET /templates/{processKey}/detail`, and `POST /templates/{processKey}/import` expose the supported template library and import path.

Supported project-structure launch path:

- `POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start` starts a linked process node through `ProjectStructureAgentService.StartProcessNodeAsync` and the process bridge. Use this when the launch must preserve project-structure context.

Supported service-level trigger path:

- Scheduler and workflow-origin starts use `ProcessesService.StartRunFromTriggerAsync(ProcessRunTriggerStartRequest)`, which validates `ProcessRunTriggerSourceKind`, requires source identity for scheduler/workflow triggers, and delegates to `StartRunAsync`.

Current support is intentionally process-service centered. Do not route UI, API, project-structure, scheduler, workflow, manager, or agent-tool starts through a process-driver runtime host, driver registry, runtime selector, or driver dependency-injection registration.

## Process Driver Read-Only Verification Migration

The current process-module integration path is `ProcessReadOnlyVerificationBatchOrchestrator.Verify(ProcessReadOnlyVerificationBatchPayload)`. It composes the focused read-only adapters directly and returns typed observation lists plus an aggregate observation when at least one lane produced a response.

Use `ProcessReadOnlyVerificationPayloadBuilder` to build lane payloads from already-loaded, caller-supplied facts:

- `CreateTranscriptPayload` feeds `ProcessTranscriptVerificationReadOnlyAdapter`.
- `CreateRuntimeEvidencePayload` feeds `ProcessRuntimeEvidenceVerificationReadOnlyAdapter`.
- `CreateArtifactEvidencePayload` feeds `ProcessArtifactEvidenceReadOnlyAdapter`.
- `CreateOfficeEvidencePayload` feeds `ProcessOfficeEvidenceReadOnlyAdapter`.
- `CreateBusinessAnalysisPayload` feeds `ProcessBusinessAnalysisReadOnlyAdapter`.

Migration from lane-by-lane adapter calls should keep request construction typed: create the relevant lane payloads, pass them into `ProcessReadOnlyVerificationBatchPayload`, then call `ProcessReadOnlyVerificationBatchOrchestrator.Verify`. Consume the returned lane observation lists, `Responses`, and `AggregateObservation` as diagnostic read-only evidence only.

For operator-facing verification diagnostics, use `IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync` over the same read-only payload shape. `ProcessManagerReadOnlyVerificationReadbackDto` is the current source-backed readback contract and must carry process-run id, step-run id, caller context, projection metadata, diagnostics, `auditRecords`, `observationHash`, `denialCategory`, `denialCode`, `denialMessage`, `noMutationPerformed = true`, and false mutation permission flags.

For host readiness, use `IProcessVerificationRuntimeHostStatusService.GetStatusAsync` or the manager facade status readback. The status contract reports correlation id, requester, enabled/emergency-disabled state, lane registration, lane enablement, audit-store kind, durable-audit use, and false mutation permission flags. It is operator readback only; it does not approve an execution-capable runtime host.

Scheduler/workflow read-only verification jobs use `IProcessReadOnlyVerificationJobRunner.RunAsync`. The runner converts `ProcessReadOnlyVerificationJob` into the manager readback request and executes through `IProcessManagerReadOnlyVerificationFacade`; scheduler and workflow modules must not call domain drivers or the batch orchestrator directly.

Future execution-capable driver readiness is represented by `ProcessExecutionCapableDriverSandboxPolicy.DefaultBlockedDryRun`, `ProcessExecutionCapableDriverFutureGate`, and `IProcessDryRunExecutionHost`. The default policy is dry-run-only, has no allow-listed surfaces, and denies command execution, package restore, file access, workspace/storage writes, network/HTTP, Office/Graph, CRM, provider repair, finalizer application, transition mutation, claim mutation, retry scheduling, and process mutation until a separate source-backed approval bundle satisfies every typed gate requirement. `IProcessDryRunExecutionHost` returns a structured dry-run plan and authorization gaps; it never performs production effects.

`ProcessVerificationHostFailureCategory` and `ProcessVerificationHostDenialCode` are troubleshooting taxonomy. They do not approve execution-capable drivers, dependency-injection registration, scheduler/workflow hooks, manager commands, external calls, storage/workspace writes, finalizer application, transition application, claim mutation, retry scheduling, or process mutation.

Do not add a generic runtime host, driver registry, runtime selector, dependency-injection registration, manager command, scheduler hook, workflow hook, file/network access, storage/workspace write, or process mutation while migrating to the batch orchestrator.

## Runtime Host Roadmap Decision

Current status: `Not approved`.

The process runtime now has source-backed proof for UI start, run persistence, dispatch/finalizer behavior, deterministic software and business-analysis scenarios, manager-visible read-only projection, and scheduler/workflow-origin process starts. That proof makes read-only verification projection useful, but it does not approve a generic process-driver runtime host.

Release-candidate validation refreshed on 2026-06-10:

- `dotnet build CanDoItAll.slnx --configuration Debug` passed with 0 warnings and 0 errors.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build` passed with 1,136 tests.
- A focused verification host/readback/security integration matrix passed with 34 tests.
- A focused operator API smoke passed with 2 tests for manager diagnostics readback and process-run detail verification audit readback.
- The existing focused Playwright matrix remains the UI proof at 1900x1200 for global process start, blocked run recovery readback, and project-structure output navigation; no UI route changed for the verification-host operator smoke API path.
- Source scans found no active bundle-path leakage and no process driver runtime host, registry, selector, manager command, endpoint mapping, scheduler hook, workflow hook, or driver mutation surface.

Approved current shape:

- Processes may call typed read-only verification adapters over already-loaded caller-supplied facts.
- Manager-visible diagnostics and verification audit readback may project those read-only observations without process mutation.
- Scheduler and workflow-origin process starts must use typed process services, not driver hooks.

Future approval gate:

- Define a runtime lifecycle owner, startup/shutdown behavior, cancellation model, retry ownership, failure handoff, and observability contract.
- Define immutable audit persistence for each request, denial, approval, output hash, and redaction descriptor.
- Define sandbox and allow-list policy for command execution, package restore, file access, workspace/storage writes, network/HTTP calls, Office/Graph calls, CRM calls, provider repair, finalizer application, transition application, claim mutation, retry scheduling, and process mutation.
- Define approval and authorization with recorded enablement, revocation, dry-run behavior, emergency stop, and predictable failure behavior.
- Update driver contract versioning, public API snapshots, migration docs, focused tests, source scans, and critical proof manifests in the same approval bundle.
- Include red-team proof that rejects report-only approval, non-empty diagnostics as approval, implicit DI registration, fallback runtime selection, fixture-only success, and undocumented manager/scheduler/workflow entry points.

Denied until that future gate passes:

- Generic process-driver runtime host, driver registry, runtime selector, dependency-injection registration, manager command, scheduler hook, workflow hook, endpoint mapping, workspace/storage writes, external calls, process mutation, or execution-capable drivers.

## Driver/Core/Runtime Roadmap Matrix

Ready now:

- Process Core remains generic and dependency-light. Domain-specific evidence verification belongs in process-module read-only adapters and `CanDoItAll.Processes.Drivers.*` packages.
- Read-only verification adapters may inspect already-loaded facts and return diagnostics, observations, redaction descriptors, evidence references, and aggregate summaries.
- The process module may project read-only verification diagnostics into manager-visible observations without applying transitions, claims, finalizers, retries, workspace writes, external calls, or process mutations.
- UI, API, project-structure, scheduler, workflow-origin, and agent-tool starts must enter through `ProcessesService` or the existing project-structure bridge.

Blocked now:

- A generic process-driver runtime host is not approved.
- Driver runtime discovery, registry lookup, selector fallback, driver DI registration, manager commands, scheduler hooks, workflow hooks, endpoint mappings, shell execution, package restore, Office/Graph calls, CRM writes, workspace/storage writes, transition mutation, claim mutation, finalizer mutation, and retry scheduling remain blocked.
- A non-empty diagnostic result, a green test fixture, or a report-only note is not approval to execute driver side effects.

Future gate:

- Approve runtime ownership, audit persistence, sandbox and allow-list policy, authorization, emergency-stop behavior, compatibility/versioning, source scans, API snapshots, migration docs, focused tests, and red-team proof in the same bundle before enabling execution-capable process drivers.
- Update this README, process API/operator docs, source guards, critical manifests, and validation transcripts in the approval bundle.

## Operator Troubleshooting Map

Use current-run readbacks before changing state. Start with run detail and narrow to step-scoped routes only after identifying the failing step or artifact expectation.

| Symptom | Source-backed readback | Expected operator action |
| --- | --- | --- |
| Required output appears complete but the run is still blocked | Read run detail health, `invariantDiagnostics`, step `blockReasonCode`, `recoveryOptions`, and artifact status fields projected by `ProcessArtifactStatusProjectionService`. | Treat missing, invalid, stale, wrong-producer, placeholder-only, unavailable, or hash-mismatched records as unsatisfied. Record a new current-run artifact instead of editing status text. |
| Final delivery points to an external target or managed output folder | Inspect grounding metadata from `ProcessExternalTargetGroundingService`, including normalized target, scaffold, alias pruning, stale-reference inspection, and prompt-redacted paths. | Accept final delivery only when grounding found a credible current-run project-structure target or managed output root. Escalate ungrounded external aliases. |
| Manager chat answers the wrong run or agent | Inspect `ProcessManagerAgentResolver` reason code, confidence, selected-run assignment, configured manager, fallback candidates, and ambiguity diagnostics. | Prefer configured manager, then selected-run assignment, then fallback. Resolve ambiguity before sending a manager directive or direct message. |
| Project-structure output is too noisy or points to stale receipts | Inspect Workbench projection through `ProjectStructureProcessRunFolderProjectionPolicy`. | Project current-run managed roots and generated or external-delivery output roots; ignore wrong-run, dated receipt, traversal, absolute, and unanchored paths. |
| A live UI-driven run looks seeded | Read the selected `ProcessTemplateLiveRunProfile` or `ProcessTemplateLiveRunProfileSummary.FreshRunPolicy`. | Reject baseline transitions/artifacts as live proof. Require current-run evidence checks before validation and project-structure writeback. |
| Process tools are absent from an AgentFramework run | Inspect runtime DI for `IAgentRuntimeToolProvider`, confirm `ProcessAgentRuntimeToolProvider` is registered, check MAF progress for the provider key/display name and expected 23-tool attachment, and inspect receipt or trace `RuntimeToolProviderKey` when available. | Fix module/provider registration or agent process access metadata. Do not add a direct MAF reference to Processes. |
| Verification host diagnostics are missing or denied | Inspect `ProcessManagerReadOnlyVerificationReadbackDto` for `diagnosticCount`, `auditRecords`, `observationHash`, `denialCategory`, `denialCode`, `denialMessage`, and mutation-denial flags. | Treat the result as read-only troubleshooting evidence. Fix payload, lane, or host policy issues; do not register drivers, invoke a runtime host, or mutate process state. |

## Migration Notes And Open Blockers

Current migrations should use the process-owned service and read-only verification shape described above. Do not migrate callers to a process-driver runtime host.

Open blockers before execution-capable drivers can be considered:

- Runtime ownership, cancellation, retry ownership, failure handoff, and observability are not approved for driver-hosted execution.
- Sandbox and allow-list policy are not defined for shell commands, package restore, file/network access, Office/Graph/CRM calls, workspace/storage writes, finalizer application, transition mutation, claim mutation, or retry scheduling.
- Authorization, approval, revocation, emergency stop, dry-run behavior, audit persistence, and redaction evidence are not defined for driver-side mutation.
- Public API/versioning, migration documentation, source guards, focused tests, and red-team proof for execution-capable drivers must be delivered in a future approval bundle.

For API or agent-driven operation, prefer the HTTP `/api/processes` routes and the `candoitall-api-processes` skill. The old Processes MCP server is not the current control plane.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Process template pack: `Templates/Processes/README.md`
- Process API skill: `codex/skills/candoitall-api-processes/SKILL.md`
