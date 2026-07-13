# Incident reconstruction

## Identifikace běhu

- Root run: `c4888f4f-eabd-469f-80a6-3fccf6018a12`
- Stav při capture: `NeedsAttention`
- Zablokovaný step: `qa-validation`
- Step instance: `1ebeadbe-98c9-4e9d-af3b-1e9f69a75c62`
- Project id: `3324868f-66e2-478a-bb8f-14f32a5db1e9`
- Product root snapshot: `C:\programovani\dotnet\output`

## Co se stalo v QA

### Attempt 1

Agent provedl restore/build/test, ale nespustil plný runtime/browser proof chain. Zvolil `repair-required`, protože chyběla browser proof evidence.

To je nesprávná branch decision. Chybějící proof, který QA sama neprovedla, není product repair defect. Je to neúplný QA step, tedy bounded same-step retry nebo `Blocked`, pokud nástroj opravdu není dostupný.

Runtime vrátil `NeedsManager` se safe retry kvůli missing receipts.

### Attempt 2

Agent převážně přepsal/rečetl evidence, ale opět neprovedl runtime/browser chain. Znovu zvolil `repair-required`. Adapter doplnil i `completed_outcome_declares_unresolved_blocker`, protože výstup tvrdil `Completed`, ale text přitom popisoval chybějící acceptance proof.

Runtime znovu spálil safe retry.

### Attempt 3

Agent provedl kompletní chain včetně:

- `workspace_dotnet_restore`,
- `workspace_dotnet_build`,
- `workspace_dotnet_test`,
- `workspace_dotnet_run`,
- `browser_navigate`,
- `browser_snapshot`,
- `browser_take_screenshot`,
- `browser_console_messages`,
- `workspace_dotnet_stop`.

Zvolil `quality-accepted`. Deterministický content gate ale našel default Blazor scaffold:

- `Counter.razor`,
- `Weather.razor`,
- `sample-data/weather.json`,
- `learn.microsoft.com/aspnet/core/`.

To je klíčový moment. Adapter měl tento acceptance-branch failure převést na `repair-required` branch signal, protože existuje repair větev. Místo toho vrátil `NeedsManager`/`SafeRetry` s `process.adapter.product_required_file_content_missing`. Tím spálil třetí retry.

### Attempt 4

Recovery instruction už agentovi jasně říká, že product content/readback failure je concrete implementation defect a má zvolit `repair-required`. Agent to udělal. Jenže adapter dál vynutil process receipt gate pro acceptance-only browser/runtime receipts a po vyčerpání budgetu převedl výsledek na `ManagerRequired`.

Tady je nejčistší důkaz root příčiny: agent zvolil správnou branch pro známý defect, ale runtime ji zablokoval požadavkem na acceptance-only proof.

## Product evidence

`product-output-snapshot/forbidden-scaffold-scan.txt` potvrzuje:

```text
MainLayout.razor: learn.microsoft.com/aspnet/core/
Counter.razor: @page "/counter"
Weather.razor: @page "/weather"
Weather.razor: WeatherForecast
Weather.razor: sample-data/weather.json
```

## Project-structure acceptance gap

Project structure obsahovala mimo jiné:

- automatický falling-piece loop,
- spawn/move/rotate/lock/clear-line/collision/game-over rules,
- distinct colors per piece type,
- keyboard-driven input,
- IndexedDB max score storage,
- next game piece UI,
- desktop-only validation.

Výstupní `Home.razor` obsahoval jednoduchý board s tlačítky `Fill sample pattern`, `Clear board`, `Restart`, nikoli plnohodnotný Tetris engine. QA acceptance proto nemá stát jen na build/test/browser screenshot a scaffold removal. Musí existovat acceptance matrix odvozená z project structure.

## Co incident neprokazuje

Hypotéza, že QA spadla kvůli stále běžící aplikaci z předchozího kroku, se v tomto capture nepotvrdila. Attempt 3 měl `workspace_dotnet_run` i `workspace_dotnet_stop`. Přesto doporučuji samostatně posílit .NET runtime lifecycle, protože u složitějších procesů je to realistické budoucí riziko.
