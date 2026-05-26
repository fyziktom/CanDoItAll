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
- Step `operationTargetScope`: use the narrowest accurate target scope: managed artifacts, managed output product, external artifact destination, external product target read-only, external product target mutable, or external action controlled.
- Artifact expectation workflow mapping: preserve `workflowOutputId`, `workflowOutputName`, and `workflowOutputKind` when a required process artifact is produced by a workflow executor.
- Artifact expectation subprocess mapping: preserve `subprocessChildArtifactExpectationId` when a parent process required artifact maps to a child process artifact.
- Runtime block/recovery state: preserve `blockCause` on transitions. Use `OwnOutput` for the blocked step's missing/invalid required output, `UpstreamInput` for missing upstream materialization, `RuntimeEvidence` for validation/runtime failures, and `PolicyDenied` for governed policy blocks.
- Runtime readbacks expose `blockReasonCode`, `recoveryOptions`, `nextRecoveryAction`, `health.recommendedAction`, `health.missingArtifactCount`, and invariant diagnostics. Read them after blocking or completing critical steps.
- Artifact records that come from automation, workflow, subprocess, or manual recovery must keep projection lineage fields such as `projectionLineage`, `projectionLineageJson`, `projectionIdentityHash`, `externalReferenceKey`, and managed storage path. Do not replace lineage with prose.

## Definition And Template Work

- Definitions: `GET /api/processes/definitions`, `GET /api/processes/definitions/{definitionId}`, `POST /api/processes/definitions`, `POST /api/processes/definitions/{definitionId}/publish`, `DELETE /api/processes/definitions/{definitionId}`.
- Import/export: `GET /api/processes/definitions/{definitionId}/export`, `POST /api/processes/definitions/import`.
- Templates: `GET /api/processes/templates`, `GET /api/processes/templates/{processKey}`, `GET /api/processes/templates/{processKey}/detail`, `GET /api/processes/templates/{processKey}/envelope`, `GET /api/processes/templates/{processKey}/mermaid`, `POST /api/processes/templates/{processKey}/import`.
- Baseline scenarios: `GET /api/processes/templates/baseline-scenarios`.
- Use template `/detail` and `/envelope` routes when verifying typed operation contracts, shared sidecars, workflow mappings, subprocess mappings, and baseline scenarios.
- Do not move product mutation into validation, revalidation, writeback, or escalation steps. Blazor implementation or repair steps may mutate the product target; validation and screenshot/review steps are read-only unless their contract explicitly allows a governed external action.

## Runtime Work

- Runs: `GET /api/processes/runs`, `GET /api/processes/runs/{runId}`, `POST /api/processes/runs/start`, `POST /api/processes/runs/stop`.
- Steps: `GET /api/processes/runs/{runId}/steps`, `GET /api/processes/runs/{runId}/steps/{stepRunId}`, `POST /api/processes/runs/{runId}/steps/{stepRunId}/transition`, `POST /api/processes/runs/{runId}/steps/{stepRunId}/rerun-agent`.
- Artifacts and assignments: use run-scoped and step-scoped artifact/assignment routes so context stays small. Prefer step-scoped artifact recording when the artifact satisfies a step expectation.
- Manager control: `POST /api/processes/runs/{runId}/manager-directives` and `POST /api/processes/runs/{runId}/direct-messages`.
- Launch and HR matching: `/api/processes/launch-plans`, `/hr-match`, `/submit-approval`, `/approval-decisions`, `/provision`, `/execute`, and `/candidate-selections`.
- Governed multi-agent process runs are expected to use the active PostgreSQL AppDbContext profile when `Processes:Runtime:RequirePostgreSqlForAgentAutomation` is enabled.
- After transitions, fetch either `/runs/{runId}/steps/{stepRunId}` for focused state or `/runs/{runId}` for health, invariant diagnostics, artifacts, workflow runs, assignments, and timeline.

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
  "name": "Tetris Blazor WASM PWA delivery",
  "summary": "Build and validate a Tetris Blazor WASM PWA.",
  "valueStatement": "Deliver a playable offline-capable sample with proof.",
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
      "key": "implement-tetris",
      "title": "Implement Tetris WASM PWA",
      "stepKind": 1,
      "outputContractSummary": "Playable Tetris Blazor WASM PWA exists.",
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
  "runName": "Tetris PWA run",
  "operatingMode": 2,
  "triggerReason": "Create and validate the Tetris Blazor WASM PWA."
}
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

## Tetris Blazor WASM PWA Checklist

- Start from the `baseline-blazor-wasm-pwa-tetris` baseline scenario or the `blazor-app-delivery` template detail route.
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
