# Current-state analysis

## Analyzované vstupy

Byly analyzované tři zdroje:

1. původní balík `CanDoItAll.Mcp.DotNetWatch.CodexPack`,
2. původní balík `CanDoItAll.Mcp.SshOps.CodexPack`,
3. aktuální repozitářový stav `src/CanDoItAll.Mcp.DotNetWatch`.

## Závěr jednou větou

`CanDoItAll.Mcp.DotNetWatch` už není jen návrh nebo spike.  
Je to poměrně kompletní server, který už dnes obsahuje většinu společných základů, jež by `CanDoItAll.Mcp.SshOps` jinak znovu implementoval.

## Faktický snapshot aktuálního projektu

### Rozsah
- C# soubory: **18**
- Přibližný rozsah: **4224 řádků**
- Veřejné MCP tooly: **13**

### Aktuálně nalezené veřejné tooly
- `candoitall_workspace_info`
- `candoitall_app_start`
- `candoitall_app_stop`
- `candoitall_app_status`
- `candoitall_app_wait`
- `candoitall_app_logs`
- `candoitall_solution_build`
- `candoitall_tests_run`
- `candoitall_operation_status`
- `candoitall_operation_wait`
- `candoitall_operation_logs`
- `candoitall_cleanup_stale_processes`
- `candoitall_diagnose_start_failure`

### Strukturní rozpad podle složek
- `Configuration`: 500 řádků
- `Contracts`: 284 řádků
- `Diagnostics`: 116 řádků
- `GlobalUsings.cs`: 5 řádků
- `Health`: 100 řádků
- `Logging`: 164 řádků
- `Operations`: 270 řádků
- `Persistence`: 307 řádků
- `Processes`: 621 řádků
- `Program.cs`: 125 řádků
- `Runtime`: 1375 řádků
- `Security`: 104 řádků
- `Tools`: 253 řádků

## Nalezené již implementované společné stavební bloky

### 1. Wire-level contracts
Ve `Contracts/ToolContracts.cs` už existují:
- `ToolEnvelope<T>`
- `ToolError`
- `ToolInvocationException`

To je přímý kandidát na shared contract layer.

### 2. Observability primitives
Ve `Logging/LoggingModels.cs` už existují:
- `LogEntry`
- `LogReadResult`
- `RingLogBuffer`
- `FileLogStore`
- `LogRedactor`

To je velmi silný shared kandidát, protože SSH balík navrhuje:
- common response envelope,
- log streaming,
- operation logs,
- redaction.

### 3. Long-running operation model
V `Operations/OperationModels.cs` a `Runtime/SessionCoordinator.cs` už existuje:
- `OperationRegistry`
- operation status/log/wait flow
- correlation a cursorový model

SSH balík má stejné potřeby pro detached remote jobs.

### 4. Runtime identity a concurrency
V `Runtime/ServerInstanceIdentity.cs` a `Runtime/WorkspaceExecutionLock.cs` už existuje:
- instance identity
- mutation gate / serializace mutací

SSH balík plánuje per-target / per-stack locking, což je stejná architektonická rodina.

### 5. Health / probe helpers
V `Health/HealthServices.cs` je `HttpHealthProbe`.

SSH balík potřebuje:
- `http_probe`
- `http_wait`
- `cert_check`

Tady nevznikne 1:1 reuse, ale určitě sdílený základ.

### 6. Local process runtime
V `Processes/ProcessServices.cs` a `Persistence/StaleProcessRegistry.cs` je už dnes:
- process supervisor
- command runner
- process tree termination
- stale process registry
- ownership markers

To se nemá duplikovat v dalších lokálních MCP serverech.

### 7. Security / policy helpers
V `Security/SecurityServices.cs` už existuje:
- `PathGuard`
- `EnvironmentOverlayFilter`

Koncept je sdílený, ale konkrétní implementace není nutně 1:1 přenositelná na remote POSIX paths.

## Přímé průniky s původním SSH návrhem

Původní SSH pack explicitně navrhoval nebo implicitně potřeboval tyto koncepty, které už `DotNetWatch` řeší:

- common tool response envelope,
- common error model,
- log redaction,
- operation wait/log/status pattern,
- locky proti souběžným konfliktům,
- path guard,
- HTTP probe helper,
- observability a correlation.

## Důležitý architektonický závěr

### Co se musí sdílet hned
- contracts,
- errors,
- IDs,
- mutation gates,
- logging/redaction,
- generické operation primitives,
- local process runtime jako samostatná optional knihovna.

### Co se má zatím jen připravit na budoucí sdílení
- obecnější path policy abstractions,
- richer probe abstractions,
- operation journal persistence model pro detached jobs.

### Co se nemá sdílet teď
- `dotnet watch` doménová logika,
- SSH transport a host key model,
- Docker / Traefik / PostgreSQL / IPFS doménové služby.

## Doporučení pro Codex

Nezačínej `CanDoItAll.Mcp.SshOps` od nuly.  
Začni extrakcí common vrstvy z existujícího `CanDoItAll.Mcp.DotNetWatch`.
