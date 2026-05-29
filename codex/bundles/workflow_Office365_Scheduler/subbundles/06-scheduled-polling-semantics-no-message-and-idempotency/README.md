# 06-scheduled-polling-semantics-no-message-and-idempotency

## Objective

Make recurring email polling safe and idempotent.

## No-message Semantics

No matching Office365 message is not an error.

Add explicit result semantics:

- Workflow result route: `no_messages`.
- Scheduler run status remains `Dispatched`.
- Scheduler summary: `No matching unprocessed email for person@example.com.`
- Optional separate display badge/tone: `No action`.

Do not retry no-message runs.

## Idempotency

Add stable keys:

```text
office365:<message-id>:summary
office365:<message-id>:tasks
```

Recommended project-structure behavior:

- Summary asset: create if missing, otherwise update/skip based on policy.
- Task nodes: detect existing source email id/idempotency key in metadata; skip duplicates by default.
- End result should say whether it created, updated, or skipped project outputs.

## Failure Ordering

Required order:

1. Download message.
2. Produce summary/tasks.
3. Persist project output idempotently.
4. Mark message processed.
5. Complete.

If step 4 fails, retry should not duplicate step 3.

## Tests

- First run creates output and marks processed.
- Retry after mark failure does not duplicate output.
- No-message run is success/no-action.
- Matching already processed message is ignored.
- Two concurrent scheduler dispatches for the same message do not create duplicates.
