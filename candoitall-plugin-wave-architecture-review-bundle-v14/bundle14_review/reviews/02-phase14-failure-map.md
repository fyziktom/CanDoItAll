# Phase14 failure map

## Original hard failures now closed

### P14-001 — once-like trigger retirement

- `src/CanDoItAll.Modules.Automation/AutomationTriggering.cs`
- `tests/CanDoItAll.Tests.Integration/AutomationRuntimeIntegrationTests.cs`
- resolved by durable once-like trigger retirement after first successful fire and by restart projection skip logic for consumed once-like triggers

### P14-002 — canonical trigger snapshot return

- `src/CanDoItAll.Modules.Automation/AutomationTriggering.cs`
- `tests/CanDoItAll.Tests.Integration/AutomationRuntimeIntegrationTests.cs`
- resolved by reloading the canonical trigger row after Quartz synchronization before returning from save

### P14-003 — ingress cursor normalization and atomic upsert

- `src/CanDoItAll.Modules.Automation/AutomationIngressService.cs`
- `tests/CanDoItAll.Tests.Integration/AutomationRuntimeIntegrationTests.cs`
- resolved by shared required-value normalization and uniqueness-conflict recovery for concurrent first writes

### P14-004 — single-executor ingress materialization

- `src/CanDoItAll.Modules.Automation/AutomationIngressService.cs`
- `src/CanDoItAll.Modules.Automation/AutomationRuntimeModels.cs`
- `tests/CanDoItAll.Tests.Integration/AutomationRuntimeIntegrationTests.cs`
- resolved by the persisted `Materializing` claim state and convergent wait/finalize behavior around plugin materialization

### P14-005 — lease-bound direct connector processing

- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs`
- `tests/CanDoItAll.Tests.Integration/AutomationRuntimeIntegrationTests.cs`
- resolved by delegating the public direct processing path into the same claim-first lease-bound execution flow used by workers

## Remaining advisories

- `src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs` still catches `Exception` broadly around handler execution; cancellation handling should remain under review.
- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs` still catches `Exception` broadly around handler execution; cancellation handling should remain under review.
