---
name: candoitall-api-processes
description: Use when managing CanDoItAll process definitions, templates, launch plans, runs, steps, assignments, artifacts, direct messages, and analytics through the HTTP API instead of the removed Processes MCP server.
---

# CanDoItAll Processes API

Use this skill when a task needs process authoring or runtime control through the CanDoItAll web API. Processes are the durable orchestration layer: definitions, templates, runs, steps, assignments, required artifacts, decisions, health, recovery, and audit evidence belong here. Workflows can be assigned as role executors inside a process step, but they are not a replacement for process governance.

## Access

- Start the CanDoItAll web app and inspect Swagger/OpenAPI at `/swagger` or `/swagger/v1/swagger.json`.
- Check `/api/access/status` before assuming bearer tokens are required.
- If JWT is active, send `Authorization: Bearer <token>`.
- Do not reinstall or use `candoitall_processes`; that MCP server has been removed.
- The current HTTP API serializes enums as numbers unless a caller explicitly uses a compatible converter. Prefer source constants or OpenAPI over hardcoded numbers when writing code.

## Governance Fields You Must Preserve

- Definition `contractMode`: `0` Compatibility, `1` Strict. Strict definitions reject text-inferred or missing risky operation contracts.
- Step `allowedOperations`: include typed operations such as `ReadProcessContext`, `ReadProjectStructure`, `ReadUpstreamArtifacts`, `WriteManagedProcessArtifacts`, `WriteExternalArtifactDestination`, `MutateProductTarget`, `RunValidation`, `LaunchRuntime`, `CaptureRuntimeProof`, `ExecuteExternalAction`, `RecoverArtifactsOnly`, and `EscalateOrDecide`.
- Step `operationTargetScope`: use the narrowest accurate `ProcessStepTargetScope` value: `ManagedProcessArtifactsOnly`, `ManagedOutputProduct`, `ExternalArtifactDestination`, `ExternalProductTargetReadOnly`, `ExternalProductTargetMutable`, or `ExternalActionControlled`.
- Artifact expectation workflow mapping: preserve `workflowOutputId`, `workflowOutputName`, and `workflowOutputKind` when a required process artifact is produced by a workflow executor.
- Artifact expectation subprocess mapping: preserve `subprocessChildArtifactExpectationId` when a parent process required artifact maps to a child process artifact.
- Runtime block/recovery state: preserve `blockCause` on transitions. Use `OwnOutput` for the blocked step's missing/invalid required output, `UpstreamInput` for missing upstream materialization, `RuntimeEvidence` for validation/runtime failures, and `PolicyDenied` for governed policy blocks.
- Runtime readbacks expose `blockReasonCode`, `recoveryOptions`, `nextRecoveryAction`, `health.recommendedAction`, `health.missingArtifactCount`, and invariant diagnostics. Recovery options are source-aligned with `ProcessStepRecoveryOption`: `None`, `WaitForArtifactMaterialization`, `RecoverArtifactsOnly`, `RetryAgent`, `FreshAgentSession`, `ReworkContinuation`, `HumanEscalation`, `RepairImplementation`, and `RerunValidation`.
- Artifact expectation satisfaction statuses are source-aligned with `ProcessArtifactExpectationSatisfactionStatus`: `Expected`, `Satisfied`, `AutoProjected`, `Missing`, `ProjectionFailed`, `ContentUnavailable`, `NotApplicable`, `InvalidFormat`, `InsufficientEvidence`, `StaleOrWrongRun`, `WrongProducerMode`, `PlaceholderOnly`, and `ContentHashMismatch`.
- Required artifact expectations remain unsatisfied when status is `Missing`, `ProjectionFailed`, `ContentUnavailable`, `InvalidFormat`, `InsufficientEvidence`, `StaleOrWrongRun`, `WrongProducerMode`, `PlaceholderOnly`, or `ContentHashMismatch`.
- Artifact records that come from automation, workflow, subprocess, or manual recovery must keep projection lineage fields such as `projectionLineage`, `projectionLineageJson`, `projectionIdentityHash`, `externalReferenceKey`, and managed storage path. Do not replace lineage with prose.
- Live-run profile summaries expose typed `freshRunPolicy`. Preserve `requiresFreshRun`, seeded transition/artifact rejection, pre-dispatch checks, evidence checks, and project-structure writeback guidance when selecting or documenting a live run.

## Definition And Template Work

- Definitions: `GET /api/processes/definitions`, `GET /api/processes/definitions/{definitionId}`, `POST /api/processes/definitions`, `POST /api/processes/definitions/{definitionId}/publish`, `DELETE /api/processes/definitions/{definitionId}`.
- Import/export: `GET /api/processes/definitions/{definitionId}/export`, `POST /api/processes/definitions/import`.
- Templates: `GET /api/processes/templates`, `GET /api/processes/templates/{processKey}`, `GET /api/processes/templates/{processKey}/detail`, `GET /api/processes/templates/{processKey}/envelope`, `GET /api/processes/templates/{processKey}/mermaid`, `POST /api/processes/templates/{processKey}/import`.
- Baseline scenarios: `GET /api/processes/templates/baseline-scenarios`.
- Live-run profiles: `GET /api/processes/templates/live-run-profiles`.
- Use template `/detail` and `/envelope` routes when verifying typed operation contracts, shared sidecars, workflow mappings, subprocess mappings, and baseline scenarios. Use live-run profiles when preparing a fresh UI-driven run without seeded transitions or artifacts.
- Do not move product mutation into validation, revalidation, writeback, or escalation steps. Blazor implementation or repair steps may mutate the product target; validation and screenshot/review steps are read-only unless their contract explicitly allows a governed external action.

## Runtime Work

- Runs: `GET /api/processes/runs`, `GET /api/processes/runs/{runId}`, `POST /api/processes/runs/start`, `POST /api/processes/runs/stop`.
- Steps: `GET /api/processes/runs/{runId}/steps`, `GET /api/processes/runs/{runId}/steps/{stepRunId}`, `POST /api/processes/runs/{runId}/steps/{stepRunId}/transition`, `POST /api/processes/runs/{runId}/steps/{stepRunId}/rerun-agent`.
- Artifacts and assignments: use run-scoped and step-scoped artifact/assignment routes so context stays small. Prefer step-scoped artifact recording when the artifact satisfies a step expectation.
- Manager control: `POST /api/processes/runs/{runId}/manager-directives` and `POST /api/processes/runs/{runId}/direct-messages`.
- Launch and HR matching: `/api/processes/launch-plans`, `/hr-match`, `/submit-approval`, `/approval-decisions`, `/provision`, `/execute`, and `/candidate-selections`.
- Governed multi-agent process runs are expected to use the active PostgreSQL AppDbContext profile when `Processes:Runtime:RequirePostgreSqlForAgentAutomation` is enabled.
- After transitions, fetch either `/runs/{runId}/steps/{stepRunId}` for focused state or `/runs/{runId}` for health, invariant diagnostics, artifacts, workflow runs, assignments, and timeline.

## Current-Run Troubleshooting Workflow

Use this order before mutating a run:

1. Read `/api/processes/runs/{runId}` with only the include flags needed for the question.
2. Check run `health`, `invariantDiagnostics`, step `blockReasonCode`, `recoveryOptions`, `nextRecoveryAction`, and attempt timeline.
3. For evidence issues, query the step-scoped artifact route and inspect artifact status, `projectionLineage`, `projectionIdentityHash`, `externalReferenceKey`, and managed storage path.
4. For final delivery issues, verify the output is grounded by current-run project-structure context or managed output root. Do not accept stale external-target aliases or dated tool receipts as final delivery proof.
5. For manager questions, inspect selected-run assignment and configured manager before using fallback manager chat or direct messages.
6. For live UI-driven runs, read the live-run profile `freshRunPolicy` and reject seeded baseline transitions or artifacts as current-run evidence.
7. Record the repair as a transition, assignment, artifact, manager directive, direct message, approval decision, or rerun request through the API. Do not patch database rows or JSON by hand.

## Agent Skill And Tool Matrix

Before dispatching or staffing an agent, evaluate required role capabilities with `AgentCapabilityRequirementEvaluator`. Treat `AgentCapabilityDiagnostic` values as blocking diagnostics; do not ask an agent to improvise process operations with missing tools, stale catalog assignments, or retired skills.

| Role | Required skill/tool capabilities | Process access | Required behavior |
| --- | --- | --- | --- |
| Process author | `candoitall-api-processes`, `processes_definition_editor_get`, `processes_definition_save`, `processes_definition_publish`, template read tools, and `processes_template_import` only when importing | Read/write for allowed definitions | Use typed definition and template routes; do not mutate process JSON or database rows directly. |
| Process manager | `candoitall-api-processes`, `processes_runs_list`, `processes_run_detail_get`, `processes_analytics_get`, `processes_step_transition`, `processes_assignment_resolve`, `processes_artifact_record` | Read/write for managed definitions | Inspect current run state before transitions and record assignment or artifact evidence through governed tools. |
| Step executor | Workspace tools named by the work brief and `processes_artifact_record` only when the step records its own process artifact | Usually read-only process access | Complete the step with current-run evidence; do not synthesize process transitions when transition tools are absent. |
| Reviewer or QA | Workspace read/validation/browser tools named by the step, `processes_run_detail_get`, `processes_artifact_record` when review evidence is required | Read access; write access only for assigned review artifacts or transitions | Review against current-run evidence and required receipts. |
| Template curator | `processes_templates_list`, `processes_template_get`, `processes_template_mermaid_get`, `processes_template_baseline_scenarios_list`, `processes_template_live_run_profiles_list`, `processes_template_import` | Write access only for import/publish work | Inspect templates read-only unless import or publish is explicitly requested. |

Runtime policy backs this matrix. `DefaultAgentToolInvocationPolicy` denies unknown tools and known tools without a registered policy classification, while process tools enforce `AgentProcessAccessMetadata` read/write scope.

## Filtering Rules

- Use `definitionId`, `projectId`, `status`, `operatingMode`, `search`, and `take` on run lists.
- Use `stepRunId`, `stepDefinitionId`, `artifactId`, `artifactExpectationId`, `artifactKind`, `roleRequirementId`, `partyId`, `agentId`, `executionState`, `search`, `take`, and include flags on run detail routes.
- For artifact review, prefer `/runs/{runId}/steps/{stepRunId}/artifacts` over full run detail.

## API Examples

Raw JSON examples below use the current numeric enum shape. Check OpenAPI in the running host before generating clients.

Save a strict definition with a typed implementation contract:

```http
POST /api/processes/definitions
Content-Type: application/json
```

```json
{
  "name": "Blazor WASM PWA delivery",
  "summary": "Build and validate a Blazor WASM PWA from a run-request topic.",
  "valueStatement": "Deliver an offline-capable app with proof.",
  "customerName": "Engineering",
  "ownerName": "Process owner",
  "governancePolicySummary": "Product mutation is limited to implementation and repair steps.",
  "changeSummary": "Initial definition.",
  "constitutionRuleSummary": "Do not complete required steps without concrete managed evidence.",
  "operatingModeSummary": "Assisted execution.",
  "simulationReadinessSummary": "Safe for local validation.",
  "contractMode": 1,
  "roles": [
    {
      "key": "software-engineer",
      "displayName": "Software engineer",
      "purpose": "Implement the Blazor WASM PWA.",
      "staffingIntent": "AI-capable implementation role.",
      "preferredExecutorKind": "AI agent"
    }
  ],
  "steps": [
    {
      "key": "implement-blazor-pwa",
      "title": "Implement Blazor WASM PWA",
      "stepKind": 1,
      "outputContractSummary": "Requested Blazor WASM PWA exists.",
      "evidenceContractSummary": "Implementation summary and browser proof are recorded.",
      "allowedOperations": [0, 3, 5, 6, 7, 8],
      "operationTargetScope": 4,
      "artifactExpectations": [
        {
          "artifactKind": 3,
          "title": "Implementation change set",
          "isRequired": true,
          "validationRequirementSummary": "Must list changed files, build/test result, and browser proof."
        }
      ]
    }
  ]
}
```

Export and import a definition:

```http
GET /api/processes/definitions/{definitionId}/export
POST /api/processes/definitions/import
```

Start a run:

```http
POST /api/processes/runs/start
Content-Type: application/json
```

```json
{
  "processDefinitionId": "00000000-0000-0000-0000-000000000000",
  "projectId": "00000000-0000-0000-0000-000000000000",
  "runName": "Blazor PWA run",
  "operatingMode": 2,
  "triggerReason": "Create and validate the requested Blazor WASM PWA."
}
```

List fresh live-run profiles before starting a UI-driven run:

```http
GET /api/processes/templates/live-run-profiles
```

```json
[
  {
    "key": "generic-blazor-wasm-pwa-app",
    "processTemplateKey": "blazor-app-delivery",
    "runNameTemplate": "Blazor WASM PWA delivery / {AppTopic}",
    "operatingMode": "GovernedLive",
    "freshRunPolicy": {
      "requiresFreshRun": true,
      "allowsSeededTransitions": false,
      "allowsSeededArtifacts": false,
      "requiredPreDispatchChecks": [
        "Confirm the run request supplies the concrete app topic and acceptance criteria."
      ],
      "requiredEvidenceChecks": [
        "Before validation, read run detail and confirm required artifact expectations are satisfied by current-run evidence."
      ],
      "projectStructureWritebackGuidance": "Write back only current-run managed output and evidence roots with project-structure lineage."
    }
  }
]
```

Block a step with typed recovery ownership:

```http
POST /api/processes/runs/{runId}/steps/{stepRunId}/transition
Content-Type: application/json
```

```json
{
  "targetStatus": 4,
  "reason": "Required upstream artifacts are missing and the source step must materialize them.",
  "blockCause": 1,
  "decidedBy": "api-client",
  "suppressAutomationDispatch": true
}
```

Record a required artifact with lineage:

```http
POST /api/processes/runs/{runId}/steps/{stepRunId}/artifacts
Content-Type: application/json
```

```json
{
  "artifactKind": 3,
  "title": "Implementation change set",
  "artifactExpectationId": "00000000-0000-0000-0000-000000000000",
  "trustStatus": 1,
  "sensitivityLevel": 1,
  "provenanceSummary": "Projected from the current process execution.",
  "allowedFutureUsageSummary": "May be used as validation evidence for this run.",
  "reviewSummary": "Contains concrete changed files and browser proof.",
  "managedStoragePath": "artifacts/process-runs/{runId}/implementation-change-set.md",
  "externalReferenceKey": "workspace-written-artifact|{executionRunId}|{artifactExpectationId}|implementation-change-set.md",
  "projectionLineage": {
    "sourceKind": 2,
    "sourceExecutionRunId": "00000000-0000-0000-0000-000000000000",
    "contentHash": "sha256:..."
  }
}
```

Read back health:

```http
GET /api/processes/runs/{runId}?includeWorkBriefs=false&includeExecutionRuns=false&includeDirectMessages=false
```

Check `stepRuns[].blockReasonCode`, `stepRuns[].recoveryOptions`, `stepRuns[].nextRecoveryAction`, `stepRuns[].health.nextRecoveryAction`, `health.recommendedAction`, `health.missingArtifactCount`, and `invariantDiagnostics`.

## Blazor WASM PWA Live-Run Checklist

- Start from the `generic-blazor-wasm-pwa-app` live-run profile or the `blazor-app-delivery` template detail route. Put the concrete app topic and acceptance criteria in the run request.
- Do not use seeded baseline transitions or artifacts as proof for a live UI run.
- Confirm the first planning/contract step is read-only and that implementation or repair steps are the only steps allowed to mutate the product target.
- Require managed artifacts for implementation summary, test/build output, browser screenshot or equivalent Playwright proof, and PWA/offline validation notes.
- After the run starts, resolve assignments before expecting automation dispatch.
- For blocked missing evidence, use `blockCause` `0` for the current step's missing output and `1` for missing upstream materialization.
- After completion, read run detail and verify no required artifact expectation remains missing, no stale lineage satisfies a required artifact, and invariant diagnostics do not contain manual transition validation failures.

## Validation

- After starting or transitioning a run, read back the run and specific step.
- After recording artifacts or assignments, query the step-scoped route.
- For templates, use `/detail` when compatibility notes or sidecar files matter.
- For docs-only updates, run `git diff --check` and source assertions for the named fields. For runtime behavior changes, add focused integration tests around `ProcessesService` and `ProcessesApi`.
