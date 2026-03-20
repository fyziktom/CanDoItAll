# Scope, non-goals a assumptions

## In scope

### 1. MCP server uvnitř solution CanDoItAll
Součástí solution bude nový projekt:

- `src/CanDoItAll.Mcp.DotNetWatch`

plus test projekty:

- `tests/CanDoItAll.Mcp.DotNetWatch.Tests`
- `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests`

### 2. Ovládání aplikace
Server bude umět:
- spustit app session v `WatchRun`
- spustit app session v `RunOnce`
- session reuse
- status
- stop
- wait
- logy

### 3. Ovládání build a test operací
Server bude umět:
- spustit build solution nebo konkrétního projektu
- spustit testy přes `dotnet test`
- řešit kolizi s běžící app session pomocí policy
- vrátit operation status, wait a logy

### 4. Recovery a housekeeping
Server bude umět:
- detekovat nečekané ukončení procesu
- uklidit stale procesy po restartu serveru
- ručně spustit cleanup
- diagnostikovat nejčastější start selhání

### 5. Bezpečnost, observabilita a validace
Server bude mít:
- konfiguraci a její validaci
- path guard
- redaction logů
- korelační ID
- testy a validační scénáře

## Out of scope pro MVP

### 1. Obecný univerzální .NET orchestrátor pro libovolné repozitáře
Tohle není produkt „pro všechny“.  
Je to server optimalizovaný pro **CanDoItAll** a jeho workflow.

### 2. Přímá browser automatizace uvnitř stejného serveru
MCP server bude připravený na spolupráci s Playwright/browser toolingem, ale v MVP:
- nespouští vlastní browser,
- nenahrazuje Playwright MCP,
- neobsahuje screenshot pipeline.

### 3. Multi-tenant / multi-workspace orchestrace
MVP předpokládá:
- jeden workspace,
- jednu hlavní aplikaci,
- jeden server process per workspace.

### 4. Distribuovaná telemetrie a produkční observabilita
Pro MVP stačí:
- lokální file/stderr logging,
- korelace,
- log redaction.

### 5. Komplexní remote transport
MVP používá stdio transport.  
HTTP streamable / polling transport je mimo scope MVP.

### 6. `dotnet watch test`
MVP jej výslovně nepoužije.

## Assumptions

### A1 — V repozitáři existuje jedna preferovaná startup aplikace
Pokud je startup projektů více, bude to řešeno konfigurací.

### A2 — Health endpoint je dostupný nebo může být doplněn
Pokud není, server použije fallback na log patterns a procesní stav, ale health endpoint je preferovaný.

### A3 — Vývoj běží na podporované .NET 10 toolchain
Server sám i ovládané projekty běží na SDK kompatibilním s `.NET 10`.

### A4 — Agent může volat více MCP toolů za sebou
Workflow předpokládá, že klient umí:
- start,
- wait,
- log polling,
- operation polling.

### A5 — Codex může měnit kód serveru i hostované aplikace
Proto jsou v návrhu silně zdůrazněná pravidla workflow, aby agent nepodkopával vlastní orchestraci.

## Explicitní design principles

### DP1 — Determinismus před „kouzlem“
Když je konflikt nebo nejistota, server vrátí explicitní outcome místo nejasného automatismu.

### DP2 — Žádné raw command injection
Server přijímá strukturované parametry, ne shell text.

### DP3 — Jediný zdroj pravdy o stavu
Stav se čte přes MCP tooly, ne odhadem z externího procesu.

### DP4 — Mutující operace se serializují
Start, stop, build, test a cleanup nepoběží nekontrolovaně paralelně nad jedním workspace.

### DP5 — Read-only operace musí být bezpečně souběžné
`status`, `logs` a diagnostika musí jít volat i během delší operace.

### DP6 — Recovery je first-class feature
Cleanup a detekce osiřelých procesů nejsou doplněk. Jsou součást produktu.

## Definition of done pro MVP

MVP je hotové až když:
- projdou všechny P0 validační scénáře,
- server nevypisuje nic mimo MCP protokol na stdout,
- `WatchRun`, build a test fungují v základním CanDoItAll flow,
- stale cleanup funguje po restartu serveru,
- Codex prompt pack a checklists pokrývají správné používání nástrojů.
