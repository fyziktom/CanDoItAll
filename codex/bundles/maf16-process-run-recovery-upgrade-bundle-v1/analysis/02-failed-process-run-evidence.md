# Failed Process Run Evidence Summary

## Run identity

- Run id: `9bbc0667-9d12-4506-ba81-654ef924cad6`
- Run name: `Main app / Blazor app delivery`
- Definition id: `9dd2f94e-b607-4f47-afb6-c51765db55bb`
- Project id: `7330105d-8450-4c80-923b-5c27d8e63d6c`
- Status: `Failed`
- Progress: `0/8`

## Failed step

- Step id: `0610f6d6-5d37-4313-b560-09cc9484f5b8`
- Title: `Resolve Blazor delivery contract`
- Status: `Failed`
- Block reason code: `ArtifactContractUnsatisfied`
- Next recovery action: `RecoverArtifactsOnly`

Failure text:

```text
AgentFramework execution failed: Step 'Resolve Blazor delivery contract' cannot be completed because required artifact contract validation failed: Blazor delivery contract: StaleOrWrongRun (The candidate artifact is not bound to the current process run, step, execution run, or workflow run.).
```

## Contradictory artifact state

The step view reports the required artifact as satisfied by process artifact record `aa9a3e75-8d3e-4757-bafa-be00e8678b8d`.

The same step later fails final validation as `StaleOrWrongRun`.

This proves at least one of these paths is wrong or inconsistent:

- artifact expectation satisfaction projection
- finalizer-grade artifact validation
- current-run lineage interpretation
- current-run managed path normalization
- content hash / content availability check
- execution-run binding extraction

## Artifact facts

Artifact record:

- `ProcessRunId`: current run
- `StepRunId`: failed step
- `ArtifactExpectationId`: expected delivery contract
- `ManagedStoragePath`: `artifacts/scopes/organization/.../process-runs/{runId}/01-blazor-delivery-contract.md`
- `ExternalReferenceKey`: `workspace-written-artifact|{executionRunId}|{expectationId}|artifacts/process-runs/{runId}/01-blazor-delivery-contract.md`
- `ProjectionLineageJson.sourceKind`: `WorkspaceWrite`
- `ProjectionLineageJson.sourceExecutionRunId`: current step execution run
- `ProjectionLineageJson.projectedExecutionRunId`: current step execution run
- `ProjectionLineageJson.contentHash`: empty

## Likely root cause candidates

1. The validator accepts only `artifacts/process-runs/{runId}/...` as current-run managed path and rejects organization-scoped paths.
2. The validator treats empty `contentHash` as stale/wrong-run instead of `MissingContentHash` or `ContentUnavailable`.
3. The validator compares `ExternalReferenceKey` path with `ManagedStoragePath` too literally.
4. The artifact satisfaction read-model is weaker than finalizer validation.
5. Source execution run binding is not recognized because the finalizer context reload path lacks the current execution run id or accepted recovery execution run id.
6. The content reader cannot resolve organization-scoped managed storage paths and silently converts a content problem into `StaleOrWrongRun`.

Codex must test these hypotheses instead of guessing.
