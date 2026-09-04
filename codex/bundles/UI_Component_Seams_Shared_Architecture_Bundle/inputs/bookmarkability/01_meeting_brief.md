# Příprava na meeting: bookmarkovatelnost a navigační stav CanDoItAll

**Datum meetingu:** 4. 9. 2026  
**Technický baseline:** `main` 10a72521aae7, `development` 14b608bcd2d8  
**UX/UI podklad:** `docs/navigation-proposal.md` ve větvi `ui-refactoring-v2`  
**Charakter kontroly:** statická revize zdrojového kódu a návrhových dokumentů

> **Doporučené společné stanovisko:** URL má být kanonickým popisem sdíleného navigačního stavu, ale URL identita nesmí diktovat vizuální formu. Agent settings může a podle mého návrhu má zůstat dialogem/overlayem, přesto musí být otevřený agent, sekce dialogu a návratový kontext reprodukovatelné z URL.

## 1. Co se změnilo od předchozí analýzy

Předchozí revize vycházela z commitu `1625b336e4f6`. Aktuální `main` je o 63 commitů dále. Největší relevantní změnou je přidání shared providers a provider request history.

Aktuální `main` je proti `development` o čtyři commity napřed, ale rozdíl je pouze v dokumentaci a testech. Pro produkční kód proto lze pro meeting považovat `main` a `development` za shodný baseline.

Nové funkce nezměnily princip problému. Naopak jej zvýraznily:

- hlavní Agents workspace dostal novou sekci `request-history`;
- provider editor nyní obsahuje Connection, Prices, Runtime, Thinking, Sharing a History;
- shared provider connections se otevírají lokálním dialogem;
- provider request history má vlastní aplikované filtry, stránkování a detail záznamu;
- žádný z těchto nových stavů není plně reprezentovaný v URL.

## 2. Největší současné chyby

### Kritické

1. **Inbound a outbound stav nejsou symetrické.** URL někdy umí stav načíst, ale běžná uživatelská akce stejnou URL nevytvoří. Typickým příkladem je výběr agenta.
2. **Významné navigační změny používají `replace: true`.** Browser Back proto často nevrací uživatele na předchozí tab, detail nebo filtr.
3. **Stav je rozdroben mezi stránku a child komponenty.** Routovatelná stránka nevlastní celý URL kontrakt a child komponenty drží lokální indexy, filtry a dialogy.
4. **Chybí jednotná obecná vrstva.** Každá stránka parsuje, normalizuje a sestavuje URL jinak.
5. **Shared providers vytvořily nový hluboký strom stavu bez URL identity.** Vybraný provider, provider subtab, shared connection dialog, historie, filtry, page a detail requestu dnes nelze spolehlivě obnovit odkazem.

### Vysoké

- ruční skládání query stringů může zahodit nesouvisející parametry nebo fragment;
- číselné indexy tabů nejsou stabilní veřejný kontrakt;
- neplatné kombinace parametrů nemají jednotnou normalizaci;
- route-driven dialogy nejsou řešené jednotně;
- Workbench ukládá route, ale stránky neposkytují jednotnou navigační identitu ani obnovitelný stav;
- aplikované filtry a draft filtry nejsou systematicky rozlišeny.

## 3. Jak hodnotit návrh UX/UI kolegy

| Oblast návrhu | Stanovisko | Důvod |
|---|---|---|
| Durable ID má být v URL | **Souhlas** | Objekt musí jít otevřít po refreshi, sdílet a obnovit. |
| Deep link nesmí skrýt defaultní filtr | **Silný souhlas** | Výslovný ID parametr má přednost před defaultním rozsahem. |
| Stabilní lowercase/kebab-case tokeny | **Souhlas** | URL je veřejný dlouhodobý kontrakt. |
| Legacy odkazy mají mít redirect/normalizaci | **Souhlas** | Nelze rozbít existující dashboard, notifikace a uložené odkazy. |
| Každý peer section musí být path segment | **Upravit** | Path je vhodný pro samostatný navigační kontext; query je vhodná pro pohled uvnitř stejného workspace. Nemá to být absolutní pravidlo. |
| Každý rozsáhlejší detail musí být samostatná stránka | **Nesouhlas jako obecné pravidlo** | Route-addressable modal/overlay umí bookmark, refresh i Back a současně zachová kontext seznamu. |
| Agent settings dialog změnit na page | **Nedoporučuji** | Dialog je vhodný pro rychlou konfiguraci, zachovává katalog a dobře se mapuje do MAUI. Musí však být řízen URL stavem. |
| Kompletní IA přestavba je podmínkou bookmarkovatelnosti | **Odložit** | Nejdříve obecná state/codec/navigator vrstva; následně lze bezpečně migrovat vybrané route family. |

## 4. Klíčové rozhodnutí: route identity není totéž co stránka

Doporučený model pro agenta:

```text
/agents?tab=agents&agentId=<guid>&agentSection=runtime
```

Tato URL může být zobrazena dvěma způsoby:

- **Web:** Agents katalog na pozadí + modal s nastavením agenta.
- **MAUI:** stejný logický location state + native/webview overlay bez browserového rámu.

V obou případech platí:

- refresh otevře stejného agenta a sekci;
- kopie odkazu obnoví stejný stav;
- Back zavře dialog nebo vrátí předchozí sekci podle zvolené historie;
- UI komponenta dialogu zůstává stejná;
- URL/route model nevnucuje samostatnou fullscreen stránku.

## 5. Doporučený obecný návrh

### Základní stavební bloky

- immutable `PageUrlState` pro každou routovatelnou plochu;
- feature-specific `IPageUrlStateCodec<TState>` pro parse, normalize a serialize;
- společný `IPageUrlNavigator` pro bezpečné změny query, Push/Replace a zachování cizích parametrů;
- feature reducer nebo typed callbacks, které převádějí UI intent na nový state;
- route-driven declarative dialog/overlay host;
- volitelný `IAppNavigationHistory` adapter pro browser a budoucí MAUI shell;
- jednotná testovací sada pro roundtrip, canonicalization, Back/Forward a SSR lifecycle.

### Co nedělat

- nevytvářet velkou `BookmarkablePageBase<T>` s implicitním lifecycle;
- nenechat child komponenty samostatně skládat URL;
- neukládat do URL neuložené formuláře, secrets, geometrie oken nebo canvas zoom;
- nepoužívat `enum.ToString()` nebo číselné indexy jako veřejné tokeny;
- nepoužívat `replace: true` pro každou uživatelskou volbu.

## 6. Doporučené URL příklady pro pilot Agents

```text
/agents
/agents?tab=agents&teamId=<guid>
/agents?tab=agents&agentId=<guid>&agentSection=runtime
/agents?tab=providers&providerId=<guid>&providerSection=sharing
/agents?tab=providers&providerId=<guid>&providerSection=history
/agents?tab=request-history&providerId=<guid>&outcome=failed&page=2
/agents?tab=request-history&entryId=<history-entry-id>
/agents?tab=simple-chats&simpleChatView=conversations&conversationId=<guid>
```

Draft hodnoty provider history filtru se do URL nezapisují. URL obsahuje pouze **aplikovaný** dotaz po stisku Search.

## 7. Push vs Replace - stanovisko pro meeting

| Akce | Historie |
|---|---|
| Výběr jiného hlavního tabu | Push |
| Otevření agenta/provideru/detailu | Push |
| Přepnutí významného subtabs | Push, případně Replace jen pro velmi jemný pohled po UX rozhodnutí |
| Aplikování filtru nebo změna stránky | Push |
| Debounce mezistav search inputu | Bez navigace nebo Replace |
| Odstranění defaultního parametru | Replace |
| Oprava legacy/invalid hodnoty | Replace |
| Compatibility redirect | Replace |
| Automatické odstranění ID po smazání entity | Replace |

## 8. Rozhodnutí, která je vhodné uzavřít na callu

1. Potvrdit, že **agent settings zůstává dialogem**, ale bude route-driven.
2. Potvrdit, že bookmarkability projekt není současně povinná kompletní IA migrace.
3. Schválit hybridní pravidlo path vs query.
4. Schválit URL jako autoritativní zdroj sdíleného navigačního stavu.
5. Schválit Push/Replace politiku.
6. Potvrdit, že routovatelná stránka vlastní URL a child komponenty pouze emitují intent.
7. Rozhodnout Workbench identitu: jedna Agents záložka s měnící se route, nebo volitelné artifact tabs pro jednotlivé agenty.
8. Schválit pilot Agents včetně shared providers a request history.
9. Schválit compatibility window pro staré odkazy.
10. Schválit fázování do samostatných bundles.

## 9. Navržená agenda meetingu

1. **5 min:** cílová definice bookmarkovatelnosti.
2. **10 min:** současné chyby a změny po shared providers.
3. **10 min:** review UX navigation proposal - co přijmout, upravit a odložit.
4. **10 min:** route-driven dialog a MAUI model.
5. **10 min:** obecná technická vrstva a Push/Replace.
6. **10 min:** fáze a bundle boundaries.
7. **5 min:** potvrzení rozhodnutí a vlastníků.

## 10. Požadovaný výstup meetingu

Meeting je úspěšný, pokud vznikne krátký schválený ADR s těmito body:

- URL state taxonomy;
- path/query pravidlo;
- route-driven overlay pravidlo;
- Push/Replace tabulka;
- vlastnictví URL stavu;
- pilotní scope Agents;
- compatibility policy;
- Workbench/MAUI integrační hranice;
- pořadí bundles.

## Zdroje

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