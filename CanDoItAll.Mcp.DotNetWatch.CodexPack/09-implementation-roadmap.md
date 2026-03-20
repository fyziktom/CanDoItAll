# Implementation roadmap

## Přehled fází

| Fáze | Název | Cíl | Exit criteria |
|---|---|---|---|
| 0 | Repo discovery | Zjistit skutečné cesty a integraci v CanDoItAll | Máme startup project, health endpoint, test projekty, package management model |
| 1 | Skeleton server | Rozběhnout čistý stdio MCP host v .NET 10 | Server startuje, tool handshake funguje, stdout je čisté |
| 2 | Core app lifecycle | Implementovat start/stop/status/logs pro app | WatchRun a RunOnce fungují end-to-end |
| 3 | Wait engine + health | Implementovat wait conditions a health probing | Healthy/QuietSinceCursor jsou stabilní |
| 4 | Build/test operations | Implementovat build/test včetně preemption policy | Build/test fungují s StopAndResume |
| 5 | Recovery + diagnostics | Stale registry, cleanup a start diagnostics | Cleanup a diagnosis pokrývají P0/P1 scénáře |
| 6 | Integration tests | Přidat fixture aplikace a validační automatizaci | Všechny P0 scénáře jsou automatizované |
| 7 | Hardening + release | QA, redaction, docs, runbook, checklisty | Release ready bez blockerů |

## Fáze 0 — Repo discovery

### Úkoly
- najít `CanDoItAll.sln`
- najít výchozí startup projekt
- najít health endpoint nebo určit, že je potřeba doplnit
- najít test projekty
- zjistit, zda solution používá `Directory.Packages.props`
- zjistit, zda už v repo existují utility pro procesní běh, logy nebo health checks

### Výstupy
- potvrzená cesta na solution
- potvrzený startup project path
- draft konfiguračního souboru s reálnými cestami
- seznam integration test fixtures, které je potřeba přidat

### Exit criteria
- žádná klíčová cesta není „hádaná“
- je rozhodnuto, kde bude projekt přidaný v solution

## Fáze 1 — Skeleton server

### Úkoly
- vytvořit `src/CanDoItAll.Mcp.DotNetWatch`
- nastavit `net10.0`
- přidat MCP C# SDK package
- vytvořit `Program.cs` s `Host.CreateEmptyApplicationBuilder(settings: null)`
- zaregistrovat stdio transport a tool discovery
- nastavit logging jen na stderr/file
- přidat minimální `workspace_info` tool
- přidat config binding a validaci

### Exit criteria
- server buildí
- server se spustí
- `workspace_info` funguje
- nic uživatelského neleze na stdout mimo protokol

## Fáze 2 — Core app lifecycle

### Úkoly
- implementovat `AppSession`
- implementovat `ProcessSupervisor`
- implementovat kill tree abstrakci
- implementovat `AppRuntimeManager`
- implementovat `candoitall_app_start`
- implementovat `candoitall_app_stop`
- implementovat `candoitall_app_status`
- implementovat `candoitall_app_logs`
- zachytávat stdout/stderr child procesu do log bufferu
- parsovat základní URL log patterns

### Exit criteria
- WatchRun funguje
- RunOnce funguje
- session reuse funguje
- stop uklidí celý strom procesů

## Fáze 3 — Wait engine + health

### Úkoly
- implementovat `RingLogBuffer`
- zavést monotónní cursor
- implementovat `HttpHealthProbe`
- implementovat `WaitEngine`
- implementovat `candoitall_app_wait`
- podporovat `Running`, `Healthy`, `Stopped`, `QuietSinceCursor`, `LogMatch`
- vynutit timeout semantics a last-known snapshot

### Exit criteria
- agent může čekat bez sleepů
- quiet period po watch restartu je stabilní
- health probing je bezpečný a lokální

## Fáze 4 — Build/test operations

### Úkoly
- implementovat `OperationRecord` a `OperationRegistry`
- implementovat `BuildOperationRunner`
- implementovat `TestOperationRunner`
- zavést `whenAppRunning` policy layer
- implementovat `candoitall_solution_build`
- implementovat `candoitall_tests_run`
- implementovat `candoitall_operation_status`
- implementovat `candoitall_operation_wait`
- implementovat `candoitall_operation_logs`
- implementovat runner detection pro `dotnet test`

### Exit criteria
- build/test mají operation lifecycle
- StopAndResume funguje
- tests_run nepoužívá `dotnet watch test`

## Fáze 5 — Recovery + diagnostics

### Úkoly
- implementovat `StaleProcessRegistry`
- cleanup na bootstrapu
- tool `candoitall_cleanup_stale_processes`
- implementovat `StartFailureDiagnoser`
- klasifikovat `PortInUse`, `BuildFailed`, `MissingSdk`, `HealthTimeout`, `ProcessExitedEarly`, `Unknown`
- přidat correlation IDs a system events

### Exit criteria
- server po restartu uklidí vlastní osiřelé procesy
- diagnose tool vrací actionable výstup

## Fáze 6 — Integration tests

### Potřebné fixture projekty
- `HappyPathWebApp`
- `SlowStartWebApp`
- `CompileErrorApp`
- `ProcessTreeFixture`
- `RunnerDetectionFixture` (volitelně dvě varianty)

### Úkoly
- vytvořit integrační harness
- testovat stdio discipline
- testovat app lifecycle
- testovat build/test policy
- testovat stale cleanup
- testovat port conflict diagnostiku
- testovat path guard

### Exit criteria
- všechny P0 scénáře automatizované
- P1 scénáře mají aspoň semi-auto coverage nebo jasný plán

## Fáze 7 — Hardening + release

### Úkoly
- projít všechny checklisty
- doplnit runbook
- doplnit redaction pravidla
- doplnit compatibility matrix
- spustit failure injection plan
- udělat self-review prompt pass
- aktualizovat references a příklady

### Exit criteria
- není žádný otevřený blocker P0/P1
- dokumentace odpovídá skutečné implementaci
- Codex prompt pack je v souladu s hotovým kódem

## Kritické závislosti mezi fázemi

- Fáze 1 je blokovaná repo discovery z fáze 0.
- Fáze 3 závisí na log bufferu z fáze 2.
- Fáze 4 závisí na mutation locku a app stop/start z fáze 2.
- Fáze 5 závisí na process registry z fáze 2.
- Fáze 6 je průřezová, ale plné P0 scénáře potřebují fáze 2–5.

## Doporučený pracovní styl pro Codex

Pro každou fázi drž tento cyklus:

1. přečti relevantní docs z tohoto balíku
2. navrhni mini-plan konkrétních souborů
3. implementuj jen jednu fázi nebo sub-fázi
4. buildni jen dotčené projekty
5. spusť testy
6. proveď self-review proti checklistu
7. až potom pokračuj dál

## Milníky pro review

### Milník A — Skeleton ready
Po fázi 1:
- lze pustit server
- je vidět `workspace_info`

### Milník B — Runtime ready
Po fázi 3:
- lze spustit app a čekat na healthy

### Milník C — Build/test orchestration ready
Po fázi 4:
- build/test už neobcházejí lifecycle

### Milník D — Recovery ready
Po fázi 5:
- zvládáme stale procesy a diagnostiku

### Milník E — Release ready
Po fázi 7:
- balík a implementace jsou připravené na rutinní používání

## Co explicitně neodkládat na konec

Tyto věci musí být od začátku součást implementace, ne pozdější leštění:

- stdout discipline
- path guard
- correlation IDs
- log cursoring
- mutation lock
- kill tree
- stale process registry

Když se přidají pozdě, obvykle rozbijí API nebo interní model.
