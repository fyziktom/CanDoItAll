# Structured Input

## Required Functional Outcome

Create a scheduled-friendly Office365 email-intake workflow path:

1. User configures:
   - Office365 connection (optional explicit connection id; otherwise latest/default connection).
   - Watched email address, either typed or selected from CRM.
   - Matching mode, default `FromOrSenderEquals`.
   - Processed category, e.g. `CanDoItAllProcessed`.
   - Optional folder id/name, lookback window, and max body characters.
   - Target project id.
   - Optional parent project-structure node id.
   - Workflow mode: `summary` or `tasks`.

2. Scheduler runs every chosen interval (for example every 2 hours).
3. Workflow asks Office365 for one newest unprocessed matching message.
4. If none exists, workflow returns a no-op success, not a failed schedule.
5. If a message exists:
   - summary workflow stores a Markdown summary asset under the chosen project/node;
   - task workflow creates task WorkItem nodes under the chosen project/node.
6. Workflow marks the message with the processed category only after project write succeeds.
7. Retrying the same scheduled fire must not create duplicate project assets/tasks for the same message.

## Important Safety / UX Expectations

- The polling workflow must be previewable without Graph calls.
- The category mutation requires approval policy only if the global workflow policy requires external write approval. For unattended scheduler use, the product must provide an explicit "allow this scheduled workflow to mark processed emails" setting or an automation-safe preapproval grant; do not silently bypass approval.
- Scheduler UI must not force users to hand-write JSON for the common Office365 email-watch scenario.
