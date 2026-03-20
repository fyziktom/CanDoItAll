# Executive summary

## Jednověté zadání

V solution **CanDoItAll** vznikne lokální **stdio MCP server v C# / .NET 10**, který bude vlastnit lifecycle vývojové aplikace a vývojových operací tak, aby Codex už nemusel a nesměl orchestrace řešit přímým voláním `dotnet` CLI nad app projektem.

## Nejdůležitější rozhodnutí

### 1. Server musí vlastnit celý lifecycle
Jakmile server existuje, agent nesmí spouštět CanDoItAll aplikaci přes raw `dotnet watch`, `dotnet run`, `dotnet build` ani `dotnet test`, pokud nejde o:
- bootstrap samotného MCP serveru,
- nouzovou diagnostiku mimo provozní flow,
- explicitně schválenou výjimku.

### 2. `dotnet watch` nesmí být předpoklad „na pozadí“
Tvoje zkušenost je správná: když si agent aplikaci sám spustí nebo zastaví bokem, nelze spoléhat na to, že někde už „prostě běží watcher“.  
Proto je v návrhu **session-based orchestrace**:
- `app_start`
- `app_stop`
- `app_status`
- `app_wait`
- `app_logs`

A build/test mají vlastní **operation** model:
- `solution_build`
- `tests_run`
- `operation_status`
- `operation_wait`
- `operation_logs`

### 3. Build a test musí umět řízenou preempci
Pokud běží `dotnet watch`, build a test mohou narazit na locky binárek nebo jiné kolize.  
Výchozí politika proto je:

- `whenAppRunning = StopAndResume`

To znamená:

1. server bezpečně zastaví aktivní app session
2. provede build nebo test
3. pokud to politika a stav dovolí, obnoví původní app session
4. vrátí jednotný výsledek včetně `resumeOutcome`

### 4. Žádné klientské `sleep`
Dlouhé buildy, restarty a health čekání se nesmí řešit odhadem.  
Místo toho server poskytne:
- wait tooly,
- log cursory,
- ready/healthy/quiet conditions,
- jasné timeout outcome.

### 5. MVP nepoužije `dotnet watch test`
Pro testy je v MVP pevné rozhodnutí:
- použít `dotnet test`
- nepoužít `dotnet watch test`

Důvod je stabilita a predikovatelnost.

### 6. Server musí být robustní po svém vlastním pádu
Pokud spadne MCP server nebo agent session, nesmí zůstávat osiřelé procesy.
Proto návrh obsahuje:
- registry vlastních procesů
- stale cleanup na startu
- ruční cleanup tool
- korelaci session a procesů

## Co přesně má Codex z balíku postavit

Projektová struktura:

- `src/CanDoItAll.Mcp.DotNetWatch`
- `tests/CanDoItAll.Mcp.DotNetWatch.Tests`
- `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests`

Primární vrstvy:

- MCP host + tool layer
- configuration + validation
- session coordinator
- process supervisor
- workspace execution lock
- log buffer + cursoring
- wait engine
- health probe
- build/test operation runner
- stale process registry
- diagnostics + observability + redaction

## Co z toho je MVP a co navazuje

### MVP
- stdio MCP server
- WatchRun + RunOnce
- build/test operations
- wait engine
- logs with cursors
- stale cleanup
- start failure diagnostics
- unit + integration tests

### Phase 2
- operation cancel
- richer structured diagnostics
- optional Playwright orchestration helper hooks
- richer metrics export
- multi-app profile support

## Co je největší riziko
Největší riziko není technologie MCP ani C# SDK.  
Největší riziko je **nedisciplinované používání workflow**:
- agent obejde server a pustí si `dotnet` bokem,
- klient bude čekat přes `sleep`,
- build/test nebudou respektovat preemption policy,
- logy budou parsovat nespolehlivě.

Proto balík obsahuje nejen architekturu, ale i:
- prompts,
- checklists,
- runbook,
- QA review,
- failure injection plan.

## Výsledek, který chceme
Chceme stav, kdy Codex umí tento stabilní cyklus:

1. zjistí workspace metadata
2. spustí nebo reuse-ne app session
3. udělá změnu v kódu
4. čeká na quiet/healthy
5. validuje UI
6. pokud je potřeba build nebo test, použije řízenou preempci
7. při chybě otevře diagnostiku, ne náhodné CLI příkazy

To je cílové chování celého návrhu.
