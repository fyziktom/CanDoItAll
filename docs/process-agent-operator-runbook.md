# Process Agent Operator Runbook

## Scope

This runbook covers process runs that use AgentFramework-backed steps and need human operation through the process workspace control plane.

## Triage Order

1. Open the process run and review the Control tab.
2. Check open escalations first. Journal-backed escalations can be assigned, resolved, reopened, or converted into rework.
3. Check pending approvals. Approve only when the tool details and process context match the intended work. Reject or request changes when the action is unclear, unsafe, or outside the process contract.
4. Check dead-lettered automation dispatch records. Treat them as failed automation evidence until the underlying error is understood.
5. Use the attempt timeline to reconstruct execution runs, approvals, outbox dispatch, recovery decisions, rework packets, and manual reruns.

## Current Runtime Status

As of 2026-06-10, process-owned runtime paths have source-backed release-candidate proof for global UI launch, project-scoped launch, project-structure process start/output navigation, durable run lifecycle, outbox dispatch/finalization, artifact readback, deterministic software and business-analysis processes, scheduler/workflow-origin starts, read-only manager diagnostics, failure triage, operator health readback, and verification audit readback.

The generic process-driver runtime host remains not approved. Operators should not use driver packages, driver registries, runtime selectors, manager commands, scheduler hooks, workflow hooks, or driver dependency-injection registration to start or mutate process runs.

## API Read Model

Use `GET /api/processes/runs/{runId}/detail` as the canonical operator read route. `ProcessRunDetailApiQuery` supports focused filters by step run, step definition, role requirement, party, artifact, artifact expectation, agent, workflow run, workflow definition, workflow version, step status, artifact kind, execution state, workflow state, search text, and take count.

The detail query also has explicit include switches:

- `includeDecisions`
- `includeArtifacts`
- `includeOutboxRecords`
- `includeAssignments`
- `includeWorkBriefs`
- `includeConformanceObservations`
- `includeDirectMessages`
- `includeExecutionRuns`
- `includeWorkflowRuns`
- `includeEscalations`
- `includeOperatorApprovals`
- `includeAttemptTimeline`

Use `GET /api/processes/runs` for list views. `ProcessRunListApiQuery` supports `definitionId`, `projectId`, `status`, `operatingMode`, `search`, and `take`.

## Verification Host Beta Operator Readback

Use the manager readback contract for verification-host beta diagnostics. The current source-backed API contract is `IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync`, which projects `ProcessManagerReadOnlyVerificationReadbackDto` for operator-facing readback. Do not route this through a runtime driver host.

Operator readback must preserve these fields:

- `processRunId`, `stepRunId`, `callerContext`, projection mode/source, response count, diagnostic count, and diagnostics.
- `auditRecords` with audit record id, lane, response count, accepted count, denied count, `observationHash`, and mutation-denial flags.
- `denialCategory`, `denialCode`, and `denialMessage` for denied verification attempts.
- `noMutationPerformed = true`, `allowsProcessMutation = false`, `allowsTransitionMutation = false`, and `allowsFinalizerMutation = false`.

Treat `ProcessVerificationHostFailureCategory` and `ProcessVerificationHostDenialCode` as troubleshooting classification only. A non-empty diagnostic list, audit record, or denied readback is not approval to execute drivers, mutate process state, call external systems, write workspace/storage, or register drivers in dependency injection.

Use `IProcessVerificationRuntimeHostStatusService.GetStatusAsync` or the manager facade status method when an operator needs host readiness. Status readback is limited to enabled/emergency-disabled state, lane registration and enablement, durable audit-store classification, and mutation-denial flags.

Scheduler and workflow read-only verification jobs must run through `IProcessReadOnlyVerificationJobRunner.RunAsync`, which delegates to `IProcessManagerReadOnlyVerificationFacade.VerifyForReadbackAsync`. Do not call domain drivers, `ProcessReadOnlyVerificationBatchOrchestrator`, or runtime-host internals from scheduler/workflow modules.

The future execution-capable driver sandbox contract is currently `ProcessExecutionCapableDriverSandboxPolicy.DefaultBlockedDryRun`. It is dry-run-only and has no allow-listed effectful surfaces. `ProcessExecutionCapableDriverFutureGate` must return blocked until a separate source-backed approval bundle proves lifecycle ownership, immutable audit, sandbox/allow-list policy, authorization/revocation, compatibility, malicious corpus, and red-team proof.

## Failure Triage

Blocked and failed steps expose typed recovery state. Read `blockReasonCode`, `nextRecoveryAction`, `recoveryOptions`, run `health.recommendedAction`, invariant diagnostics, outbox health, escalations, and the attempt timeline before changing state.

Use these operator actions:

- `RecoverArtifactsOnly` means the current step owns missing or invalid required output. Record or repair the current-run artifact instead of changing status text.
- `WaitForArtifactMaterialization` means an upstream step must produce required input. Do not force-complete the blocked step until the upstream artifact exists.
- `FreshAgentSession`, `RetryAgent`, or `ReworkContinuation` means retry is permitted only through the governed process rerun path and with the smallest corrective directive.
- `HumanEscalation` means the runtime cannot safely continue without an explicit operator decision.

Dead-lettered automation records are failure evidence. Resolve the outbox cause or create a governed rerun; do not delete the record to hide the failure.

## Escalations

Blocked, failed, refused, and waiting-approval transitions create durable escalation journal entries. Each escalation records kind, severity, status, owner, due date, source run/step, reason, resolution, and correlation id.

Use assignment when a person is actively triaging the escalation. Use resolution only after the process state is no longer blocked by that escalation. Reopen if the earlier resolution was premature.

## Rework

Manual rework creates a typed rework packet and queues a governed agent rerun. The rerun is allowed only for blocked or failed agent-owned steps. The directive should describe the smallest required correction and should not ask the agent to regenerate unrelated work.

## Approvals

Execution tool approvals continue the paused AgentFramework run. The operator note is recorded in the process journal and decision ledger. "Changes requested" is represented as an explicit rejection plus the operator note because the execution continuation API supports approve/reject decisions.

## Artifacts And Projection Lineage

When recording operator or agent evidence through `POST /api/processes/runs/{runId}/artifacts`, include the current run or step context and preserve external references. `ProcessArtifactRecordApiRequest` supports `externalReferenceKey` and `projectionLineage`; use those fields when a process artifact is mirrored into project structure or Cognitive Memory projection state.

Do not accept baseline scenarios or seeded artifacts as live-run evidence. For live UI-driven work, read `/api/processes/templates/live-run-profiles` and enforce the returned `freshRunPolicy` before dispatching agents or recording closure evidence.

## Direct Agent Tools Versus HTTP

Internal process agents have a smaller direct tool surface than the HTTP API. Direct tools cover definition authoring, run read/detail/start, step transition, assignment resolution, artifact record, option catalogs, and template pack reads/imports. Launch plans, manager directives, direct messages, escalations, operator approvals, and several template/detail routes are HTTP-only until typed tools are added. See [Agent runtime tool surface](agent-runtime-tool-surface.md).

## Secrets

Do not paste provider keys into appsettings, reports, screenshots, logs, or operator notes. Configure provider credentials through environment variables or the runtime secret mechanism documented in `docs/secure-configuration.md`. If a provider key pattern is found in tracked files, stop work, remove the value, rotate or revoke the credential outside the repository, and run the secret-scanning gate.

## Validation Gates

Run the focused process gates after changing operator behavior:

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release --filter "FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration Release --filter "FullyQualifiedName~ProcessWorkspaceTests"
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration Release --filter "FullyQualifiedName~SecretScanningTests|FullyQualifiedName~SnapshotIntegrityTests"
```

For release-candidate validation, also run:

```powershell
dotnet build CanDoItAll.slnx --configuration Debug
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessOutboxIntegrationTests|FullyQualifiedName~ProcessWorkflowExecutorIntegrationTests|FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests|FullyQualifiedName~SchedulerPlannerIntegrationTests|FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~Process_manager_diagnostics_operator_smoke_SB055_INV_001|FullyQualifiedName~Process_run_detail_verification_audit_readback_SB056_INV_001"
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter "FullyQualifiedName~Process_start_SB015_INV_001_large_screen_imports_template_and_executes_ready_launch_from_ui|FullyQualifiedName~Process_run_detail_recovery_SB030_large_screen_displays_blocked_recovery_and_artifact_readback|FullyQualifiedName~Project_structure_process_run_output_SB012_INV_002_opens_project_processes_from_output_folder_node"
```
