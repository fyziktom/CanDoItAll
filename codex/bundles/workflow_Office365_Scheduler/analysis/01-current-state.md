# Current State

## Baseline

- The previous workflow executor catalog work added MAF 1.8 usage, catalog-backed validation, artifact content storage, and bundled executors for storage, JSON transforms, Markdown rendering, delay, approval, HTTP download, and source ingestion.
- `command.process` is intentionally planned/unavailable and must remain unavailable in this bundle.
- Workflow templates are file-backed under `repo://Templates/Workflows`.
- Scheduler Planner can currently dispatch workflow targets with raw `InputJson`.

## Office365 Gap

- The Office365 plugin currently supports category-oriented download and mark-processed behavior.
- The requested polling scenario requires address-oriented matching, processed-category exclusion, no-message success semantics, and add-only processed-category mutation.
- The current category workflow path is not enough because it assumes a source category already exists.

## Scheduler Gap

- Scheduler Planner has no workflow input schema contract that can drive a typed form.
- The current UI forces users to hand-write JSON for the Office365 email-watch scenario.
- There are no narrow Scheduler option providers for CRM contact email, Office365 connection/category, projects, or project nodes.

## Runtime Gap

- Recurring polling needs explicit `no_messages` success semantics so empty polls do not look like failures.
- Project writes need idempotency by Office365 message id before the message is marked processed, otherwise retry can duplicate summaries or tasks.
- Scheduler dispatch and history need auditable route, workflow run id, retry, approval, and no-action status details.

