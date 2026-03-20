# Stavové automaty a sekvence

## 1. App session state machine

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Starting: app_start
    Starting --> Running: process started
    Running --> Healthy: health success / ready condition
    Healthy --> Restarting: watch rebuild / rude edit restart
    Restarting --> Running: process restarted
    Starting --> Failed: build/start failure
    Running --> Failed: fatal error
    Healthy --> Failed: fatal error
    Running --> Stopping: app_stop / preemption
    Healthy --> Stopping: app_stop / preemption
    Restarting --> Stopping: app_stop
    Stopping --> Stopped: graceful or force kill complete
    Running --> ExitedUnexpectedly: external exit
    Healthy --> ExitedUnexpectedly: external exit
    Restarting --> ExitedUnexpectedly: external exit
    ExitedUnexpectedly --> Starting: replacement start
    Failed --> Starting: retry start
    Stopped --> Starting: start again
```

## 2. Operation state machine

```mermaid
stateDiagram-v2
    [*] --> Queued
    Queued --> Running: process launched
    Running --> Completed: exit code 0
    Running --> Failed: non-zero exit / diagnostic failure
    Running --> TimedOut: timeout threshold reached
    Running --> Cancelled: future extension or server-side cancellation
```

## 3. Mutating workspace lock

Na jednom workspace smí být najednou nejvýše jedna mutující operace:

- `app_start`
- `app_stop`
- `solution_build`
- `tests_run`
- `cleanup_stale_processes`

Read-only operace mohou běžet souběžně:

- `workspace_info`
- `app_status`
- `app_logs`
- `operation_status`
- `operation_logs`
- `diagnose_start_failure`

`app_wait` a `operation_wait` jsou logicky read-heavy, ale interně si sahají na stav.  
Nemají brát mutation lock na celý svůj runtime, jen číst konzistentní snapshoty.

## 4. Sekvence – první start aplikace

```mermaid
sequenceDiagram
    participant Codex
    participant Tool as candoitall_app_start
    participant Coord as SessionCoordinator
    participant Lock as WorkspaceExecutionLock
    participant Runtime as AppRuntimeManager
    participant Proc as ProcessSupervisor
    participant Wait as WaitEngine
    participant Health as HealthProbe

    Codex->>Tool: app_start(mode=WatchRun)
    Tool->>Coord: StartAppAsync(request)
    Coord->>Lock: Acquire mutation lock
    Coord->>Runtime: ensure no conflicting session
    Runtime->>Proc: start dotnet watch process
    Proc-->>Runtime: pid, stream hooks, session created
    Runtime-->>Coord: sessionId, initial cursor
    Coord-->>Tool: session started
    Codex->>Wait: app_wait(condition=Healthy)
    Wait->>Health: poll configured health URLs
    Health-->>Wait: success
    Wait-->>Codex: satisfied=true
```

## 5. Sekvence – idempotentní start

```mermaid
sequenceDiagram
    participant Codex
    participant Tool as candoitall_app_start
    participant Coord as SessionCoordinator
    participant Runtime as AppRuntimeManager

    Codex->>Tool: app_start(reuseIfCompatible=true)
    Tool->>Coord: StartAppAsync(request)
    Coord->>Runtime: find active compatible session
    Runtime-->>Coord: existing session
    Coord-->>Tool: reused existing session
    Tool-->>Codex: reused=true, sessionId=<same>
```

## 6. Sekvence – build při běžící watch session

```mermaid
sequenceDiagram
    participant Codex
    participant Tool as candoitall_solution_build
    participant Coord as SessionCoordinator
    participant Lock as WorkspaceExecutionLock
    participant Runtime as AppRuntimeManager
    participant Ops as BuildOperationManager
    participant Proc as ProcessSupervisor

    Codex->>Tool: solution_build(whenAppRunning=StopAndResume)
    Tool->>Coord: StartBuildAsync(request)
    Coord->>Lock: Acquire mutation lock
    Coord->>Runtime: detect active app session
    Runtime-->>Coord: active session found
    Coord->>Runtime: stop session for preemption
    Runtime->>Proc: terminate process tree
    Proc-->>Runtime: stopped
    Coord->>Ops: start build operation
    Ops->>Proc: run dotnet build
    Proc-->>Ops: operation logs + exit code
    Ops-->>Coord: build result
    Coord->>Runtime: resume previous session
    Runtime->>Proc: restart app session
    Proc-->>Runtime: resumed
    Coord-->>Tool: operationId + resume outcome
```

## 7. Sekvence – testy při běžící app session

Stejný princip jako build, pouze runner je `dotnet test` a response nese test summary.

## 8. Sekvence – UI edit loop

Cílový opakovatelný flow pro Codex:

```mermaid
sequenceDiagram
    participant Codex
    participant AppStart as candoitall_app_start
    participant Logs as candoitall_app_logs
    participant Wait as candoitall_app_wait
    participant Browser as Browser/Playwright MCP

    Codex->>AppStart: app_start(reuseIfCompatible=true)
    AppStart-->>Codex: sessionId + cursor
    Codex->>Logs: app_logs(cursor=lastKnown)
    Logs-->>Codex: nextCursor
    Note over Codex: Codex edits source files in workspace
    Codex->>Wait: app_wait(condition=QuietSinceCursor, cursor=nextCursor, quietPeriodMs=2000)
    Wait-->>Codex: satisfied=true
    Codex->>Wait: app_wait(condition=Healthy, timeout=120s)
    Wait-->>Codex: satisfied=true
    Codex->>Browser: refresh / validate UI
```

## 9. Sekvence – recovery po neočekávaném exit

```mermaid
sequenceDiagram
    participant Proc as ProcessSupervisor
    participant Runtime as AppRuntimeManager
    participant Wait as WaitEngine
    participant Codex
    participant Diag as diagnose_start_failure

    Proc-->>Runtime: process exited unexpectedly
    Runtime-->>Wait: publish state change
    Wait-->>Codex: aborted/failed with latest status
    Codex->>Diag: diagnose_start_failure(sessionId)
    Diag-->>Codex: category + recommendedActions + log citations
```

## 10. Sekvence – cleanup stale procesů po restartu serveru

```mermaid
sequenceDiagram
    participant Host as MCP Host
    participant Registry as StaleProcessRegistry
    participant Proc as ProcessSupervisor
    participant OS as ProcessTreeTerminator

    Host->>Registry: load persisted managed process records
    Registry-->>Host: stale candidates
    Host->>Proc: verify candidates still alive and in workspace
    Proc->>OS: terminate stale process tree
    OS-->>Proc: killed
    Proc->>Registry: remove or mark cleaned
```

## 11. Eventy, které musí jít zapisovat do log bufferu

Minimální katalog systémových událostí:

- `SessionCreated`
- `SessionReused`
- `SessionStateChanged`
- `SessionStopped`
- `SessionExitedUnexpectedly`
- `OperationQueued`
- `OperationStarted`
- `OperationCompleted`
- `OperationFailed`
- `OperationTimedOut`
- `HealthProbeSucceeded`
- `HealthProbeFailed`
- `CleanupStarted`
- `CleanupKilledProcess`
- `CleanupSkippedProcess`
- `DiagnosticsProduced`

Tyto eventy mohou být ukládány jako standardní log entries s polem `eventType`.

## 12. Specifika watch restartů

`dotnet watch` může:
- provést hot reload bez plného restartu,
- v některých případech vynutit restart,
- při rude edit vyžadovat rozhodnutí o restartu.

Pro server to znamená:

- Session může zůstat stejná, ale `sessionVersion` se zvýší.
- `QuietSinceCursor` musí být navázáno na logovou aktivitu, ne jen na PID.
- `Healthy` je samostatná podmínka; restart bez healthy není úspěšný loop.

## 13. Doporučené interní invariants

Tyto invariants by měly být vyjádřeny testy:

1. V jednom workspace není více než jedna aktivní managed app session.
2. Každá log entry má monotónní `Sequence`.
3. Každá mutující operace má držitele workspace mutation locku.
4. `app_stop` vždy končí stavem `Stopped` nebo explicitní chybou.
5. `operation_wait` nikdy netvrdí `Completed`, pokud `operation_status` je stále `Running`.
6. `diagnose_start_failure` nic nespouští ani nemění stav.
