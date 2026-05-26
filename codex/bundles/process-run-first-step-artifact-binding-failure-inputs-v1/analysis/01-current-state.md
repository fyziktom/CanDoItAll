# Current State

This is an input-only current-state note built from API evidence.

## Runtime State

- The local app at `http://localhost:5032` is running and exposes OpenAPI.
- `/api/access/status` reports `authorizationEnabled: false`.
- The failed run is `Main app / Blazor app delivery`.
- The run has `0/8` completed steps and status `Failed`.
- Health reports one active execution, one pending approval, one failed step, and one invariant diagnostic.

## First Step State

- Step `Resolve Blazor delivery contract` failed.
- The agent execution for that step completed successfully and produced a structured `process_step_outcome_result`.
- The process runtime failed the step after artifact contract validation.
- The failure reason is `StaleOrWrongRun`.

## Artifact State

- A `Blazor delivery contract` process artifact record exists.
- The artifact record is linked to the step run and artifact expectation id.
- The artifact record points to an organization-scoped managed path.
- Its projection lineage references execution run `91e6a078-ac63-43e6-9901-6f8364539c42`.
- Its projection lineage has an empty `contentHash`.

## Surrounding State

- A failed-step escalation is open.
- A later manager-chat agent execution is waiting on approval for `processes_artifact_record`.
- The project structure has a current process-run output node for the delivery contract.
- One project-structure QA evidence node still mentions old run id `0cca729a-e9bc-47e7-89aa-bef9b88dbf1c`.
