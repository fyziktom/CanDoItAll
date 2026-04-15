# 04 — Provider Ownership Bridge And Legacy Runtime Retirement

## Status

- `Completed`

## Objective

- Rozseknout duplicitu provider ownershipu a přesunout canonical runtime execution do AgentFrameworku.
- Zachovat Workspace/Security jako master-data a secrets ownera.
- Retire-nout starou Workspace provider execution vrstvu bez rozbití provider management UX.

## Covered Inputs

- `IN-03`, `IN-05`, `RQ-09`, `RQ-10`, `RQ-27`, `US-01`, `US-22`

## Prerequisites

- `01-foundation-import-map-and-module-skeleton` closed.
- Source-of-truth matrix accepted.

## Exact Source References

- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/ProviderExecution.cs
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor
- C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Security/SecurityModels.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Models/ProviderModels.cs
- C:\repositories\CanDoItAll.AgentFramework/src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll/tests/CanDoItAll.Tests.Components/SettingsPageProvidersTests.cs

## Deliverables

- Integrated provider catalog/credential bridge from Workspace/Security into AgentFramework runtime.
- Kill switch or retirement path for legacy Workspace provider execution adapters.
- Provider diagnostics path that runs through AgentFramework in integrated mode.
- Migration plan / redirect for provider management UI.

## Dependency Impact

- Agent runtime, CRM-HR technical agent config, scenarios i process execution závisejí na tom, že provider runtime je singular.
- Pokud tady zůstane dvojí execution path, pozdější scenario proof nebude důvěryhodný.

## Validation Depth

- `Critical foundation`
- Vyžaduje provider bridge integration tests, diagnostics proof a UI proof pro provider management surface.

## Implementation Steps

1. Definovat runtime-neutral provider contract, který AgentFramework integrated mode načítá z Workspace/Security.
2. Implementovat credential resolver backed by `SecretService` místo environment-variable-only ownershipu.
3. Odpojit nebo feature-gate-nout `ProviderExecution.cs` jako canonical runtime path.
4. Rehostovat nebo redirectnout provider management UI do Agents/Providers tak, aby master data stále žila ve Workspace store.
5. Přidat regression tests pro existing provider profiles a health checks.

## Scope Exceptions

- Fyzické odstranění legacy kódu může být dokončené až v cleanup subbundle, pokud mezikrok vyžaduje feature gate. Aktivní canonical path ale musí být už jen jedna.

## Do Not Do

- Nenechávat dva aktivní runtime execution paths.
- Nedublovat provider master data do druhé tabulky v AgentFrameworku.
- Nepřesouvat secrets do environment variables jako nový canonical owner.

## Acceptance Checklist

- Integrated AgentFramework umí načíst provider runtime view z Workspace/Security.
- Legacy Workspace execution path není canonical a je vypnutá nebo striktně gated.
- Provider health/execution proof jde přes nový bridge.
- Provider management je dostupný z Agents shellu bez rozbití ownershipu.

## Proof Required

- Integration tests pro provider mapping a credential resolution.
- Regression tests pro legacy provider profiles.
- Playwright/browser proof na provider management surface nebo redirect flow.
- Build proof affected projects.

## Browser Validation Logging

- Route: `/agents?tab=Providers` a případně `/settings` redirect.
- Viewport: `1600x900`.
- Actions: otevřít provider list, edit/create flow, health check, screenshot.
- Screenshot review: není vidět duplicitní provider management surface.

## Progression Gate

- Další AI-runtime subbundles smějí pokračovat až když provider execution má jedinou canonical cestu.
- Pokud lze stále spustit request přes starou Workspace adapter cestu, gate failuje.

## Suggested Agent Prompt

```text
Implement only subbundle 04.

Make Workspace/Security the canonical owner of provider master data and secrets, and AgentFramework the canonical owner of runtime execution. Add the integrated provider bridge, disable the old Workspace execution path as canonical, and prove health/execution through the new bridge.
```

