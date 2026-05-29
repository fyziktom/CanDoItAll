# 03-office365-email-summary-and-task-template-workflows

## Objective

Add managed workflow templates for the recurring Office365 email-watch scenario.

## Template File

Add:

```text
Templates/Workflows/workflows/office365-email-watch-workflows.yaml
```

Add the file to `Templates/Workflows/manifest.yaml` and bump seed version.

## Template 1: Summary

Key suggestion:

```text
office365-email-address-summary-to-project
```

Flow:

1. Start.
2. `office365.message-by-address-unprocessed`.
3. Switch:
   - `$.count == 0` -> no-op End.
   - `$.count > 0` -> LLM summary.
4. LLM returns strict JSON:
   - `route`
   - `summary`
   - `markdown`
   - `evidence`
   - `projectId`
   - `nodeId`
   - `sourceEmailId`
   - `office365Processing`
5. `project-structure` / `CreateAsset`:
   - project id from `$.projectId`;
   - parent node from `$.nodeId`;
   - title: deterministic title including message subject/date;
   - content from `$.markdown`;
   - metadata/idempotency key containing Office365 message id.
6. `office365.mark-message-processed` add processed category.
7. End.

## Template 2: Tasks

Key suggestion:

```text
office365-email-address-tasks-to-project
```

Flow:

1. Start.
2. `office365.message-by-address-unprocessed`.
3. Switch no-message -> no-op End.
4. LLM task extraction.
5. Optional `json.transform` validate/normalize tasks.
6. `project-structure` / `CreateTaskNodes`:
   - project id from `$.projectId`;
   - parent node from `$.nodeId`;
   - task array from `$.tasks`.
7. Mark message processed.
8. End.

## Important Template Semantics

- Mark processed only after the project write succeeds.
- No-message path must not call LLM or mark processed.
- Templates must work with Scheduler input JSON:
  - `emailAddress`
  - `processedCategory`
  - `projectId`
  - `nodeId`
  - optional `connectionId`
  - optional `lookbackHours`
- Templates must include preview simulation steps for Office365 executor nodes.

## Tests

- Template pack loader test.
- Seed refresh test.
- Scenario harness test for no-message.
- Scenario harness test for one message -> summary asset -> mark processed.
- Scenario harness test for one message -> tasks -> mark processed.
