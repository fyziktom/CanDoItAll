# Corrective playbook — side-effects reset

Invoke this when activity/search side effects remain best-effort post-commit calls.

## Trigger examples

- a command can report failure after the DB commit solely because projection/activity dispatch threw;
- outbox design was deferred without durable replacement.

## Mandatory repair moves

- introduce or adapt a durable outbox boundary;
- prove retry/idempotency behavior;
- rerun side-effect failure proof before reopening Gate C.
