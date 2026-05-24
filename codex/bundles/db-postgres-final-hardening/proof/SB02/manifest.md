# SB02 proof manifest

## Status

Completed.

## Changed files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | See `../SB08/transcripts/changed-file-hashes.txt` | See `../SB08/transcripts/changed-file-hashes.txt` | Guard process outbox attempt start/finalization by lease token and unexpired lease. |
| `src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | See transcript | See transcript | Guard connector outbox finalization and defer audit rows until canonical update wins. |
| `src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | See transcript | See transcript | Guard automation delivery finalization by lock token and running state. |
| Integration tests | See transcript | See transcript | Add stale-worker negative coverage for process, connector, and automation delivery paths. |

## Commands

| Command | Result | Transcript |
|---|---|---|
| Focused lease/options integration filter | Passed, 5 tests | `transcripts/focused-lease-and-options-tests.txt` |
| Source audit for conditional finalization | Passed | `transcripts/conditional-finalization-source-audit.txt` |

## Source assertions

| Assertion | Source | Proof |
|---|---|---|
| Process outbox finalization requires matching `Id`, `LeaseToken`, and unexpired lease. | `ProcessOutbox.cs` | Source audit transcript. |
| Connector command finalization requires matching `Id`, `LeaseToken`, and unexpired lease before audit rows are committed. | `ConnectorOutboxService.cs` | Source audit transcript. |
| Automation delivery finalization requires matching `Id`, `LockToken`, `Running` state, and unexpired lock. | `AutomationMessagingServices.cs` | Source audit transcript. |

## Negative tests

| Scenario | Expected | Result |
|---|---|---|
| Process dispatch worker loses lease before finalization. | Stale worker returns no processed item and cannot mark complete. | Passed. |
| Connector worker loses lease before finalization. | No stale completed audit; later worker completes. | Passed. |
| Automation delivery worker loses lock before finalization. | Stale worker cannot complete delivery; later worker completes second attempt. | Passed. |

## Remaining risks

The guards intentionally return non-processed results for lost leases; operational alerting depends on existing logs and the new lease-loss audit/log events.
