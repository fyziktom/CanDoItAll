# Critical Findings

## F1 — Current Office365 executor is category-centric, not person/email-centric

`Office365GraphClient.DownloadMessagesByCategoryAsync` filters `categories/any(c:c eq '{category}')`. This cannot implement "watch whether this person sent me an unprocessed task email" without manual category pre-tagging.

## F2 — No-message polling must be success, not failure

Current category download executor throws when the batch is empty. That is correct for manual "download this category" tests, but wrong for a recurring watcher. A two-hour poll where no new matching message exists should be recorded as `Completed / NoMessages`, not `Failed`.

## F3 — Marking processed must be add-only capable

The requested filter is "not already marked as processed", not "has source category X". Therefore the mark step must be able to add a processed category without requiring a source category to remove.

## F4 — Scheduler has raw JSON input only

The Scheduler UI can schedule workflows, but does not provide a typed workflow-parameter form. A business user cannot reasonably configure:
- watched email address,
- CRM contact,
- Office365 connection,
- target project,
- target parent node,
- processed category,
- lookback window.

## F5 — Idempotency is necessary before recurring schedule use

If the workflow writes summary/tasks and then fails before marking the message processed, the next run may process the same message again. The project write step must use a stable idempotency key, such as `office365:{messageId}:summary` or `office365:{messageId}:tasks`, and either update/skip duplicates.

## F6 — Approval policy and unattended schedules conflict

Office365 category mutation is an external write. The existing approval gate can pause runs, but a recurring unattended scheduler needs an explicit policy:
- either scheduled workflow runs requiring approval stay WaitingForInput,
- or the user can explicitly preapprove a specific workflow+executor+connection+category scope.
No silent bypass is acceptable.
