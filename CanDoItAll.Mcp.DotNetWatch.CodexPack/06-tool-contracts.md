# Tool contracts

Tento dokument definuje navržené veřejné MCP tooly serveru.

## 1. Naming convention

Doporučené názvy toolů:

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

Prefix `candoitall_` je záměrný kvůli namespacingu a čitelnosti ve více-MCP prostředí.

## 2. Common response envelope

Doporučený společný response envelope:

```json
{
  "ok": true,
  "tool": "candoitall_app_start",
  "timestampUtc": "2026-03-20T15:41:00Z",
  "correlationId": "corr_01HQ...",
  "data": {},
  "warnings": [],
  "errors": []
}
```

Pro chyby:

```json
{
  "ok": false,
  "tool": "candoitall_solution_build",
  "timestampUtc": "2026-03-20T15:41:00Z",
  "correlationId": "corr_01HQ...",
  "error": {
    "code": "RunningSessionConflict",
    "message": "Cannot start build because a managed app session is running and whenAppRunning=Fail.",
    "details": {
      "sessionId": "app_01HQ..."
    }
  }
}
```

## 3. Common error codes

Doporučená taxonomie:

- `ValidationError`
- `ConfigurationError`
- `PathOutsideWorkspace`
- `RunningSessionConflict`
- `OperationInProgress`
- `SessionNotFound`
- `OperationNotFound`
- `Timeout`
- `ProcessStartFailed`
- `ProcessExitedEarly`
- `HealthTimeout`
- `PortInUse`
- `UnsupportedPolicy`
- `SecurityViolation`
- `InternalError`

## 4. Tool: `candoitall_workspace_info`

### Účel
Vrátí metadata o workspace, solution, default app projektu a aktivních session/operacích.

### Request
Minimální request:

```json
{}
```

Volitelné:
- `includeHistory` (`bool`, default `false`)
- `includeConfigSnapshot` (`bool`, default `false`, redacted)

### Response – `data`
```json
{
  "workspaceRoot": "/repo/CanDoItAll",
  "solutionPath": "/repo/CanDoItAll/CanDoItAll.sln",
  "defaultApp": {
    "projectPath": "/repo/CanDoItAll/src/CanDoItAll.Web/CanDoItAll.Web.csproj",
    "mode": "WatchRun",
    "healthUrls": [
      "https://localhost:7010/health"
    ]
  },
  "testProjects": [
    "/repo/CanDoItAll/tests/CanDoItAll.Web.Tests/CanDoItAll.Web.Tests.csproj"
  ],
  "activeAppSession": null,
  "activeOperations": [],
  "supportedPolicies": [
    "StopAndResume",
    "StopOnly",
    "Fail",
    "ContinueIfSafe"
  ]
}
```

## 5. Tool: `candoitall_app_start`

### Účel
Spustí app session v `WatchRun` nebo `RunOnce`.

### Request
```json
{
  "projectPath": null,
  "mode": "WatchRun",
  "configuration": "Debug",
  "framework": null,
  "launchProfile": null,
  "workingDirectory": null,
  "arguments": [],
  "environmentOverlay": {},
  "urls": [],
  "reuseIfCompatible": true,
  "conflictPolicy": "Fail",
  "waitFor": "None"
}
```

### Parametry
- `projectPath`: optional; default z konfigurace
- `mode`: `WatchRun | RunOnce`
- `configuration`: default `Debug`
- `framework`: optional
- `launchProfile`: optional
- `workingDirectory`: optional
- `arguments`: args předané aplikaci
- `environmentOverlay`: whitelistovaný overlay
- `urls`: optional explicit URL override
- `reuseIfCompatible`: default `true`
- `conflictPolicy`: `Fail | Replace`
- `waitFor`: `None | Running | Healthy`

Poznámka:
- Pokud `waitFor != None`, tool může interně zavolat wait engine a vrátit výsledek až po splnění / timeoutu.
- Doporučené pro rychlé klienty je ale často `waitFor = None` a pak explicitní `app_wait`.

### Response – `data`
```json
{
  "sessionId": "app_01K0...",
  "reused": false,
  "mode": "WatchRun",
  "state": "Starting",
  "sessionVersion": 1,
  "projectPath": "/repo/CanDoItAll/src/CanDoItAll.Web/CanDoItAll.Web.csproj",
  "observedUrls": [],
  "initialCursor": 42,
  "lastKnownPid": 12345
}
```

## 6. Tool: `candoitall_app_stop`

### Účel
Zastaví aktivní app session nebo konkrétní session.

### Request
```json
{
  "sessionId": null,
  "reason": "RequestedByClient",
  "force": false
}
```

### Response – `data`
```json
{
  "sessionId": "app_01K0...",
  "stopped": true,
  "finalState": "Stopped",
  "graceful": true,
  "killedPids": [12345, 12346],
  "finalCursor": 119
}
```

## 7. Tool: `candoitall_app_status`

### Účel
Vrátí snapshot session stavu.

### Request
```json
{
  "sessionId": null
}
```

`null` znamená „aktivní nebo poslední známá session“.

### Response – `data`
```json
{
  "sessionId": "app_01K0...",
  "state": "Healthy",
  "mode": "WatchRun",
  "projectPath": "/repo/CanDoItAll/src/CanDoItAll.Web/CanDoItAll.Web.csproj",
  "sessionVersion": 3,
  "lastKnownPid": 12345,
  "observedUrls": [
    "https://localhost:7010",
    "http://localhost:5010"
  ],
  "lastExitCode": null,
  "lastRestartUtc": "2026-03-20T15:49:44Z",
  "lastCursor": 321,
  "health": {
    "status": "Healthy",
    "lastSuccessUtc": "2026-03-20T15:49:46Z",
    "lastFailureUtc": null,
    "lastUrl": "https://localhost:7010/health"
  },
  "recentEvents": [
    "Restart completed",
    "Health probe succeeded"
  ]
}
```

## 8. Tool: `candoitall_app_wait`

### Účel
Server-side wait nad app session.

### Request
```json
{
  "sessionId": null,
  "condition": "Healthy",
  "timeoutMs": 120000,
  "pollIntervalMs": 500,
  "cursor": null,
  "quietPeriodMs": 2000,
  "logPattern": null,
  "caseInsensitive": true
}
```

### Podporované conditions
- `Running`
- `Healthy`
- `Stopped`
- `QuietSinceCursor`
- `LogMatch`

Volitelně lze přidat i:
- `RestartCompleted`

### Response – `data`
```json
{
  "sessionId": "app_01K0...",
  "condition": "Healthy",
  "satisfied": true,
  "timedOut": false,
  "elapsedMs": 6842,
  "observedState": "Healthy",
  "finalCursor": 358,
  "matchedLogEntry": null
}
```

Při timeoutu:

```json
{
  "sessionId": "app_01K0...",
  "condition": "Healthy",
  "satisfied": false,
  "timedOut": true,
  "elapsedMs": 120000,
  "observedState": "Running",
  "finalCursor": 351,
  "diagnosticHint": "Health probe did not succeed within timeout."
}
```

## 9. Tool: `candoitall_app_logs`

### Účel
Inkrementálně číst logy app session.

### Request
```json
{
  "sessionId": null,
  "cursor": null,
  "limit": 200,
  "includeStdOut": true,
  "includeStdErr": true,
  "includeSystemEvents": true
}
```

### Response – `data`
```json
{
  "sessionId": "app_01K0...",
  "entries": [
    {
      "sequence": 359,
      "timestampUtc": "2026-03-20T15:50:03.133Z",
      "source": "ProcessStdOut",
      "stream": "stdout",
      "sessionVersion": 3,
      "correlationId": "corr_01K0...",
      "text": "Now listening on: https://localhost:7010"
    }
  ],
  "nextCursor": 359,
  "truncated": false,
  "totalAvailableAfterCursor": 1
}
```

## 10. Tool: `candoitall_solution_build`

### Účel
Spustí build operaci.

### Request
```json
{
  "targetPath": null,
  "configuration": "Debug",
  "framework": null,
  "arguments": [],
  "environmentOverlay": {},
  "whenAppRunning": "StopAndResume",
  "waitForCompletion": false,
  "timeoutMs": 1800000
}
```

Výchozí `targetPath`:
- solution path z konfigurace

### Response – `data`
```json
{
  "operationId": "op_01K0...",
  "operationType": "Build",
  "state": "Running",
  "targetPath": "/repo/CanDoItAll/CanDoItAll.sln",
  "appPreemption": {
    "policy": "StopAndResume",
    "stoppedSessionId": "app_01K0...",
    "resumePlanned": true
  },
  "initialCursor": 0
}
```

Pokud `waitForCompletion=true`, response může vrátit i final outcome.  
Doporučené workflow pro Codex je však:
- `solution_build(waitForCompletion=false)`
- `operation_wait`
- `operation_logs` při potřebě detailu

## 11. Tool: `candoitall_tests_run`

### Účel
Spustí test operaci přes `dotnet test`.

### Request
```json
{
  "targetPath": null,
  "configuration": "Debug",
  "framework": null,
  "filter": null,
  "arguments": [],
  "collectCoverage": false,
  "whenAppRunning": "StopAndResume",
  "runnerPreference": "Auto",
  "waitForCompletion": false,
  "timeoutMs": 1800000
}
```

### Response – `data`
```json
{
  "operationId": "op_01K0...",
  "operationType": "Test",
  "state": "Running",
  "runner": "Auto",
  "targetPath": "/repo/CanDoItAll/tests/CanDoItAll.Web.Tests/CanDoItAll.Web.Tests.csproj",
  "appPreemption": {
    "policy": "StopAndResume",
    "stoppedSessionId": "app_01K0...",
    "resumePlanned": true
  },
  "initialCursor": 0
}
```

Final summary po dokončení by měl obsahovat:
- `total`
- `passed`
- `failed`
- `skipped`
- `exitCode`
- `artifacts`

## 12. Tool: `candoitall_operation_status`

### Účel
Vrátí snapshot build/test operace.

### Request
```json
{
  "operationId": "op_01K0..."
}
```

### Response – `data`
```json
{
  "operationId": "op_01K0...",
  "operationType": "Build",
  "state": "Completed",
  "startedUtc": "2026-03-20T15:51:00Z",
  "finishedUtc": "2026-03-20T15:51:17Z",
  "elapsedMs": 17012,
  "exitCode": 0,
  "summary": "Build succeeded.",
  "runner": null,
  "resumeOutcome": {
    "attempted": true,
    "success": true,
    "sessionId": "app_01K0..."
  },
  "lastCursor": 92
}
```

## 13. Tool: `candoitall_operation_wait`

### Účel
Čeká na dokončení operation.

### Request
```json
{
  "operationId": "op_01K0...",
  "timeoutMs": 1800000,
  "pollIntervalMs": 500
}
```

### Response – `data`
```json
{
  "operationId": "op_01K0...",
  "completed": true,
  "timedOut": false,
  "state": "Completed",
  "elapsedMs": 17012,
  "exitCode": 0,
  "summary": "Build succeeded.",
  "resumeOutcome": {
    "attempted": true,
    "success": true
  }
}
```

## 14. Tool: `candoitall_operation_logs`

### Účel
Inkrementálně číst logy build/test operace.

### Request
```json
{
  "operationId": "op_01K0...",
  "cursor": null,
  "limit": 200
}
```

### Response – `data`
```json
{
  "operationId": "op_01K0...",
  "entries": [
    {
      "sequence": 1,
      "timestampUtc": "2026-03-20T15:51:03.113Z",
      "source": "ProcessStdOut",
      "stream": "stdout",
      "correlationId": "corr_01K0...",
      "text": "Build started..."
    }
  ],
  "nextCursor": 1,
  "truncated": false
}
```

## 15. Tool: `candoitall_cleanup_stale_processes`

### Účel
Uklidit procesy, které server dříve založil, ale které přežily pád serveru nebo klienta.

### Request
```json
{
  "dryRun": false
}
```

### Response – `data`
```json
{
  "checked": 3,
  "killed": [
    {
      "pid": 23111,
      "ownerKind": "AppSession",
      "ownerId": "app_old_01K0..."
    }
  ],
  "skipped": [
    {
      "pid": 23119,
      "reason": "Process no longer exists"
    }
  ]
}
```

## 16. Tool: `candoitall_diagnose_start_failure`

### Účel
Diagnostika posledního start/test/build failu.

### Request
```json
{
  "sessionId": null,
  "operationId": null,
  "maxLogEntries": 200
}
```

Poznámka:
- Přesně jedna z hodnot `sessionId` nebo `operationId` by měla být vyplněna.
- Když nejsou vyplněny, server použije poslední failed entity.

### Response – `data`
```json
{
  "targetType": "AppSession",
  "targetId": "app_01K0...",
  "category": "PortInUse",
  "confidence": "High",
  "summary": "Application failed to bind one of the configured URLs because the port is already in use.",
  "recommendedActions": [
    "Call candoitall_app_stop if another managed session is active.",
    "Call candoitall_cleanup_stale_processes to remove orphaned managed processes.",
    "Retry with a different port configuration if the conflict is external."
  ],
  "evidence": [
    {
      "sequence": 412,
      "text": "Failed to bind to address https://127.0.0.1:7010: address already in use"
    }
  ]
}
```

## 17. Doporučený behavior contract pro klienta

Klient má dodržet tento workflow:

### Pro první start
1. `candoitall_workspace_info`
2. `candoitall_app_start`
3. `candoitall_app_wait(condition=Healthy)`

### Po úpravě UI
1. `candoitall_app_logs` -> vezmi cursor
2. proveď změnu kódu
3. `candoitall_app_wait(condition=QuietSinceCursor, cursor=<cursor>)`
4. `candoitall_app_wait(condition=Healthy)`
5. teprve potom browser validace

### Pro build/test
1. `candoitall_solution_build` nebo `candoitall_tests_run`
2. `candoitall_operation_wait`
3. při neúspěchu `candoitall_operation_logs`

### Při selhání startu
1. `candoitall_app_status`
2. `candoitall_app_logs`
3. `candoitall_diagnose_start_failure`

## 18. Chování, které je zakázané

Pro CanDoItAll workflow po zavedení serveru je zakázané, aby Codex:
- volal raw `dotnet watch run` mimo server
- volal raw `dotnet run` mimo server
- volal raw `dotnet build` nad app workflow mimo server
- volal raw `dotnet test` nad app workflow mimo server
- čekal přes klientské `sleep`, pokud může použít wait tool
- ručně zabíjel procesy bez vědomí serveru, pokud může použít `app_stop` nebo `cleanup_stale_processes`

## 19. Poznámky k budoucím rozšířením

Možné Phase 2 tooly:
- `candoitall_operation_cancel`
- `candoitall_session_replace`
- `candoitall_browser_hint`
- `candoitall_collect_diagnostics_bundle`

Tyto tooly nejsou nutné pro MVP.
