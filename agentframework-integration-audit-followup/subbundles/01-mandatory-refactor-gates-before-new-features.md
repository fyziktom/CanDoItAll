# Subbundle 01 — Mandatory Refactor Gates Before New Features

## Objective

Zabránit tomu, aby se zbývající integrace nalepila na už teď přetížené soubory a zhoršila maintainability.

## Files already at risk

Audit flagoval zejména:

- `src/CanDoItAll.Modules.Collaboration/CollaborationService.cs`
- `src/CanDoItAll.Modules.Collaboration/Pages/CollaborationHomePage.razor`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Web/Components/Layout/MainLayout.razor`

## Mandatory rules

1. **No more piling onto placeholder files**
   - `AgentsHomePage.razor` se nesmí „postupně vylepšovat“ do monolitu
   - místo toho se musí vytvořit reálné components / services / presenters

2. **Split Collaboration before approval work**
   - rozdělit Collaboration query a command část
   - rozdělit stránku na menší subcomponents: inbox composer, list, detail, shell counters

3. **Introduce dedicated launch-planning services**
   - launch planning, recommendation a approval logic se nesmí lepit do stávajícího `ProcessesService.Runtime.cs`
   - použít separátní coordinator / service / read query vrstvy

4. **Enforce refactor threshold**
   - žádný nově upravený non-generated soubor nesmí skončit nad 400 řádků bez explicitní refactor note
   - pokud je k doplnění feature nutné porušit threshold, práce se zastaví a nejdřív vznikne refactor subbundle

## Acceptance

- rozdělení odpovědností je zřejmé ze stromu souborů,
- další fáze nepřidávají critical logic do již přetížených souborů,
- existuje záznam o refactor gate pass/fail.

## Hard fail

Pokud Codex začne implementovat launch planning, approval, execution nebo UI recomposition přímo do stávajících velkých souborů bez splitu, subbundle failuje.
