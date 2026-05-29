# Current State Review

## What Codex Solved Well

- MAF packages are upgraded to the current 1.8 package line.
- `WorkflowDefinitionValidator` is now meant to be catalog-aware and the final report claims product save paths reject unknown/planned/schema-invalid executors.
- Artifact content storage was added after the previous metadata-only concern.
- `storage.file` now includes practical folder/file operations such as tree, create directory, delete, copy, move, hash, zip, unzip, include/exclude globs, and dry-run delete.
- Built-in workflow templates now demonstrate local-folder summaries, file diffs, HTTP download + ingestion, JSON transform task creation, and approval-gated HTTP.
- `Office365DownloadByCategoryWorkflowExecutor` and `Office365MarkProcessedWorkflowExecutor` already exist.
- Scheduler Planner can list workflow targets and dispatch a selected workflow with `SchedulerPlan.InputJson`.

## Current Office365 Gap

The Office365 plugin currently supports:

- Download by category.
- Mark processed by removing a source category and adding a processed category.

The requested polling scenario needs a different read executor:

- Download newest matching message by email address.
- Exclude messages already marked with a processed category.
- Return zero messages as a normal no-op result by default.
- Preserve enough input context (`projectId`, `nodeId`, `runContext`) for downstream project-structure writes.
- Carry a processing context that the later mark-processed step can use.

The current mark-processed executor also assumes a source category is present. For "not already processed" polling, there might be no source category to remove. It must support add-only processed category behavior or a new add-category executor.

## Current Scheduler Gap

Scheduler Planner can technically schedule a workflow and pass `InputJson`, but the UX is still too raw for the requested use case:

- Target selection is workflow/process only.
- Input configuration is a raw `InputTextArea` for JSON.
- There is no structured schema/parameter form for a workflow template.
- There is no CRM contact/email picker.
- There is no project picker or project-structure node picker in the scheduler.
- There is no scheduler-level validation that the configured input JSON satisfies the chosen workflow's required parameters.
- There is no polling-specific "no new email is success" visualization.
