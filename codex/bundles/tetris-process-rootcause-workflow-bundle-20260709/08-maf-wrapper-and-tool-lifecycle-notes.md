# MAF wrapper and .NET tool lifecycle notes

## MAF wrapper finding

The Tetris incident does not show that MAF failed to capture receipts. Attempt 3 contains full runtime/browser receipt evidence. The issue is how the process adapter interprets these receipts against branch outcomes.

The MAF wrapper still needs one architectural tightening:

- MAF result text and evidence refs are not the source of truth for current-run tool execution.
- Tool receipt records with `ExecutionRunId` are the source of truth.
- Adapter diagnostics should always report whether a tool was absent, present but failed, present but wrong execution run, or present but not applicable for the selected branch.

## Runtime/browser process lifecycle

No direct evidence in this incident proves that a previous step held the Blazor app open and caused the escalation. Attempt 3 had both `workspace_dotnet_run` and `workspace_dotnet_stop`.

Still, the risk is real for larger processes. The correct boundary is the .NET workspace tool layer, not generic process runtime.

Recommended lifecycle rules:

1. Every `workspace_dotnet_run` must record:
   - process owner: processRunId, stepInstanceId, agentExecutionRunId,
   - product root,
   - project file,
   - listen/probe URL,
   - process ids/tree ids,
   - startup receipt path,
   - intended lifetime scope.

2. `workspace_dotnet_stop` must be idempotent:
   - already stopped is success with cleanup evidence,
   - missing child process is success with warning,
   - still-running process after stop is failed receipt with process ids.

3. A later `workspace_dotnet_run` on the same product root/port should:
   - detect active previous owner,
   - either fail with actionable diagnostic pointing to stop receipt, or
   - use explicit orphan cleanup mode if safe.

4. Browser proof should reference the startup receipt path, not just a URL string.

5. Add tests with fake process host:
   - run then stop success,
   - stop already-stopped success,
   - second run blocked by active owner,
   - orphan cleanup produces receipt,
   - failed cleanup causes `Blocked`, not repair branch.
