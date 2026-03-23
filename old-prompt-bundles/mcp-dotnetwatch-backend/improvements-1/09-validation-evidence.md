# Validation Evidence

## 1. Build and test results
Completed:
- `dotnet build src\CanDoItAll.Mcp.DotNetWatch\CanDoItAll.Mcp.DotNetWatch.csproj -c Debug --artifacts-path C:\repositories\CanDoItAll\.artifacts\mcp-dotnetwatch-improvements1-final2 -p:UseAppHost=false -p:UseSharedCompilation=false`
- `dotnet test tests\CanDoItAll.Mcp.DotNetWatch.Tests\CanDoItAll.Mcp.DotNetWatch.Tests.csproj -c Debug --artifacts-path C:\repositories\CanDoItAll\.artifacts\mcp-dotnetwatch-improvements1-tests-final2 -p:UseSharedCompilation=false`
- Result: `22` tests passed, `0` failed

Observed constraint:
- Default-output `dotnet build` / `dotnet test` of `CanDoItAll.Mcp.DotNetWatch` is blocked while live backend daemons hold `src\CanDoItAll.Mcp.DotNetWatch\bin\Debug\net10.0\CanDoItAll.Mcp.DotNetWatch.dll`.
- This is why the improvement package now documents shadow-artifact builds for MCP-server self-work.

## 2. Manager aggregation evidence
Live aggregate backend page:
- Screenshot: `backend-manager-aggregate.png`
- Current backend: `backend_933ceded91904fbda6fb4599181ac80e` at `http://127.0.0.1:53336`
- Discovered remote backend: `backend_46b623a3c321422aa2d98324ed8cc650` at `http://127.0.0.1:53335`
- Aggregate counts shown in the live page:
  - `Live Backends = 2`
  - `Sessions = 2`
  - `Operations = 0`

Manager UI proof points:
- Both workspace roots are rendered:
  - `C:\repositories\CanDoItAll`
  - `C:\repositories\pveinvoicing\PVEInvoicing`
- Both backend cards expose:
  - `Start Default App`
  - `Build Workspace`
  - per-session `Rebuild`
  - per-session `Force Rebuild`
  - per-session `Stop`
  - per-session `Force Stop`

## 3. Manager action evidence
Executed through the aggregate manager endpoint on the CanDoItAll backend:
- Target backend: `backend_46b623a3c321422aa2d98324ed8cc650`
- Action: `RebuildSession`
- Original PVE session: `app_7e3ac6da235c48e69707fb0123cc9a1b`
- Result: new rebuilt session `app_3e170ffe6d194a53becaccd67046ed40`
- New watcher PID after rebuild: `19856`
- Post-action wait result: `WatchSettled = true`, `state = Running`

Follow-up improvement applied after observing this:
- The manager proxy path now rewrites remote responses to return `proxied = true` instead of leaking the remote backend's local `proxied = false` value.
- This is a response-metadata correction only; the remote rebuild execution already worked live before the patch.

## 4. Watch confirmation handling
Implementation safeguards confirmed in `AppRuntimeModels.cs`:
- `dotnet watch --non-interactive`
- `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1`

Meaning:
- rude edits do not stall on interactive confirmation
- the backend can keep long-lived watch sessions unattended for agents

## 5. `CanDoItAll` persistence evidence
Persistent backend:
- Backend id before and after stdio proxy re-instance: `backend_933ceded91904fbda6fb4599181ac80e`
- Backend PID before and after re-instance: `21812`

Persistent app session:
- Session id remained usable across re-instance: `app_1d8936e22ccc4593a9f367d75bc27cc8`
- Runtime state after re-instance: `Healthy`
- Watch state after re-instance: `WaitingForChanges`
- Runtime PID observed: `8308`

Visual validation:
- Temporary CSS probe applied to the dashboard `New project` button.
- Computed style changed from `boxShadow = none` to `rgba(15, 23, 42, 0.18) 0px 12px 24px 0px`.
- Probe was removed.
- Final computed style returned to `boxShadow = none`.

## 6. `pveinvoicing` persistence evidence
Persistent backend:
- Backend id before and after stdio proxy re-instance: `backend_46b623a3c321422aa2d98324ed8cc650`
- Backend PID before and after re-instance: `24572`

Initial persistent app session:
- Session id remained usable across re-instance: `app_7e3ac6da235c48e69707fb0123cc9a1b`
- Watch state after re-instance: `WaitingForChanges`

Generic startup isolation:
- The app session runs from per-session shadow artifacts under:
  - `C:\repositories\pveinvoicing\PVEInvoicing\.mcp-state\artifacts\app-sessions\<sessionId>`
- This avoided the previous lock conflict on the app's normal `bin\Debug\net10.0` output.

Visual validation and revert:
- Baseline login container margin:
  - `pageMarginTop = 0px`
  - `pageRectTop = 52.275...`
- Applied temporary change:
  - `.account-page { margin: 0.75rem auto 0; }`
- Watch settled without stopping the backend.
- Browser-side note:
  - the running app served the updated `app.css`, but the page held a stale stylesheet response until the stylesheet href was cache-busted
  - after cache busting, the live page showed:
    - `pageMarginTop = 12px`
    - `pageRectTop = 64.275...`
- Reverted the margin change.
- After watch settled and the stylesheet was cache-busted again, the page returned to:
  - `pageMarginTop = 0px`
  - `pageRectTop = 52.275...`

Artifacts:
- `pve-login-before-margin-probe.png`
- `pve-login-after-margin-probe.png`
- `pve-login-after-margin-revert.png`

## 7. Log reduction measurement
Measured sample:
- Source: live `pveinvoicing` watch session `app_3e170ffe6d194a53becaccd67046ed40`
- Request: `app-logs`, `cursor = 0`, `limit = 500`, `includeStdOut = true`, `includeStdErr = true`, `includeSystemEvents = true`

Measured payload sizes:
- Raw:
  - returned entries: `500`
  - payload chars: `140,560`
  - estimated payload tokens: `35,140`
  - text chars only: `40,671`
  - estimated text tokens only: `10,168`
- AgentOptimized:
  - returned entries: `36`
  - consumed raw entries: `1,357`
  - suppressed entries: `1,321`
  - payload chars: `10,496`
  - estimated payload tokens: `2,624`
  - text chars only: `2,600`
  - estimated text tokens only: `650`

Measured savings:
- Payload-token savings per read: `32,516`
- Payload reduction ratio: `92.53%`
- Text-token savings per read: `9,518`
- Text reduction ratio: `93.61%`

Suppressed noise categories in the measured sample:
- `60` compiler / NuGet warning lines
- `32` framework HTTP trace lines
- `1,224` Entity Framework information / command trace lines
- `2` restore/build progress lines
- `3` blank lines

## 8. Result analysis
Context-window impact, using the measured payload-token estimate:
- Against a `128k` input window:
  - raw sample consumes about `27.45%`
  - reduced sample consumes about `2.05%`
  - raw samples that fit: `3`
  - reduced samples that fit: `48`
- Against a `200k` input window:
  - raw sample consumes about `17.57%`
  - reduced sample consumes about `1.31%`
  - raw samples that fit: `5`
  - reduced samples that fit: `76`

Estimated multi-build savings:
- `10` comparable log reads:
  - raw: about `351,400` input tokens
  - reduced: about `26,240` input tokens
  - saved: about `325,160` input tokens
- `20` comparable log reads:
  - raw: about `702,800` input tokens
  - reduced: about `52,480` input tokens
  - saved: about `650,320` input tokens

Estimated credit impact:
- Exact Codex credit-to-token conversion depends on the active model and plan and is not exposed here.
- For any token-priced model, the measured savings scale linearly with input tokens, so this reduction is also about `92.53%` less input-token spend for comparable noisy log reads.

Estimated time impact:
- Input processing cost for large raw logs is roughly linear enough that a `92%+` reduction materially lowers context packing pressure.
- In practice this means:
  - fewer forced context compressions
  - less agent attention wasted on warning floods and framework trace spam
  - faster turns whenever the agent needs repeated watch/build log reads during an implementation cycle
