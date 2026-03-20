# Waits, logs, and health prompt

Implement deterministic waiting and diagnostics support for app runtime and operations.

## Implement now
- `HttpHealthProbe`
- `WaitEngine`
- support for app wait conditions:
  - `Running`
  - `Healthy`
  - `Stopped`
  - `QuietSinceCursor`
  - `LogMatch`
- `OperationWait`
- health snapshots in app status
- timeout outcomes with last-known state
- evidence-friendly log retrieval

## Behavioral rules
- No client-side sleeps should be required.
- `QuietSinceCursor` must be based on log activity after a cursor.
- `Healthy` should prefer health probe success when configured.
- On timeout, return enough diagnostic context to act.
- Preserve correlation IDs across logs, waits, and statuses.

## Deliver
- tests for healthy wait
- tests for quiet wait
- tests for timeout behavior
