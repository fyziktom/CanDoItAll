# Candidate Hydration Staging

## Candidate Header Selector

Purpose: isolate run/step dispatchability read and header ordering.

Allowed:

- read `ProcessRun` status,
- read eligible `ProcessStepRun` rows,
- apply lease expiry and eligibility filters,
- return typed headers.

Not allowed:

- durable claim writes,
- candidate hydration,
- technical-agent binding,
- route execution.

## Candidate Hydration Loader

Purpose: gather all read-only data needed to assemble one candidate.

Allowed:

- EF read models and dictionaries,
- artifact record readback,
- work brief readback,
- role requirement / assignment / branch outcome / artifact input readback.

Not allowed:

- `SaveAgentAsync`,
- transition writes,
- artifact record writes,
- workflow/subprocess/execution calls,
- finalizer invocation.

## Candidate Hydration Assembler

Purpose: convert loaded data into candidate construction inputs.

Allowed:

- branch outcome shaping,
- role assignment facts,
- expected artifact ids,
- external reference key sets,
- subprocess/workflow/direct-agent candidate branch shaping.

Not allowed:

- EF reads or writes,
- technical-agent access mutation,
- execution-client calls.

## Technical Agent Binding Coordinator

Purpose: isolate the side-effectful technical-agent binding and project-structure read-access preparation.

Allowed:

- call `IAiTechnicalAgentBridge.GetDirectorySummariesAsync`,
- call `executionClient.GetAgentEditorAsync`,
- apply project-structure read-access mutation,
- call `executionClient.SaveAgentAsync` when access changes,
- return explicit binding outcome.

Not allowed:

- hide mutation as a pure planner,
- decide route order,
- create process driver APIs.
