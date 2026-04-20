# 03 — Duplication And Shared Asset Map

## Canonical Ownership Matrix

| Concern | Current observable owner(s) | Target owner | Access pattern after integration |
| --- | --- | --- | --- |
| Provider master data | Workspace | Workspace + Security | AgentFramework čte provider runtime view přes bridge, ne z vlastního store. |
| Provider execution | Workspace + AgentFramework | AgentFramework | Workspace runtime se retire/shimne a UI redirectne do Agents/Providers. |
| Resource identity | CRM-HR | CRM-HR | Processes i AgentFramework konzumují resource identities přes bridges. |
| Technical agent definition | AgentFramework sandbox models | AgentFramework module | CRM-HR používá facade a binding, ne vlastní technical registry. |
| Conversations / notifications | žádný canonical owner | Collaboration | Automation jen transportuje signals; Activity jen projektuje audit. |
| Process messaging policy | žádný explicitní owner | Processes | Collaboration/Agent runtime se dotazují na effective permissions. |
| Launch/staffing orchestration | jednoduchý `StartRunAsync` + CRM-HR staffing request | Processes | CRM-HR dodává candidate/resource data; approval/inbox jde přes Collaboration. |
| Artifacts as process evidence | Processes | Processes + managed storage | Agent runtime publikuje raw artifacts, bridge je promítá do canonical evidence records. |

## Recommended Physical Import Strategy

| Source project in AgentFramework repo | Target inside CanDoItAll | Keep / adapt / drop | Notes |
| --- | --- | --- | --- |
| `CanDoItAll.AgentFramework.Models` | `src/CanDoItAll.Modules.AgentFramework/Domain` | Adapt | Převzít modely, ale sladit namespaces a persistent ownership. |
| `CanDoItAll.AgentFramework.Core` | `src/CanDoItAll.Modules.AgentFramework/Runtime` | Adapt | Zachovat runtime seams, nahradit sandbox dependencies bridge vrstvami. |
| `CanDoItAll.AgentFramework.Maf` | `src/CanDoItAll.Modules.AgentFramework/Runtime/Maf` | Adapt | Použít jako execution engine uvnitř nového modulu. |
| `CanDoItAll.AgentFramework.Persistence` | `src/CanDoItAll.Modules.AgentFramework/Persistence` | Heavy adapt | File sandbox store nahradit scoped workspace locator a integrated stores. |
| `CanDoItAll.AgentFramework.Hosting` | `src/CanDoItAll.Modules.AgentFramework/Composition` | Adapt | `AddAgentFrameworkIntegrated` musí být skutečný integrated composition root. |
| `CanDoItAll.AgentFramework.Components` | `src/CanDoItAll.Modules.AgentFramework/Components` | Adapt | Převzít reusable components bez sandbox shell assumptions. |
| `CanDoItAll.AgentFramework.Sandbox/Components/Pages` | `src/CanDoItAll.Modules.AgentFramework/Pages` | Recompose | Převzít behavior a content, ale ne původní shell/navigation. |
| `CanDoItAll.AgentFramework.Sandbox` host bootstrap | n/a | Drop | Původní sandbox shell se neintegruje jako druhá aplikace. |

## Shared Helpers That Must Be Reused Instead Of Reimplemented

- `IClock` pro timestamps, ttl a retry cutoffs.
- `IActivityStream` pro audit trail zapisovaný z nových modulů.
- `IAutomationMessagePublisher` / `Dispatcher` pro durable orchestration transport.
- `ProcessOutboxService` jako canonical boundary mezi process state a side-effect execution.
- `SecretService` pro provider credentials.
- `ISearchIndexService` pro searchable projections nových Collaboration a Agent records.
- `IStorageCatalogService` pro managed artifact persistence.
- Existing page scaffolds, summary tiles, list/detail shells a tab components z CanDoItAll UI.
- Existing integration contract pattern z `ProjectPartyIntegrationContracts.cs` pro nové bridges.

## Explicit "Do Not Duplicate" List

- Nedělat druhý provider profile store.
- Nedělat druhý provider health/execution service.
- Nedělat druhou agent business profile edit surface s vlastním write path.
- Nedělat druhý conversation store mimo Collaboration module.
- Nedělat druhý approval inbox mimo Collaboration/Processes integrated governance.
- Nedělat druhý artifact evidence registry mimo Processes managed storage.
- Nedělat přímé AgentFramework -> CRM-HR nebo AgentFramework -> Processes writes bez bridge / outbox boundary.

## File Size And Maintainability Guardrails

- Preferovat focused services a mappers; nový service nemá míchat persistence, orchestration a UI mapping v jednom typu.
- Nové page code-behindy držet malé; shared behavior vytahovat do services nebo reusable components.
- Pokud implementace potřebuje duplicitní JSON serializer/config helper, nejprve hledat reuse v existujících modulech.
- Každá nová persistent entity musí mít jasný module owner a migration story.
