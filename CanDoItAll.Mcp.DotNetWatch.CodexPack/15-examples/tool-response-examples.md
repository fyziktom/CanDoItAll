# Tool response examples

## Example: start app

Request:
```json
{
  "mode": "WatchRun",
  "reuseIfCompatible": true
}
```

Response:
```json
{
  "ok": true,
  "tool": "candoitall_app_start",
  "timestampUtc": "2026-03-20T16:00:00Z",
  "correlationId": "corr_01K0START",
  "data": {
    "sessionId": "app_01K0START",
    "reused": false,
    "mode": "WatchRun",
    "state": "Starting",
    "sessionVersion": 1,
    "projectPath": "/repo/CanDoItAll/src/CanDoItAll.Web/CanDoItAll.Web.csproj",
    "observedUrls": [],
    "initialCursor": 57,
    "lastKnownPid": 11111
  }
}
```

## Example: wait for healthy

Request:
```json
{
  "sessionId": "app_01K0START",
  "condition": "Healthy",
  "timeoutMs": 120000
}
```

Response:
```json
{
  "ok": true,
  "tool": "candoitall_app_wait",
  "timestampUtc": "2026-03-20T16:00:07Z",
  "correlationId": "corr_01K0WAIT",
  "data": {
    "sessionId": "app_01K0START",
    "condition": "Healthy",
    "satisfied": true,
    "timedOut": false,
    "elapsedMs": 6842,
    "observedState": "Healthy",
    "finalCursor": 83,
    "matchedLogEntry": null
  }
}
```

## Example: build with StopAndResume

Request:
```json
{
  "whenAppRunning": "StopAndResume",
  "waitForCompletion": false
}
```

Response:
```json
{
  "ok": true,
  "tool": "candoitall_solution_build",
  "timestampUtc": "2026-03-20T16:01:00Z",
  "correlationId": "corr_01K0BUILD",
  "data": {
    "operationId": "op_01K0BUILD",
    "operationType": "Build",
    "state": "Running",
    "targetPath": "/repo/CanDoItAll/CanDoItAll.sln",
    "appPreemption": {
      "policy": "StopAndResume",
      "stoppedSessionId": "app_01K0START",
      "resumePlanned": true
    },
    "initialCursor": 0
  }
}
```

## Example: operation completed

Response:
```json
{
  "ok": true,
  "tool": "candoitall_operation_wait",
  "timestampUtc": "2026-03-20T16:01:14Z",
  "correlationId": "corr_01K0BUILD",
  "data": {
    "operationId": "op_01K0BUILD",
    "completed": true,
    "timedOut": false,
    "state": "Completed",
    "elapsedMs": 13992,
    "exitCode": 0,
    "summary": "Build succeeded.",
    "resumeOutcome": {
      "attempted": true,
      "success": true
    }
  }
}
```

## Example: diagnostics

Response:
```json
{
  "ok": true,
  "tool": "candoitall_diagnose_start_failure",
  "timestampUtc": "2026-03-20T16:02:00Z",
  "correlationId": "corr_01K0DIAG",
  "data": {
    "targetType": "AppSession",
    "targetId": "app_01K0FAIL",
    "category": "HealthTimeout",
    "confidence": "Medium",
    "summary": "The process started but the configured health endpoint did not become healthy before timeout.",
    "recommendedActions": [
      "Read the latest app logs.",
      "Verify the health endpoint path.",
      "Increase timeout only after checking startup performance."
    ],
    "evidence": [
      {
        "sequence": 231,
        "text": "Application started."
      },
      {
        "sequence": 247,
        "text": "Health probe timed out after 120000 ms."
      }
    ]
  }
}
```
