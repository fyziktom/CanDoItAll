# Target Solution

## Desired end state after this bundle
- `CanDoItAll.Processes.Core` remains stable and deterministic.
- Driver packages remain verification-only alpha packages.
- Gateway exposes explicit methods for approved read-only lanes:
  - transcript verification,
  - runtime evidence consistency,
  - artifact evidence,
  - Office evidence,
  - business-analysis evidence,
  - observation aggregation.
- Gateway still has no generic `Verify(lane, object)` entrypoint and no runtime host behavior.
- Process module has explicit read-only adapters for approved supplied-evidence lanes, with allow-listed files only.
- All drivers/adapters use a shared evidence policy and shared no-mutation/audit/redaction expectations.
- Full unit debt is either removed or explicitly narrowed to current-owned skips with current source-backed replacement tests.

## Non-goals
- No runtime host implementation.
- No process runtime integration.
- No manager or scheduler command.
- No execution-capable driver.
- No external system calls.
