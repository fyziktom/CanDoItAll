# Task 09: Harden .NET runtime tool lifecycle

## Goal

Prevent future false escalations caused by stale running apps, locked files, or port/process ownership conflicts.

## Scope

This belongs in the .NET/workspace tool layer, not generic process runtime.

## Requirements

- `workspace_dotnet_run` records owner metadata: processRunId, stepInstanceId, agentExecutionRunId if available.
- Startup receipt records project path, working directory, product root, URL, process ids, lifetime scope.
- `workspace_dotnet_stop` is idempotent.
- If a process is already stopped, stop returns successful cleanup receipt with warning.
- If another active owner uses the same product root/port, new run returns actionable diagnostic or performs explicit safe orphan cleanup only when configured.
- Browser proof records startup receipt path.

## Tests

Use existing fake process host tests or add focused tests:

- run then stop success,
- stop already stopped,
- second run detects active previous owner,
- orphan cleanup produces receipt,
- failed cleanup returns failed receipt and `Blocked` guidance.
