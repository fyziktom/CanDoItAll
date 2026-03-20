# CanDoItAll.Mcp.DotNetWatch – Codex podkladový balík

Verze balíku: **1.0.0**  
Datum: **2026-03-20**

Tento balík je navržený tak, aby z něj Codex dokázal **navrhnout, implementovat, otestovat a zrevidovat** MCP server v **C# / .NET 10**, který bude součástí solution **CanDoItAll** a bude bezpečně řídit:

- lifecycle aplikace (`dotnet watch run` / `dotnet run`)
- build solution
- spouštění testů přes `dotnet test`
- logy, waity, health-checky a diagnostiku
- cleanup osiřelých procesů
- chování Codexu tak, aby už **neobcházel** server přímým voláním `dotnet run`, `dotnet watch`, `dotnet build` a `dotnet test`

## Proč je tenhle balík jiný než běžné zadání

Klíčový problém v tvém workflow není jen „umět pustit `dotnet watch`“.  
Skutečný problém je:

1. Codex někdy aplikaci sám spustí nebo zastaví mimo dohodnutý lifecycle.
2. `dotnet watch` může držet locky na binárkách a kolidovat s buildem nebo testy.
3. Dlouhé buildy a restarty potřebují **řízené čekání**, ne nahodilé `sleep`.
4. Po pádu MCP serveru nebo agent session nesmí zůstat v systému zombie procesy.

Proto je navržený server **ne jako tenká obálka nad jedním CLI příkazem**, ale jako **lokální orchestrace vývojového běhu** pro solution CanDoItAll.

## Doporučené pořadí čtení

1. `01-executive-summary.md`
2. `02-problem-statement-and-goals.md`
3. `03-scope-nongoals-assumptions.md`
4. `04-architecture.md`
5. `05-state-machines-and-sequences.md`
6. `06-tool-contracts.md`
7. `07-configuration-model.md`
8. `08-user-stories-and-acceptance.md`
9. `09-implementation-roadmap.md`
10. `11-validation-strategy.md`
11. `13-checklists/*`
12. `14-prompts/*`
13. `17-qa-review/*`

## Co je v balíku už rozhodnuté

- **Transport:** stdio MCP server
- **Technologie:** `.NET 10`, C#, oficiální MCP C# SDK
- **Režim app startu:** `WatchRun` a `RunOnce`
- **MVP test strategy:** `dotnet test`, **nikdy** `dotnet watch test`
- **Výchozí build/test politika při běžící app:** `StopAndResume`
- **Primární filozofie waitů:** explicitní MCP wait tooly místo klientských sleepů
- **Bezpečnost:** žádné raw shell command stringy; jen strukturované CLI argumenty a path guard
- **Observabilita:** session ID, operation ID, log cursory, korelace, redakce logů
- **Recovery:** stale process registry a cleanup po restartu serveru

## Co je potřeba po převzetí balíku doplnit proti reálnému repozitáři

Tento balík je navržený tak, aby byl použitelný **hned**, ale stále počítá s tím, že Codex nebo vývojář při implementaci doplní konkrétní hodnoty z repozitáře CanDoItAll:

- přesnou cestu na startup projekt
- skutečné health endpointy
- seznam test projektů
- zda solution používá `Directory.Packages.props`
- zda se bude používat Playwright MCP nebo jiný browser tool

K tomu slouží zejména:

- `14-prompts/01-repo-discovery-prompt.md`
- `15-examples/candoitall.mcpserver.settings.example.json`
- `17-qa-review/04-known-risks-and-open-questions.md`

## Jak je zohledněný přísný QA review krok

Po sestavení původní sady podkladů jsem do balíku přidala i simulovaný, ale praktický audit z pohledu přísné QA senior manažerky:

- `17-qa-review/01-initial-qa-review.md`
- `17-qa-review/02-remediation-checklist.md`
- `17-qa-review/03-remediation-summary.md`

Audit vedl k doplnění těchto kritických částí:

- threat model
- observability a redaction politika
- failure injection plan
- compatibility matrix
- ops runbook
- risk register a open questions

## Rychlý závěr

Pokud chceš z balíku vytěžit maximum, dej Codexu nejdřív:

- `14-prompts/00-master-system-prompt.md`
- `14-prompts/01-repo-discovery-prompt.md`
- `14-prompts/02-scaffold-server-prompt.md`

A potom ho veď po fázích podle `09-implementation-roadmap.md`.

---
Tento balík je záměrně psaný tak, aby šel použít jako:
- implementační blueprint,
- zadání pro Codex,
- checklist pro code review,
- základ validačního a release procesu.
