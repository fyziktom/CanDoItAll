# Phase13 failure map

## Hard failures found by the new phase13 gate

### P13-001 — configuration binding
- `src/CanDoItAll.Modules.Automation/AutomationModuleServiceCollectionExtensions.cs:12`
- no matching production binding for `AutomationRuntimeOptions` was found in source code

### P13-002 — atomic idempotency
- `src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs:49-102`
- `src/CanDoItAll.Modules.Automation/AutomationIngressService.cs:19-49`
- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs:241-311`

### P13-003 — claim/lease + DB-side acquisition
- `src/CanDoItAll.Modules.Automation/AutomationMessagingServices.cs:143-167`
- `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs:335-357`
- `src/CanDoItAll.Modules.Automation/AutomationRuntimeModels.cs:130-131`

### P13-004 — worker resilience
- `src/CanDoItAll.Modules.Automation/AutomationHostedServices.cs:25-97`

### P13-005 — legacy queue seam still live
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs:688-745`
- `src/CanDoItAll.Modules.Automation/AutomationHostedServices.cs:69-97`

## Why this matters before plugins

These issues sit exactly in the generic runtime foundation that future plugins will rely on:

- runtime configuration,
- durable scheduling and dispatch,
- concurrency-safe idempotency,
- worker survivability,
- canonical job execution seams.

Leaving them unresolved would make the first plugin wave build on a runtime surface that still behaves like a single-instance prototype.
