# 07-scheduler-dispatch-observability-and-retry-policy

## Objective

Make Scheduler Planner observability and retry behavior fit recurring Office365 polling.

## Improvements

- Add target run summary normalization for common workflow routes:
  - `processed`
  - `no_messages`
  - `failed`
  - `waiting_for_approval`
- Store workflow run id and route in scheduler run details.
- Add filter for "No action" / "Processed" / "Waiting for approval" if the current model supports extending status; otherwise add a `SummaryKind` field.
- Show last successful processed email timestamp/message subject if available.
- Show last no-message timestamp separately from last error.
- Do not let no-message overwrite `LastError`.
- Add retry policy distinction:
  - Graph/network failure -> retry;
  - no-message -> complete;
  - approval waiting -> no retry;
  - project write failure -> retry with idempotency.

## Approval / Preapproval

Office365 mark processed is external write. For scheduler:

- If workflow policy requires approval, the run may pause WaitingForInput.
- Add product-level explicit preapproval concept only if it is strongly scoped:
  - workflow id/version;
  - executor id;
  - connection id;
  - processed category;
  - target project/node;
  - optional email address.
- Record audit event when preapproval is used.
- Do not bypass approval based only on "scheduler launched it".

## Tests

- Scheduler history shows no-message as non-failure.
- Waiting approval run is not retried as failure.
- Failed Graph/network dispatch gets retry-scheduled.
- Preapproval scope mismatch blocks mark-processed.
