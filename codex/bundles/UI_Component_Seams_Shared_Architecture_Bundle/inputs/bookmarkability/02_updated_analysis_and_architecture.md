# Bookmarkovatelnost UI stavu CanDoItAll

## Aktualizovaná analýza a cílová architektura

**Datum:** 3. 9. 2026  
**Meeting:** 4. 9. 2026  
**Aktuální `main`:** `10a72521aae7cbcd5d5bc2b7c16366d496ef8285`  
**Aktuální `development`:** `14b608bcd2d82ec86b9a4970a487a255114f17e8`  
**Předchozí analyzovaný baseline:** `1625b336e4f60ddb64987240c3a3dc485591d20f`  
**UX/UI návrh:** `ui-refactoring-v2/docs/navigation-proposal.md`

> **Architektonický verdikt:** Předchozí doporučení zůstává platné. Aktualizace shared providers přidala další významné UI stavy, ale nepřinesla společnou URL-state infrastrukturu. Doporučuji kompoziční model `state + codec + reducer + navigator`, nikoli dědičnou component base. Detail může být kanonicky adresovatelný a současně zůstat modalem/overlayem.

# 1. Rozsah a metoda

Analýza porovnává:

- aktuální produkční zdroje na `main`;
- `development`, který je produkčním kódem shodný a za `main` zaostává pouze o čtyři testovací/dokumentační commity;
- předchozí bookmarkability audit;
- UX/UI návrhy `navigation-proposal.md` a `navigation-and-structure.md`;
- aktuální doporučení Microsoftu pro Blazor .NET 10;
- přístupnostní doporučení W3C APG a UX doporučení GOV.UK Design System.

Jde o statickou revizi. Neproběhl kompletní interaktivní průchod všech 28 route-bearing Razor pages. Zdroje, které ovlivňují závěry, jsou uvedeny v registru na konci dokumentu.

# 2. Delta od předchozí analýzy

## 2.1 Repository baseline

Předchozí analýza vycházela z commitu `1625b336e4f6`. Aktuální `main` `10a72521aae7` je o 63 commitů dále. `development` `14b608bcd2d8` má proti `main` pouze čtyři chybějící commity, které mění dokumentaci a testovací infrastrukturu, nikoli produkční bookmarkability chování.

To znamená, že závěry lze formulovat vůči aktuálnímu `main` a současně je prakticky aplikovat na `development`.

## 2.2 Shared providers a request history

Nová funkcionalita zvyšuje hloubku stavového stromu na `/agents`:

```text
Agents workspace
  -> hlavní tab
      -> Providers
          -> vybraný provider
              -> editor section
                  -> Connection / Prices / Runtime / Thinking / Sharing / History
              -> Shared connections dialog
              -> provider-scoped request history
                  -> applied filters
                  -> page
                  -> selected history entry dialog
      -> Request history
          -> global scope
          -> applied filters
          -> page
          -> selected history entry dialog
```

Aktuální `AgentWorkspaceRouteState` stále reprezentuje pouze hlavní tab, `agentId`, `teamId`, Simple Chat stav a usage scope. Neobsahuje provider identity, provider section, shared connection overlay, history query, stránku ani selected history entry. [R5]

`AgentProviderProfilesPanel` drží `providerModel.Id`, `providerSearch`, `providerEditorTabIndex` a `sharedConnectionsOpen` pouze lokálně. Po načtení automaticky vybere dříve vybraný nebo první provider. Odkaz proto neumí určit, který provider a která jeho sekce se mají zobrazit. [R7]

`ProviderRequestHistoryPanel` má správně oddělený filter draft od applied query, ale applied query, cursor/page a selected entry zůstávají lokální. Jde o vhodný základ pro migraci: do URL se má zapisovat až applied query, nikoli rozepsaný draft. [R8]

## 2.3 Co se nezměnilo

Zůstávají hlavní systémové problémy:

- některé query parametry jsou pouze inbound;
- významná navigace často používá `replace: true`;
- child komponenty vlastní route-relevant stav;
- URL se skládají ručně;
- chybí společná normalizace;
- číselné tab indexy jsou používány jako interní identita;
- SSR lifecycle a rychlé změny URL nejsou řešeny jednotným mechanismem.

# 3. Současný stav: co je dobře

## 3.1 Existují použitelné základy

Aplikace už má několik kvalitních stavebních bloků:

- `[SupplyParameterFromQuery]` je použit na řadě routovatelných stránek;
- `AgentWorkspaceRouteState` centralizuje alespoň část Agents query modelu;
- některé stránky používají immutable nebo typed navigation identity pro ochranu async loadů;
- Workbench ukládá celou aktuální route včetně query stringu;
- několik detailů lze otevřít přes ID v query;
- `ProviderRequestHistoryPanel` rozlišuje draft a aplikovaný filtr;
- `SecondaryTabs` již používají sémantické string keys, což je vhodnější než index;
- compatibility redirect pro starou `/chats` route používá Replace, což je správné.

Tyto části mají být zachovány, nikoli přepsány bez důvodu.

## 3.2 Workbench je výhoda pro MAUI

`MainLayout.Workbench` sleduje aktuální route a `WorkbenchStateService` ji ukládá do obnovitelného tab session state. [R10][R11]

To podporuje hlavní návrhovou myšlenku:

- interní location může být kanonická i bez viditelného browserového address baru;
- MAUI shell může zobrazovat stejné workbench tabs a používat stejný location model;
- URL není pouze webový artefakt, ale serializovatelná navigační identita aplikace;
- není nutné převádět každý detail na fullscreen page.

Současně je třeba zpřesnit rozdíl mezi:

- `RestoreUri` - poslední přesný stav záložky;
- `LogicalTabIdentity` - zda jde stále o stejnou workbench záložku;
- `ArtifactIdentity` - volitelné otevření konkrétního agenta, runu nebo promptu v samostatném workbench tabu.

# 4. Současný stav: hlavní problémy

## 4.1 Inbound/outbound asymetrie

Stránka může přijmout parametr, ale uživatelská akce jej nezapíše zpět.

### Agents

`AgentsHomePage` přijímá `agentId`. `AgentCatalogPanel` jej umí použít pro otevření detail dialogu. Běžný selection callback však pouze aktualizuje lokální `effectiveRequestedAgentId`; navigaci neprovede. [R6]

Důsledek:

- ručně sestavený deep link může fungovat;
- běžné kliknutí nevytvoří stejný shareable stav;
- refresh nebo kopie URL může otevřený dialog ztratit;
- Workbench si uloží starší route než skutečný vizuální stav.

Stejný vzor se objevuje u Resources: stránka přijímá `resourceId` a `projectId`, ale editace a přepnutí Registry/Browse zůstávají lokální. [R12]

## 4.2 Overuse `replace: true`

Microsoft .NET 10 dokumentace říká, že `replace: true` nahradí aktuální položku browser history; výchozí chování přidá novou položku. Nejde o výkonovou volbu, ale o UX sémantiku. [R13]

V aktuálním kódu používají Replace mimo jiné:

- Agents hlavní taby a další route změny;
- Collaboration thread selection;
- Settings taby;
- zavírání některých CRM/HR detailů;
- kanonické/compatibility redirecty.

Poslední kategorie je správně. První kategorie často není.

Výsledek je nepředvídatelný Back/Forward:

- někdy Back zavře detail;
- někdy přeskočí celý workspace;
- někdy změna tabu nezanechá historii;
- někdy se detail zavírá explicitním Replace namísto návratu na parent history entry.

## 4.3 Ruční query-string konstrukce

`AgentWorkspaceRouteState.Build`, Settings a Collaboration skládají adresy ručně. To zvyšuje riziko:

- ztráty cizích query parametrů;
- ztráty fragmentu;
- nejednotného encodingu;
- rozdílného pořadí a casing parametrů;
- navigačních smyček při kanonizaci.

Microsoft poskytuje `GetUriWithQueryParameter(s)`, které umí parametr přidat, změnit nebo odstranit, používá invariantní formátování, encoding a zachovává ostatní query hodnoty. [R13]

Společný navigator má tento mechanismus využít, ale přidat aplikační politiku vlastněných klíčů, normalizace a historie.

## 4.4 Fragmentované vlastnictví

Dnešní stav je často rozdělen takto:

```text
Routovatelná page:
  - část query parametrů
  - část load lifecycle

Child panel:
  - selected entity
  - search
  - tab index
  - dialog open/close

Nested dialog:
  - selected section
  - dirty form
```

Takový strom nemá jeden kanonický stav. Child komponenta někdy mění jen lokální state, jindy volá `NavigationManager`, jindy parent provede Replace.

Microsoft povoluje přímé `[SupplyParameterFromQuery]` pouze routovatelným komponentám právě kvůli top-down toku a jednoznačnému pořadí zpracování. [R13]

## 4.5 Číselné indexy jako identita

Provider editor a mnoho dalších detailů používá `selectedTabIndex`. Index je vhodný pro render, ale ne pro URL:

- vložení nového tabu mění význam všech následujících indexů;
- změna pořadí rozbije bookmark;
- hodnota není čitelná;
- nelze udržet aliasy.

Veřejný token musí být explicitně mapovaný stabilní string, například `runtime`, `sharing`, `history`.

## 4.6 Deep link může být skryt defaultním filtrem

UX/UI analýza správně upozorňuje na `/processes/live?runId=...`, který může skončit bez výsledku, pokud defaultní časový filtr zobrazí pouze poslední hodinu. [R4]

Obecné pravidlo:

> Explicitní identita v URL má přednost před defaultním filtrem. Detail se načte přímo, přidá jako pinned result nebo se filtr explicitně rozšíří a kanonizuje.

## 4.7 Neexistuje jednotná canonicalization policy

Není systémově určeno:

- co se stane s neznámým tab tokenem;
- jak se odstraní závislý parametr bez parent identity;
- zda se invalid page opraví na 1 nebo poslední platnou;
- zda se not-found entita odstraní, nebo zobrazí explicitní stav;
- jak dlouho fungují legacy parametry;
- zda se defaulty zapisují, nebo vynechávají.

# 5. Updated inventory a priorita

| Oblast | Aktuální URL coverage | Hlavní mezera | Priorita |
|---|---|---|---|
| Agents | hlavní tab, agent/team, Simple Chat identity, usage scope | outbound agent selection, agent section, provider state, history state | P0 |
| Shared providers | žádná vlastní URL identity | provider, subtab, shared connection overlay | P0 |
| Provider request history | pouze host tab | applied query, page/cursor, selected entry | P0 |
| Processes / live | IDs a část filterů | aktivní view, deep-link override, detail history | P0 |
| Workflows | project/workflow/run IDs | hlavní section, history page/filter, editor substate | P0 |
| Projects | projectId pro modal | portfolio view, filtry, hierarchy scope, route-driven detail state | P1 |
| Collaboration | threadId | tab/view, read filter, selection validity | P1 |
| Resources | resourceId/projectId | Registry/Browse, selected resource outbound, filtry | P1 |
| Settings | tab | overuse Replace, pseudo-tab Providers, selected item/filter | P1 |
| Project Calendar | path projectId, persisted JSON | shareable view/date/event versus personal preference | P1 |
| CRM/HR Directory | partyId | editor section, list filters/page, close/back semantics | P1 |
| CRM | account/opportunity/interaction IDs | detail section, list state, history consistency | P1 |
| Workforce / Recruiting / CRM Agents | hlavní entity | detail section, list filter/page | P2 |
| Assignments | projectId | active workspace a jeho applied filters/page | P2 |
| Scheduler | prakticky pouze root route | section, plan, calendar state, applied filters | P2 |
| Plugins | root route | plugin, detail section, log scope | P2 |
| Memory | root route | provider, section, operation/event selection | P2 |
| Prompt Gallery | promptId inbound | outbound editor open/close, section, list state | P2 |
| Test Lab | planId/projectId | detail section, list filters | P2 |
| Dashboard | root | optional activity view; nízká hodnota | P3 |
| Runtime capabilities | samostatná page | téměř žádný navigační stav | bez změny |

# 6. Review UX/UI navigation proposal

## 6.1 Co přijmout

### Stable IDs a canonical links

Každý sdílitelný objekt musí mít jednoznačný durable ID v location. Souhlasím také s tím, že názvy objektů nemají být kanonickou identitou.

### Explicitní peer areas

Tam, kde jde o skutečně samostatné pracovní oblasti, dává named route smysl. Například `/processes/runs`, `/workflows` nebo project route family mohou zlepšit informační architekturu.

### Deep link má vyhrát nad defaultem

Toto pravidlo je nutné zavést napříč aplikací.

### Compatibility first

Staré odkazy musí zůstat funkční a být normalizovány pomocí Replace.

### Oddělení globální navigace, peer navigation a contextual actions

Toolbar nemá suplovat sidebar nebo secondary tabs. Kontextový odkaz má nést konkrétní selected entity a má být tak pojmenovaný.

## 6.2 Co upravit

### Path segment není vždy lepší než query

Doporučené hybridní pravidlo:

| Typ stavu | Preferovaná reprezentace |
|---|---|
| Primární samostatný resource nebo scope | path segment |
| Peer work area s vlastním load lifecycle | path segment nebo stabilní route |
| Pohled uvnitř stejného workspace | query |
| Selected record v list-detail workspace | query nebo nested path podle UX potřeby |
| Route-driven dialog/overlay | query nebo nested path; vizuálně stále dialog |
| Search/filter/sort/page/date | query |
| Anchor v dokumentu | fragment |

Nemá smysl zavádět samostatnou route pro každý subtab, pokud všechny sekce sdílejí stejný editor, data a dirty-state hranici.

### Route-addressable neznamená page

UX návrh sám připouští routed overlay jako migrační krok. Doporučuji jej povýšit na plnohodnotný cílový pattern pro:

- agent settings;
- party/account/workforce detail, pokud je primární práce list-detail;
- provider settings;
- prompt editor;
- compact run/request evidence details.

Samostatná page je vhodnější pro:

- proces run s dlouhou timeline, outputy a recovery;
- velký project management surface;
- workflow designer;
- objekt, který uživatel často otevře bez seznamového kontextu.

## 6.3 Co odložit

Kompletní přejmenování a přestavba route family nemá být součástí prvního bookmarkability bundle. Jinak se smíchají:

- URL-state infrastruktura;
- IA redesign;
- component refactor;
- compatibility redirects;
- vizuální změny;
- Workbench identity změny.

Nejdříve je nutné vytvořit stabilní obecnou vrstvu a pilot. Poté lze migrovat route family po modulech.

# 7. Cílové principy

## 7.1 URL jako autoritativní shareable state

Pro stav, který má přežít refresh, otevření v nové záložce nebo sdílení, platí:

```text
URL > persisted user preference > product default
```

Persisted preference nesmí přepsat explicitní URL.

## 7.2 Vizuální prezentace je samostatná dimenze

Stejný `AppLocation` může host vykreslit jako:

- full page;
- modal;
- side sheet;
- Workbench tab;
- MAUI native overlay;
- mobile full-screen sheet.

Tím lze zachovat agent settings dialog a současně splnit bookmarkability.

## 7.3 Routovatelná page vlastní URL

Child komponenty:

- dostávají typed state;
- dostávají případně precomputed `Href`;
- emitují typed intent;
- neparsují page query;
- neskládají page URL;
- nevolají `NavigationManager` pro vlastní parent state.

Výjimkou jsou skutečné cross-page odkazy, které nejsou změnou stavu hostitelské stránky.

## 7.4 URL obsahuje kanonický, nikoli kompletní UI snapshot

Cílem není serializovat každý pixel. URL obnovuje sémantický pracovní kontext.

# 8. Taxonomie stavu

| Kategorie | Uložení | Příklady |
|---|---|---|
| Primární identita | path/query | projectId, agentId, runId, providerId |
| Sdílitelný pohled | query | section, view, applied filter, sort, page, date |
| Osobní preference | profile DB/local storage | default page size, compact mode, preferred calendar view |
| Workspace geometrie | local/workbench state | window size/position, canvas zoom, pane width |
| Transientní UI | component memory | hover, dropdown, toast, spinner |
| Draft formuláře | editor/draft store | neuložené agent/provider hodnoty |
| Jednorázová signalizace | history state/flash state | saved, processStarted |
| Citlivé údaje | nikdy URL | secret value, API key, token, confidential payload |
| Debug/test | dev-only state | mockScenario |

# 9. Obecná architektura

## 9.1 Doporučené komponenty

```text
Browser/Maui location
        |
        v
Routable page raw parameters
        |
        v
Feature PageUrlStateCodec
  Parse -> Normalize -> Serialize
        |
        v
Immutable PageUrlState
        |
        v
Controlled child components
        |
        v
Typed UI intent
        |
        v
Feature reducer
        |
        v
PageUrlNavigator
  Build canonical URI + Push/Replace
```

## 9.2 Kontrakty

```csharp
public enum UrlHistoryMode
{
    Push,
    Replace
}

public interface IPageUrlStateCodec<TState>
{
    IReadOnlySet<string> OwnedQueryKeys { get; }

    TState Parse(Uri location);

    TState Normalize(TState state);

    IReadOnlyDictionary<string, object?> Serialize(TState state);
}

public interface IPageUrlNavigator
{
    string Build<TState>(
        Uri currentLocation,
        TState state,
        IPageUrlStateCodec<TState> codec);

    void Navigate<TState>(
        TState state,
        IPageUrlStateCodec<TState> codec,
        UrlHistoryMode historyMode,
        string? historyEntryState = null);
}
```

Feature reducer může být jednoduchá sada metod nebo explicitní intent model:

```csharp
public abstract record AgentsPageIntent;

public sealed record SelectAgent(Guid? AgentId) : AgentsPageIntent;

public sealed record ChangeAgentSection(
    AgentDetailsSection Section) : AgentsPageIntent;

public sealed record SelectProvider(Guid? ProviderId) : AgentsPageIntent;
```

## 9.3 Proč ne component base

Velká base class by musela abstrahovat:

- Blazor lifecycle;
- canonical redirect;
- async loading;
- navigation cancellation;
- dialog synchronization;
- dirty form lock;
- Workbench tracking;
- MAUI back behavior.

To by vedlo k implicitnímu a obtížně testovatelnému frameworku. Kompozice umožní každé feature zachovat vlastní load semantiku a sdílet pouze deterministické části.

Malá pomocná base class může vzniknout později pouze tehdy, když po několika migracích zůstane skutečně identický mechanický boilerplate.

## 9.4 Umístění

Doporučený základ:

```text
src/UI/CanDoItAll.AppComponents/Navigation/
  UrlHistoryMode.cs
  IPageUrlStateCodec.cs
  IPageUrlNavigator.cs
  PageUrlNavigator.cs
  PageUrlCanonicalization.cs
  RouteOverlayHistory.cs
  IAppNavigationHistory.cs
```

Feature-specific části zůstávají v modulech:

```text
src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Navigation/
  AgentsPageUrlState.cs
  AgentsPageUrlCodec.cs
  AgentsPageUrlReducer.cs
  AgentDetailsSection.cs
  ProviderDetailsSection.cs
  ProviderHistoryUrlState.cs
```

`CanDoItAll.AppComponents` již referencuje Blazor Components Web a shared UI knihovny, proto je přirozeným místem pro Blazor navigator a overlay host. [R10]

# 10. URL mutation a canonicalization

## 10.1 Owned keys

Každý codec deklaruje všechny klíče, které stránka vlastní. Při serializaci navigator:

1. začne aktuální URI;
2. nastaví všechny owned keys na `null`;
3. přidá kanonické nenulové hodnoty;
4. zachová cizí query parametry a fragment;
5. porovná výslednou URI s aktuální;
6. naviguje pouze při skutečné změně.

To je bezpečnější než ruční `string.Join('&', ...)`.

## 10.2 Stabilní tokeny

- lowercase kebab-case;
- explicitní mapování enum <-> token;
- GUID formát `D`;
- datum `yyyy-MM-dd`;
- one-based `page`;
- defaulty se vynechávají;
- `page=1` se vynechává;
- `false` se vynechává;
- lokalizované labely se nikdy nepoužívají jako token.

## 10.3 Dependency normalization

Příklady pro Agents:

```text
agentSection vyžaduje agentId
providerSection vyžaduje providerId a tab=providers
teamId je platné pouze pro tab=agents
conversationId vyžaduje tab=simple-chats a view=conversations
definitionId vyžaduje tab=simple-chats a view=definitions
history entryId vyžaduje history host
```

Normalizace musí být idempotentní:

```text
Normalize(Normalize(state)) == Normalize(state)
```

## 10.4 Neplatné entity

Rozlišit:

- syntakticky invalid ID -> odstranit/kanonizovat Replace;
- syntakticky valid, ale neexistující entita -> explicitní unavailable/not found stav;
- existující, ale nepovolená entita -> standardní authorization failure bez prozrazení dat;
- smazaná entita během otevřeného detailu -> zavřít overlay, Replace parent URL a zobrazit oznámení.

# 11. Route-driven dialog/overlay

## 11.1 Agent settings jako cílový pattern

Aktuálně `AgentCatalogPanel` imperativně otevírá `AgentDetailsDialog` přes `DialogService`. Cílově má routovatelná `AgentsHomePage` vlastnit:

```text
agentId
agentSection
isAgentDetailsOpen = agentId != null
```

Dialog dostane controlled props:

```csharp
[Parameter]
public Guid AgentId { get; set; }

[Parameter]
public AgentDetailsSection SelectedSection { get; set; }

[Parameter]
public EventCallback<AgentDetailsSection> SelectedSectionChanged { get; set; }

[Parameter]
public EventCallback CloseRequested { get; set; }
```

Otevření detailu:

```text
click agent -> reducer -> URL Push -> page state -> dialog render
```

Nikoli:

```text
click agent -> imperative dialog -> callback -> možná později URL
```

## 11.2 Back a Close

- Otevření overlaye vytvoří Push.
- Browser Back overlay zavře.
- Forward jej znovu otevře.
- Close použije Back, pokud history entry nese marker parent overlaye.
- Při přímém deep linku Close provede Replace na bezpečný parent.
- `HistoryEntryState` může nést parent marker, ale identity zůstávají v URL, protože history state se nekopíruje s odkazem. [R13]

## 11.3 Accessibility

W3C modal pattern vyžaduje focus uvnitř dialogu, omezení Tab/Shift+Tab na dialog, Escape a logické obnovení focusu. [R15]

Route-driven open musí provést stejný focus management jako kliknutí. `FocusOnNavigate` na `h1` nestačí pro query-only otevření overlaye.

## 11.4 Dirty state

Agent/provider editor může použít `NavigationLock`:

- změna subtabu stejného editoru může zůstat povolena;
- zavření detailu nebo změna entity může vyžadovat potvrzení;
- rychlé překrývající navigace musí respektovat cancellation token nejnovější navigace. [R13]

# 12. Tabs a odkazy

W3C APG definuje `tablist`, `tab`, `tabpanel`, `aria-selected`, `aria-controls` a keyboard model. Automatická aktivace je vhodná pouze bez vnímatelné latence; jinak je vhodná manual activation přes Enter/Space. [R14]

GOV.UK upozorňuje, že taby jsou vhodné pro rychlé přepínání pravidelných uživatelů, ale nemají bezmyšlenkovitě suplovat page navigation. [R16]

Doporučení pro CanDoItAll:

- hlavní routovací navigace má renderovat skutečné odkazy (`href`/`NavLink`);
- semantic tabs uvnitř workspace mohou zůstat tabs, ale každý tab má stabilní key a případně `Href`;
- child tab component emituje key, parent page rozhodne URL a history mode;
- taby s lazy server loadem mají manual keyboard activation;
- interní `SelectedIndex` je pouze render detail odvozený z key.

# 13. Push/Replace politika

## 13.1 Push

Použít, když uživatel vědomě změnil sémantickou polohu:

- hlavní tab/section;
- selected durable entity;
- otevření route-driven overlaye;
- aplikování filtru;
- stránkování;
- změna sort;
- calendar date/view;
- selected run/history entry.

## 13.2 Replace

Použít, když aplikace opravuje aktuální adresu bez nové uživatelské polohy:

- canonical casing;
- odstranění defaultu;
- legacy alias;
- invalid dependency cleanup;
- correction page out of bounds;
- compatibility redirect;
- smazaná entita;
- transient debounce mezistav, pokud se vůbec zapisuje.

## 13.3 Bez navigace

- každý jednotlivý keypress v search boxu;
- hover/focus;
- dropdown;
- toast;
- loading;
- geometry;
- dirty form field.

# 14. SSR a Blazor lifecycle

## 14.1 Parse v parameter lifecycle

URL stav aplikovat v `OnParametersSet` / `OnParametersSetAsync`, nikoli pouze v `OnInitializedAsync`. Stejná route může změnit query bez vytvoření nové instance komponenty.

## 14.2 Žádný JavaScript jako zdroj stavu

První serverový render musí umět:

- parse;
- normalize;
- rozhodnout loading/unavailable;
- vykreslit overlay shell.

JavaScript je pouze pro browser history back, focus restoration nebo scroll/geometry.

## 14.3 Async race safety

Každý load závislý na URL musí mít generation/cancellation fence:

```text
URL A -> load A
URL B -> cancel/supersede A -> load B
late A result -> ignored
```

Některé současné stránky už generation pattern používají; má se sjednotit minimální helper/test convention, ne nutně jedna base class.

## 14.4 Granular loading

Změna `agentSection` nesmí znovu načíst celý katalog. Codec/reducer musí umožnit určit dependency delta:

- identity change -> entity load;
- section change -> případně lazy section load;
- page/filter change -> list query;
- overlay close -> žádný reload katalogu.

# 15. Workbench a MAUI

## 15.1 Doporučený společný location model

```csharp
public sealed record AppLocation(
    string Path,
    IReadOnlyDictionary<string, string?> Query,
    string? Fragment);
```

Browser adapter používá `NavigationManager`. Budoucí MAUI adapter může mapovat:

- `Navigate(Push)` na WebView history nebo native navigation stack;
- `Navigate(Replace)` na replace current shell location;
- `Back()` na browser/native back;
- deep-link activation na stejný codec.

## 15.2 Workbench identity

Bookmarkability nesmí automaticky vytvořit novou Workbench záložku pro každý filter nebo subtab.

Doporučené rozdělení:

```text
LogicalTabIdentity: agents
RestoreUri: /agents?tab=agents&agentId=...&agentSection=runtime
ArtifactIdentity: agent:<id> (jen při explicitním Open in new tab)
```

`MainLayout.Workbench` už ukládá route do descriptoru. Cílová URL-state vrstva mu poskytne kanonickou route, zatímco feature/workbench resolver rozhodne logical identity. [R10][R11]

# 16. Doporučený Agents URL model

```csharp
public sealed record AgentsPageUrlState(
    AgentsWorkspaceArea Area,
    Guid? AgentId,
    Guid? TeamId,
    AgentDetailsSection? AgentSection,
    Guid? ProviderId,
    ProviderDetailsSection? ProviderSection,
    bool SharedConnectionsOpen,
    SimpleChatWorkspaceRouteState SimpleChat,
    ProviderUsageWorkloadSelection UsageScope,
    ProviderHistoryUrlState History);
```

Doporučené kanonické příklady:

```text
/agents
/agents?tab=agents&teamId=<guid>
/agents?tab=agents&agentId=<guid>&agentSection=runtime
/agents?tab=providers&providerId=<guid>&providerSection=sharing
/agents?tab=providers&providerId=<guid>&providerSection=history
/agents?tab=providers&providerId=<guid>&sharedConnections=true
/agents?tab=request-history&providerId=<guid>&outcome=failed&page=2
/agents?tab=request-history&entryId=<id>
```

`sharedConnections=true` lze případně nahradit obecnějším `overlay=shared-connections`, pokud bude na stránce více overlay typů. Důležité je nevytvořit ad-hoc boolean pro každý budoucí dialog bez pravidel.

# 17. Provider history state

## 17.1 Draft vs applied

```text
Filter controls -> local draft
Search -> validate -> applied state -> URL Push -> load
```

URL může obsahovat:

- `providerId`;
- `from`, `to`;
- `outcome`, `operation`, `workload`;
- `caller` nebo bezpečný caller key;
- `page` nebo opaque cursor;
- `entryId`.

Nemá obsahovat:

- prompt/response content;
- secrets;
- citlivý raw filter text, pokud by mohl obsahovat osobní data;
- ephemeral cancellation/loading state.

## 17.2 Pagination

GOV.UK doporučuje skutečné odkazy a jasný current page stav. [R17]

Pokud backend používá cursor pagination, URL může nést opaque cursor, ale je vhodné zvážit:

- stabilitu cursoru mezi requesty;
- maximální délku URL;
- zda je page number přesnější pro sdílení;
- jak se chová expired cursor.

# 18. Security

Microsoft upozorňuje, že route a query parametry jsou nedůvěryhodné vstupy. [R13]

Povinná pravidla:

- každý ID z URL projde server-side authorization;
- URL nikdy není důkaz přístupu;
- not-found a forbidden nesmí zbytečně prozrazovat existenci citlivého objektu;
- secret value, bearer token, prompt payload a confidential note se nikdy nezapisují;
- query se logují opatrně, protože URL často končí v telemetry/proxy/browser history;
- canonical redirect nesmí odhalit normalizovaný interní ID nepovolené entity.

# 19. Komponentové změny

## 19.1 SecondaryTabs/Tabs

Doplnit nebo standardizovat:

- stable `Key`;
- controlled `SelectedKey`;
- `SelectedKeyChanged`;
- optional `Href`;
- manual activation pro lazy panely;
- žádné URL skládání uvnitř generic component.

## 19.2 List/browser komponenty

Controlled stav:

- `SearchDraft` a `SearchCommitted`;
- `Page`;
- `Sort`;
- typed filters;
- selected entity;
- callbacks s jedním atomickým intentem.

## 19.3 Dialog/overlay

Doplnit declarative host:

- `IsOpen` odvozené z state;
- entity ID;
- selected section;
- close intent;
- focus target;
- dirty-state policy;
- idempotentní route reconciliation.

## 19.4 Workbench

Resolver by měl konzumovat feature-provided navigation descriptor:

```text
logical tab key
artifact key
canonical restore URI
title
project scope
```

Tím se zabrání centrálnímu `MainLayout` switchi, který musí ručně znát každý budoucí query parametr.

# 20. Testovací strategie

## 20.1 Unit tests codeců

- valid parse;
- invalid fallback;
- legacy alias;
- default omission;
- dependency cleanup;
- foreign query preservation;
- fragment preservation;
- roundtrip;
- canonicalization idempotence;
- stable token mapping;
- security-sensitive key exclusion.

## 20.2 Component tests

- query-only změna stejné route znovu aplikuje state;
- child intent vytvoří správnou URI;
- není navigation loop;
- section change nezpůsobí full reload;
- late async load nepřepíše novější location;
- route-driven overlay se otevře i z initial URL;
- close direct-deep-link má bezpečný fallback;
- dirty navigation lock rozlišuje section versus entity leave.

## 20.3 Playwright

Pro každou migrovanou plochu:

1. přímé vložení URL;
2. refresh;
3. kopie do nové záložky;
4. Back/Forward přes tab/entity/detail;
5. otevření a zavření overlaye;
6. změna section;
7. aplikace filtru a page;
8. invalid/legacy URL;
9. deleted/unauthorized entity;
10. rychlá navigace A -> B;
11. Workbench restore;
12. responsive/MAUI-like narrow viewport.

## 20.4 Architecture guards

- route-owning child components nesmí skládat parent URL;
- raw `NavigateTo(... replace: true)` mimo approved canonicalization helper má být review finding;
- route tokeny musí být v codec/constants, ne rozptýlené string literals;
- všechny routovatelné workspaces mají state classification manifest.

# 21. Definition of Done pro jednu stránku

1. Každý persistentní viditelný stav je klasifikován.
2. URL obnoví stejný sémantický kontext po refreshi.
3. Kopie URL funguje v nové přihlášené session.
4. Běžná UI cesta vytvoří stejnou URL jako ruční deep link.
5. Back/Forward odpovídají mentálnímu modelu.
6. Jedna uživatelská akce vytvoří nejvýše jednu history entry.
7. Defaulty jsou z URL odstraněny.
8. Invalid/legacy stav se kanonizuje nejvýše jednou.
9. Child komponenty neznají page query keys.
10. URL neobsahuje secrets ani draft values.
11. Initial SSR render je konzistentní s URL.
12. Async race je krytý.
13. Codec a reducer mají unit testy.
14. Deep-linked overlay splňuje focus a keyboard requirements.
15. Workbench uloží kanonickou restore URI.

# 22. Doporučená rozhodnutí pro ADR

- URL je authoritative shareable state.
- Visual presentation je nezávislá na route identity.
- Agent settings zůstává route-driven dialog.
- Page vlastní URL; child komponenty emitují typed intent.
- Kompozice `state + codec + reducer + navigator`, bez velké base class.
- Hybridní path/query pravidlo.
- Explicitní Push/Replace policy.
- Applied filters v URL, draft filters lokálně.
- URL má přednost před persisted preference.
- Workbench odděluje logical tab identity, restore URI a artifact identity.
- Staré odkazy mají compatibility window a Replace canonicalization.
- Pilot zahrnuje Agents, providers, shared providers a provider request history.

# Registr zdrojů

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