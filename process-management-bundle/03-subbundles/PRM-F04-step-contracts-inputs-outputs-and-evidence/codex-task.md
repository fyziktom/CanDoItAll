# Codex task — PRM-F04

Implement **Step contracts, inputs, outputs, and evidence** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not create a new durable agent registry; use CRM-HR bindings when actors are involved.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo in the first process-management implementation.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- Each step can declare entry criteria, exit criteria, expected artifacts, and evidence requirements.
- Steps can declare reusable input and output contracts with type, cardinality, and notes.
- Reviewers can see required evidence before completion is allowed.
- Contract data is queryable separately from the diagram layout.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessContractModels.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessContractServices.cs (new)`
- `src/CanDoItAll.Modules.Validation/* (integration hooks)`
- `src/CanDoItAll.Modules.TestLab/* (integration hooks)`
- `tests/CanDoItAll.Tests.Integration/ProcessContractIntegrationTests.cs (new)`
