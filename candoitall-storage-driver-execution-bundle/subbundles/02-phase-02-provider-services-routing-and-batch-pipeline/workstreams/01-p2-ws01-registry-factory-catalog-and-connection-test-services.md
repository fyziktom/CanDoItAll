# P2-WS01 Registry, factory, catalog, and connection-test services

## Objective

Implement runtime services that load catalog entries, resolve driver instances, test connections, and expose provider health consistently.

## Touchpoints From Workbook

| Touchpoint | Surface | Module | Scope | Required change | Proof route |
| --- | --- | --- | --- | --- | --- |
| TP-001 | Baseline storage abstraction | Infrastructure | In scope | Replace with layered storage contracts while retaining a compatibility adapter during migration. | Unit + integration tests + build |
| TP-003 | DI registrations | Infrastructure | In scope | Register registry, catalog, routing, drivers, testers, compatibility adapters, and access services. | Build + service-resolution smoke |

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor

## Ordered Implementation Tasks

1. Implement storage catalog service, driver registry, driver factory, recommendation service, and connection-test service.
2. Register concrete providers and compatibility adapters in DI.
3. Return redacted health/test details fit for UI display and logs.

## Acceptance Checklist

- Connection-test results are provider-agnostic and UI-ready.
- DI can resolve the correct driver from a catalog record without module-specific branching.
- Logs and activity records do not leak raw secrets.

## Proof Required

- Update `reviews/01-execution-report.md` with this workstream's command output or browser evidence.
- Add or update automated tests if the task changes executable behavior.
- If the task affects a UI surface, attach both desktop and narrow screenshot paths plus written findings.
- If anything is blocked, record the blocker explicitly instead of downgrading the requirement silently.

## Reopen Triggers

- A workbook touchpoint owned by this workstream has no implementation note, proof route, or linked evidence.
- Any required test command fails or is skipped.
- Any screenshot reveals clipping, overlap, overflow, inaccessible wizard navigation, or incorrect enabled/disabled actions.
- A provider is marked supported without a real protocol-backed validation path.

## Suggested Codex Prompt

```text
Implement workstream P2-WS01 only.

Objective:
Implement runtime services that load catalog entries, resolve driver instances, test connections, and expose provider health consistently.

Mandatory files to read first:
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/README.md
- C:\repositories\CanDoItAll/candoitall-storage-driver-execution-bundle/subbundles/02-phase-02-provider-services-routing-and-batch-pipeline/README.md
- C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor

Mandatory execution behavior:
- Keep comments in English.
- Update reviews/01-execution-report.md with the exact commands, screenshots, and findings for this workstream.
- Do not mark the workstream complete if required proof is blocked.
- If this workstream touches UI, run Playwright automation plus manual headed Playwright MCP with screenshots at 1900x1200 and 1366x900.
- If a screenshot shows overlap, clipping, overflow, or broken action gating, fix it before closure.
```

