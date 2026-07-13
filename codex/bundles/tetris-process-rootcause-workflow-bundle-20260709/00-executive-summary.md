# Executive summary

## Co se zlepšilo

Jednoduchý `Calculator` už prošel a výstup fungoval. To je důležitý signál: předchozí root příčiny typu prázdné `.slnx`, nepoužitý helper, chybějící product writeback a slepé retry u základního scaffoldingu jsou z velké části odstraněné.

## Co Tetris odhalil

Tetris je už procesně zajímavější případ: vyžaduje víc než jen vytvořit Blazor projekt a projít build/test. Má funkční požadavky z project structure: automatický game loop, spawn/move/rotate/lock/clear-line/collision/game-over rules, klávesové ovládání, barevné typy kostek, IndexedDB score storage, next-piece UI a desktop-only validaci.

Vygenerovaný produkt ale obsahoval hlavně jednoduchou Blazor shell obrazovku a zároveň v něm zůstaly default scaffold soubory:

- `Layout/MainLayout.razor` s odkazem na `learn.microsoft.com/aspnet/core/`,
- `Pages/Counter.razor`,
- `Pages/Weather.razor`,
- `wwwroot/sample-data/weather.json`.

To je reálný product defect, ale neměl vést k manager eskalaci. Měl aktivovat repair branch.

## Root příčina eskalace

`qa-validation` má dnes smíchané dva typy povinností:

1. **Acceptance proof obligations**: spusť aplikaci, naviguj browser, udělej snapshot/screenshot/console, zastav app.
2. **Defect routing obligations**: pokud je deterministicky prokázaný product defect, zvol `repair-required` a pusť `quality-repair`.

Runtime/adapter dnes vynucuje runtime/browser receipt chain i ve chvíli, kdy QA správně zvolila `repair-required` kvůli deterministickému content defectu. Tím z validního repair routingu udělá completion-gate failure, spálí retry budget a skončí v `ManagerRequired`.

## Hlavní systémové chyby

1. Required receipt gates nejsou branch-aware.
2. Completion gate issue neumí být branch-routable; všechno bezpečné jde do same-step retry budgetu.
3. Product receipt rules a `CapabilityScope.RequiredReceipts` duplikují stejný browser/runtime contract a dávají duplicitní diagnózy.
4. `ProcessStepRecoveryInstructionBuilder` obsahuje .NET a QA step-key znalosti v generické application vrstvě.
5. QA template stále umožňuje agentovi splést „chybí acceptance proof, protože jsem ho nespustil“ s „implementace má repair defect“.
6. Project-structure požadavky nejsou převedené do strojově čitelné acceptance matrix, takže QA může přijmout pouhou shell obrazovku.
7. Testy pokrývají jednotlivé gates, ale ne reálné kombinace branch outcome + receipts + deterministic product defect + retry budget.

## Doporučený směr oprav

Neopravovat hackem `qa-validation` ani hardcodem Tetris. Opravit obecný completion contract:

- zavést branch-aware/purpose-aware receipt rules,
- zavést generic issue routing z completion gate failure na branch outcome,
- odstranit duplikaci product/process receipt gate,
- přesunout doménové recovery advice do providerů nebo template metadata,
- posílit software-delivery template o acceptance criteria matrix,
- extrahovat completion gate evaluator do testovatelných služeb,
- přidat integration regression pro přesný Tetris incident bez LLM.
