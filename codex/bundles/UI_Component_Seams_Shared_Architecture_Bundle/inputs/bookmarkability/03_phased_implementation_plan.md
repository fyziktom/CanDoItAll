# Fázovaný implementační plán

## Bookmarkovatelnost a navigační stav CanDoItAll

**Baseline:** `main` `10a72521aae7`, produkčně shodný s `development` `14b608bcd2d8`  
**Cíl:** připravit postupné implementační bundles bez big-bang refaktoru  
**Zásadní omezení:** agent settings zůstává dialog/overlay, nikoli samostatná fullscreen page

> **Doporučené pořadí:** nejprve ADR a obecná infrastruktura, potom kompletní pilot Agents včetně shared providers a historie, následně reusable UI seams a teprve potom modulové migrace a případná IA route redesign.

# 1. Programové principy

1. Každý bundle musí mít úzkou odpovědnost a vlastní acceptance evidence.
2. Bookmarkability nesmí být zaměněna za kompletní UI redesign.
3. Staré odkazy musí fungovat během compatibility window.
4. Feature migrace nesmí obejít obecnou navigator/codec vrstvu.
5. Každý bundle spouští pouze cílené testy; broad gate až na definovaných checkpointách.
6. Route-driven overlay je plnohodnotný cílový pattern.
7. Vývoj musí průběžně kontrolovat SSR, Back/Forward a Workbench restore.

# 2. Přehled fází a bundles

| Fáze | Bundle | Hlavní výstup | Riziko |
|---|---|---|---|
| 0 | NAV-00 | ADR, URL vocabulary, inventory, acceptance contract | nízké |
| 1 | NAV-01 | Obecná state/codec/navigator/history/overlay infrastruktura | střední |
| 2 | NAV-02A | Agents hlavní state, agent/team a route-driven agent dialog | vysoké |
| 2 | NAV-02B | Providers, shared providers, provider sections a overlays | vysoké |
| 2 | NAV-02C | Provider request history filters, page a entry detail | vysoké |
| 3 | NAV-03 | Reusable tabs/list/dialog seams + Collaboration/Resources/Settings pilots | střední |
| 4 | NAV-04 | Processes, Workflows, Scheduler, Calendar a Test Lab | vysoké |
| 5 | NAV-05 | CRM/HR, Projects a Workbench integration | vysoké |
| 6 | NAV-06 | Selective canonical route migration a compatibility redirects | vysoké |
| 7 | NAV-07 | MAUI navigation adapter, broad regression, docs a governance | střední |

# 3. NAV-00 - Architecture decision and inventory

## Cíl

Uzavřít pravidla, která nesmí jednotlivé feature bundles znovu rozhodovat.

## Scope

- ADR: URL state taxonomy;
- path versus query decision table;
- route-driven overlay pravidlo;
- Push/Replace policy;
- canonical token conventions;
- invalid/not-found/forbidden behavior;
- compatibility window;
- Workbench logical identity model;
- MAUI host contract;
- inventory všech route-bearing pages a jejich state owners.

## Deliverables

```text
docs/architecture/navigation-state-adr.md
docs/architecture/navigation-state-inventory.md
docs/architecture/url-vocabulary.md
docs/architecture/navigation-test-contract.md
```

## Acceptance gate

- schválený ADR bez otevřeného blockeru;
- žádný konflikt mezi UX route proposal a agent dialog rozhodnutím;
- každá page má klasifikované URL/persisted/transient/sensitive stavy;
- definované pilot URL příklady;
- definované bundle boundaries NAV-01 až NAV-07.

## Out of scope

- produkční změny routingu;
- redesign komponent;
- migrace route family.

# 4. NAV-01 - General navigation-state foundation

## Cíl

Dodat obecnou, feature-neutral vrstvu, na které nebudou moduly duplikovat parser, normalizaci a history behavior.

## Implementace

### Contracts

- `UrlHistoryMode`;
- `IPageUrlStateCodec<TState>`;
- `IPageUrlNavigator`;
- `AppLocation`;
- `NavigationStateTransition<TState>`;
- `IAppNavigationHistory`;
- canonicalization result model;
- optional route-overlay parent marker.

### Blazor implementation

- build URI přes `GetUriWithQueryParameters`;
- owned-key clearing;
- foreign query a fragment preservation;
- no-op detection;
- Push/Replace přes `NavigationOptions`;
- optional `HistoryEntryState`;
- safe Back/fallback helper pro overlay;
- logging bez citlivých query values.

### Testing helpers

- codec roundtrip assertions;
- canonicalization idempotence helper;
- fake navigation history;
- test cases pro fragment/foreign params;
- test cases pro overlapping navigation.

## Doporučené umístění

```text
src/UI/CanDoItAll.AppComponents/Navigation/
tests/Components/CanDoItAll.Tests.Components/Navigation/
tests/Unit/CanDoItAll.Tests.Unit/Navigation/
```

## Acceptance gate

- unit coverage pro Push, Replace, no-op a key removal;
- codec test fixture funguje na sample state;
- žádný feature-specific token v obecné vrstvě;
- žádná závislost na AgentFramework/Workspace/Processes;
- render/SSR sample funguje bez JS;
- broad build UI/AppComponents + relevant unit tests.

## Rizika

- příliš chytrá base class;
- leak feature types do shared UI;
- nekonečná canonicalization loop;
- nechtěná ztráta fragmentu;
- HistoryEntryState použitý jako jediný zdroj identity.

# 5. NAV-02A - Agents state and route-driven agent dialog

## Cíl

Dokázat celý pattern na nejviditelnější ploše a zachovat agent settings jako modal.

## Scope

- nahradit/rozšířit `AgentWorkspaceRouteState` na plný `AgentsPageUrlState`;
- odstranit ruční fallback parser `TryGetQueryValue`;
- sjednotit parse/normalize/serialize;
- UI výběr agenta a teamu zapisuje URL;
- `agentSection` je stabilní string token;
- routovatelná page vlastní otevření/zavření detailu;
- `AgentDetailsDialog` controlled section API;
- Back/Forward/refresh/direct link;
- dirty editor navigation lock;
- Workbench restore URI.

## Doporučené URL

```text
/agents?tab=agents
/agents?tab=agents&teamId=<guid>
/agents?tab=agents&agentId=<guid>&agentSection=runtime
```

## Nutný refaktor odpovědností

```text
AgentCatalogPanel
  před: vybírá + imperativně otevírá dialog
  po: emituje SelectAgent/OpenAgent intent

AgentsHomePage
  před: část route state
  po: jediný owner AgentsPageUrlState

AgentDetailsDialog
  před: vlastní selectedTabIndex
  po: controlled AgentDetailsSection
```

## Acceptance gate

- kliknutí, refresh a pasted URL vedou do stejného dialogu/section;
- Back po otevření zavře dialog;
- Forward jej znovu otevře;
- změna section neznovu načte katalog;
- direct deep link Close má parent fallback;
- invalid agentSection se jednou normalizuje;
- unauthorized agent nevykreslí data;
- bUnit + Playwright evidence.

# 6. NAV-02B - Providers and shared providers

## Cíl

Zahrnout nové shared provider funkce do stejného kanonického state modelu místo dalšího lokálního řešení.

## Scope

- `providerId`;
- `providerSection` tokens: connection, prices, runtime, thinking, sharing, history;
- outbound provider selection;
- provider editor section controlled state;
- shared connections route-driven overlay;
- zachování selected provider po refreshi;
- source-managed provider behavior;
- selected provider deletion normalization;
- Workbench restore.

## URL příklady

```text
/agents?tab=providers&providerId=<guid>
/agents?tab=providers&providerId=<guid>&providerSection=sharing
/agents?tab=providers&providerId=<guid>&overlay=shared-connections
```

## Acceptance gate

- žádný automatický first-provider fallback při explicitním invalid ID bez oznámení;
- shared provider dialog se obnoví z URL;
- source-managed readonly stav se obnoví konzistentně;
- provider selection callback vytváří Push;
- editor section má stable key, ne numeric public identity;
- focused tests pro local, shared-import a unavailable provider.

# 7. NAV-02C - Provider request history

## Cíl

Udělat auditní/diagnostický workspace reprodukovatelný bez zápisu filter draftů nebo citlivého obsahu do URL.

## Scope

- applied filter URL model;
- date range, provider, outcome, operation, workload a bezpečné caller keys;
- page/cursor state;
- selected entry detail overlay;
- global a provider-scoped host;
- Search jako commit boundary;
- invalid/expired cursor behavior;
- history detail authorization;
- URL length/security review.

## URL příklady

```text
/agents?tab=request-history&providerId=<guid>&outcome=failed&page=2
/agents?tab=request-history&entryId=<opaque-id>
/agents?tab=providers&providerId=<guid>&providerSection=history&page=3
```

## Acceptance gate

- draft typing nemění URL;
- Search vytvoří jednu Push entry;
- Previous/Next jsou bookmarkable;
- Back z detailu vrátí stejnou výsledkovou stránku;
- content-load action a citlivý obsah nejsou v URL;
- provider-scoped a global history používají jeden codec model;
- tests ověří denied/not-found/canceled query.

# 8. NAV-03 - Reusable UI seams and smaller migrations

## Cíl

Ověřit, že obecná vrstva funguje mimo Agents a že reusable komponenty podporují controlled navigation state.

## Shared UI scope

- `SecondaryTabs` optional Href a stable key semantics;
- `Tabs` controlled key API nad indexem;
- list/browser committed-search callbacks;
- controlled pagination/filter model;
- route-driven overlay host;
- focus restoration hooks;
- architecture guard proti child-owned page query.

## Feature pilots

### Collaboration

```text
/collaboration?view=inbox&read=unread&threadId=<guid>
```

- view/filter/selection atomicky;
- selected thread validity;
- Push místo plošného Replace.

### Resources

```text
/resources?view=registry&resourceId=<guid>&projectId=<guid>
/resources?view=browse&projectId=<guid>&path=<safe-token>
```

- outbound selected resource;
- Registry/Browse;
- applied filters podle schváleného rozsahu.

### Settings

- tab changes Push;
- Providers pseudo-tab nahradit explicitním cross-module linkem;
- provider-history policy zůstává Settings-owned;
- secrets: URL může nést `secretId`, nikdy secret value; rozhodnout, zda je detail shareable nebo pouze session state.

## Acceptance gate

- tři rozdílné stránky používají stejný navigator;
- žádný nový ruční query builder;
- reusable component API neobsahuje feature query keys;
- accessibility keyboard tests;
- focused Playwright Back/Forward matrix.

# 9. NAV-04 - Operational workspaces

## Cíl

Migrovat plochy, kde ztráta state nejvíce komplikuje reálnou práci a support.

## 9.1 Processes

- definitions/activity/live/runs view;
- processId/runId/launchPlanId;
- search/filter/page;
- explicit run ID vždy viditelný bez ohledu na default range;
- rozhodnutí: plný run detail page versus route-driven large overlay;
- compatibility `/processes/live?runId=...`.

## 9.2 Workflows

- dashboard/catalog/editor/history/analytics section;
- workflowId/runId/projectId;
- history page/filter;
- editor substate pouze pokud je sdílitelný;
- direct run detail.

## 9.3 Scheduler

- calendar/schedules/new/history section;
- selected plan;
- calendar date/view;
- applied schedule filters;
- edit dialog route state podle dirty-state policy.

## 9.4 Project Calendar

- explicit URL view/date/event/scope/timezone;
- precedence URL > persisted project/user view state > default;
- view persistence zůstává preference, nikoli jediný share mechanismus.

## 9.5 Test Lab

- planId/projectId;
- detail section;
- applied list filters;
- outbound plan selection.

## Checkpoint gate

Po NAV-04 spustit broad navigation regression:

- full component test project;
- relevant unit suites;
- Playwright smoke pro Agents, Processes, Workflows, Scheduler, Calendar, Test Lab;
- Workbench restore after app restart;
- legacy URL suite.

# 10. NAV-05 - CRM/HR, Projects and Workbench

## Cíl

Migrovat největší list-detail plochy a odstranit nekonzistentní dialog/history behavior bez nutnosti převést všechny detaily na samostatné stránky.

## CRM/HR

- Directory: partyId, section, list filter/page;
- CRM: accountId, opportunityId, interactionId, section;
- Workforce: partyId, section, filters/page;
- Recruiting: applicationId/partyId, section, stage/page;
- CRM Agents: partyId, section, validation/page;
- Assignments: projectId, active workspace a pouze jeho applied filter/page.

## Projects

- portfolio state;
- hierarchy scope versus selected project;
- project modal route state;
- Gantt/Structure/Calendar relationships;
- rozhodnout, které UX proposal paths budou zavedeny až v NAV-06.

## Workbench

- feature-provided descriptor registry;
- logical tab identity oddělená od restore URI;
- artifact-specific tab pouze při explicitním příkazu;
- canonical location se ukládá po každé změně state;
- stale/deleted entity handling.

## Acceptance gate

- všechny detail overlays obnovitelné;
- contextual links nesou selected identity;
- toolbar duplicates vyhodnoceny podle UX auditu;
- Workbench reopen vrací poslední detail/section/filter;
- žádná query změna nevytváří nechtěný nový logical tab.

# 11. NAV-06 - Selective canonical route migration

## Cíl

Až po ověření state vrstvy rozhodnout, které části UX route proposal opravdu přinášejí hodnotu.

## Kandidáti s vysokou hodnotou

- `/workflows` místo `/agents/workflows`;
- `/processes/runs/{runId}` pro plnohodnotný run detail;
- project route family;
- `/collaboration/inbox|threads|escalations`;
- `/settings/<section>`;
- `/resources/browse`.

## Kandidáti, které nemusí být samostatné pages

- agent settings;
- provider settings;
- CRM/HR list-detail records;
- prompt editor;
- history evidence dialog.

Mohou získat path alias a stále se vykreslit jako overlay.

## Compatibility

- old -> new mapping registry;
- Replace redirect;
- zachování relevantních query filterů;
- telemetry/logging legacy route use bez citlivých hodnot;
- test fixtures pro staré dashboard/notifikační odkazy;
- removal criteria až po schváleném období.

# 12. NAV-07 - MAUI adapter and closure

## Cíl

Zajistit, že navigační model není závislý na viditelném browser chrome a je připravený pro budoucí MAUI wrapper.

## Scope

- `IAppNavigationHistory` host adapter contract;
- browser implementation hardening;
- MAUI/WebView proof-of-concept adapter nebo test double;
- native Back mapping;
- deep-link activation mapping;
- route-driven overlay presentation policy;
- Workbench restore in hosted environment;
- documentation a contributor guidelines;
- architecture linter/guardrails.

## Acceptance gate

- stejný codec/reducer bez conditional compilation;
- browser a MAUI fake mají shodné transition tests;
- Back zavře overlay před odchodem z workspace;
- canonical AppLocation lze serializovat a obnovit;
- final broad regression a accessibility audit;
- dokumentace pro nové moduly.

# 13. Test budget a gates

## Každý subbundle

- build pouze dotčených projektů;
- codec/reducer unit tests;
- component tests dotčené plochy;
- jeden až tři cílené Playwright scénáře;
- architecture/source scan;
- changed-files evidence.

## Checkpointy

| Checkpoint | Po bundle | Broad gate |
|---|---|---|
| CP1 | NAV-01 | shared navigation unit/component suite |
| CP2 | NAV-02C | kompletní Agents/provider/history suite + Playwright |
| CP3 | NAV-03 | reusable component and three-pilot regression |
| CP4 | NAV-04 | operational workspace broad gate |
| CP5 | NAV-05 | CRM/Projects/Workbench broad gate |
| CP6 | NAV-07 | full solution + full UI + compatibility suite |

# 14. Doporučená bundle struktura

```text
codex/bundles/Bookmarkable-Navigation-State/
  README.md
  CHANGE-CONTROL.md
  manifest.json
  requirements/
  architecture/
  inventories/
  plan/
  subbundles/
    NAV-00-architecture-contract/
    NAV-01-navigation-foundation/
    NAV-02A-agents-dialog-state/
    NAV-02B-provider-shared-provider-state/
    NAV-02C-provider-history-state/
    NAV-03-reusable-seams-and-pilots/
    NAV-04-operational-workspaces/
    NAV-05-crm-projects-workbench/
    NAV-06-route-migration-compatibility/
    NAV-07-maui-and-closure/
  proof/
  reviews/
  scripts/
```

Každý subbundle má:

- explicitní in-scope/out-of-scope;
- owned files nebo owner modules;
- required tests;
- no-broad-test pravidlo před checkpointem;
- acceptance evidence;
- session handoff;
- rollback/compatibility notes.

# 15. Rizikový registr

| Riziko | Dopad | Mitigace |
|---|---|---|
| Bookmarkability se změní na full IA rewrite | velmi vysoký | oddělit NAV-01/02 od NAV-06 |
| Dialog/page spor zablokuje návrh | vysoký | přijmout route identity != presentation |
| Navigation loop | vysoký | idempotent codec + no-op URI check |
| Back/Forward chaos | vysoký | central Push/Replace policy + Playwright |
| Child components dál vlastní URL | vysoký | architecture guard + controlled APIs |
| Shared provider state bude řešen ad-hoc | vysoký | zahrnout do pilotu NAV-02B |
| History URL odhalí citlivý obsah | vysoký | applied safe keys only + security review |
| Workbench vytvoří příliš mnoho tabs | střední | logical identity oddělit od restore URI |
| MAUI host vyžaduje jinou logiku | střední | platform-neutral codec/reducer + adapter |
| Numeric tab index pronikne do URL | střední | explicit token maps + tests |
| Legacy odkazy se rozbijí | vysoký | mapping registry + compatibility suite |
| Async old load přepíše nový state | vysoký | cancellation/generation fence |

# 16. Rozhodnutí potřebná před NAV-01

1. Agent settings je route-driven dialog.
2. URL versus persisted preference precedence.
3. Hybridní path/query pravidlo.
4. Push/Replace tabulka.
5. Workbench logical tab identity.
6. Overlay Close behavior pro direct deep link.
7. Canonical token vocabulary pro Agents/provider/history.
8. Compatibility window.
9. Umístění shared navigation contracts.
10. První broad checkpoint test budget.

# 17. Definition of program completion

Program je uzavřený, když:

- všechny významné workspaces mají state classification;
- high-value states jsou canonical a shareable;
- agent settings a další schválené detaily zůstávají overlaye, ale jsou route-driven;
- Back/Forward má jednotnou sémantiku;
- Workbench obnovuje přesný location state bez tab explosion;
- legacy routes mají otestovanou migraci;
- MAUI host může použít stejný state/codec/reducer;
- nové moduly mají dokumentovaný pattern a guardrails;
- full regression, accessibility a security gates projdou.

# Zdroje

- **R1 - CanDoItAll main branch baseline:** https://github.com/fyziktom/CanDoItAll/commit/10a72521aae7cbcd5d5bc2b7c16366d496ef8285
- **R2 - CanDoItAll development branch baseline:** https://github.com/fyziktom/CanDoItAll/commit/14b608bcd2d82ec86b9a4970a487a255114f17e8
- **R3 - UX/UI navigation proposal:** https://github.com/fyziktom/CanDoItAll/blob/ui-refactoring-v2/docs/navigation-proposal.md
- **R4 - UX/UI current navigation issues:** https://github.com/fyziktom/CanDoItAll/blob/ui-refactoring-v2/docs/navigation-and-structure.md
- **R5 - Current AgentWorkspaceRouteState:** https://github.com/fyziktom/CanDoItAll/blob/10a72521aae7cbcd5d5bc2b7c16366d496ef8285/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentWorkspaceRouteState.cs
- **R6 - Current AgentsHomePage state synchronization:** https://github.com/fyziktom/CanDoItAll/blob/10a72521aae7cbcd5d5bc2b7c16366d496ef8285/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentsHomePage.razor.cs
- **R7 - Current provider profile UI state:** https://github.com/fyziktom/CanDoItAll/blob/10a72521aae7cbcd5d5bc2b7c16366d496ef8285/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentProviderProfilesPanel.razor.cs
- **R8 - Current provider request history UI:** https://github.com/fyziktom/CanDoItAll/blob/10a72521aae7cbcd5d5bc2b7c16366d496ef8285/src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/History/ProviderRequestHistoryPanel.razor
- **R9 - Current Workspace settings routing:** https://github.com/fyziktom/CanDoItAll/blob/10a72521aae7cbcd5d5bc2b7c16366d496ef8285/src/Modules/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor.cs
- **R10 - Current workbench route tracking:** https://github.com/fyziktom/CanDoItAll/blob/10a72521aae7cbcd5d5bc2b7c16366d496ef8285/src/App/CanDoItAll.Web/Components/Layout/MainLayout.Workbench.cs
- **R11 - Current workbench state service:** https://github.com/fyziktom/CanDoItAll/blob/10a72521aae7cbcd5d5bc2b7c16366d496ef8285/src/Modules/CanDoItAll.Modules.Workbench/Workbench/WorkbenchTabState.cs
- **R12 - Current Resources page URL state:** https://github.com/fyziktom/CanDoItAll/blob/10a72521aae7cbcd5d5bc2b7c16366d496ef8285/src/Modules/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor.cs
- **R13 - Microsoft Learn: ASP.NET Core Blazor navigation (.NET 10):** https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0
- **R14 - W3C WAI-ARIA Authoring Practices: Tabs Pattern:** https://www.w3.org/WAI/ARIA/apg/patterns/tabs/
- **R15 - W3C WAI-ARIA Authoring Practices: Dialog Modal Pattern:** https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/
- **R16 - GOV.UK Design System: Tabs:** https://design-system.service.gov.uk/components/tabs/
- **R17 - GOV.UK Design System: Pagination:** https://design-system.service.gov.uk/components/pagination/