# SB03 proof manifest

## Status

Completed.

## Changed files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | See `../SB08/transcripts/changed-file-hashes.txt` | See transcript | Convert lease renewal loss into a stop condition. |
| `src/CanDoItAll.Modules.Workspace/Connectors/ConnectorCommands.cs` | See transcript | See transcript | Add `LeaseLost` audit event kind. |
| `src/CanDoItAll.Modules.Workspace/Connectors/ConnectorOutboxService.cs` | See transcript | See transcript | Persist connector lease-loss audit and mask tokens in logs. |
| `src/CanDoItAll.Modules.Automation/Runtime/AutomationRuntimeModels.cs` | See transcript | See transcript | Add automation `DeliveryLeaseLost` telemetry kind. |
| `src/CanDoItAll.Modules.Automation/Services/AutomationMessagingServices.cs` | See transcript | See transcript | Emit delivery lease-loss telemetry when guarded finalization loses. |

## Commands

| Command | Result | Transcript |
|---|---|---|
| Focused lease/options integration filter | Passed, 5 tests | `transcripts/focused-lease-and-options-tests.txt` |
| Lease-loss source audit | Passed | `transcripts/lease-loss-source-audit.txt` |

## Source assertions

| Assertion | Source | Proof |
|---|---|---|
| Process outbox heartbeat renewal returns ownership success/failure and throws lease-lost from the monitor. | `ProcessOutbox.cs` | Source audit transcript. |
| Connector outbox records `LeaseLost` audit when stale finalization is detected. | `ConnectorCommands.cs`, `ConnectorOutboxService.cs` | Source audit transcript. |
| Automation delivery records `DeliveryLeaseLost` telemetry on stale finalization. | `AutomationRuntimeModels.cs`, `AutomationMessagingServices.cs` | Source audit transcript. |

## Negative tests

| Scenario | Expected | Result |
|---|---|---|
| Stale process worker tries to finalize after lease loss. | No final state mutation. | Passed. |
| Stale connector worker tries to finalize after lease loss. | Lease-lost audit is written, completed audit is not. | Passed. |
| Stale automation delivery worker tries to finalize after lock loss. | Delivery lease-lost telemetry is written and canonical state remains owned by the later worker. | Passed. |

## Remaining risks

Lease-loss is treated as expected contention, not fatal process failure. Alert thresholds are still a runtime operations concern.
