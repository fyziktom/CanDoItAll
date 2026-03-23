# Validation Evidence

## Automated checks run

1. `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore`
   - passed
2. `dotnet test tests\CanDoItAll.Mcp.DotNetWatch.Tests\CanDoItAll.Mcp.DotNetWatch.Tests.csproj --no-restore`
   - passed after the `AppSession` constructor update
3. `dotnet build src\CanDoItAll.Mcp.DotNetWatch\CanDoItAll.Mcp.DotNetWatch.csproj -c Debug --artifacts-path .artifacts\mcp-server-shadow-final -p:UseAppHost=false -p:UseSharedCompilation=false`
   - passed

## Live validation executed

### CanDoItAll workspace

1. Started the shadow-built MCP server, which launched a detached backend daemon.
2. Confirmed registration at:
   - `C:\repositories\CanDoItAll\.mcp-state\backend\registration.json`
3. Started `CanDoItAll.Web` and waited for `Healthy`.
4. Opened the project structure page in a real browser and confirmed:
   - the lower support lane no longer stretches the right card to the full outline height
   - the outline lane is internally scrollable
5. Applied an additional CSS-only refinement to the structure support cards while the app was already running.
6. Started a fresh stdio MCP process again and confirmed:
   - the backend ID stayed the same
   - the backend PID stayed the same
   - the same app session remained available
   - the same runtime PID stayed active
7. Opened the manager UI and confirmed it exposed backend identity plus the live session.

### PVEInvoicing workspace

1. Added a workspace-local settings file:
   - `C:\repositories\pveinvoicing\PVEInvoicing\PVEInvoicing.Mcp.DotNetWatch.settings.json`
2. Started the same shadow-built server binary against that workspace and confirmed a separate backend registration:
   - `C:\repositories\pveinvoicing\PVEInvoicing\.mcp-state\backend\registration.json`
3. Confirmed the final build fixed the health-disabled case:
   - `app_wait` with `Ready` succeeded
   - the session returned `pendingChange=false`
   - static-asset hot reload ended in `lastHotReloadOutcome=Succeeded`
4. Applied a reversible margin change in:
   - `C:\repositories\pveinvoicing\PVEInvoicing\PVEInvoicing\wwwroot\app.css`
5. Confirmed the browser-observed margin changed from `0px` to `16px`.
6. Started a fresh stdio MCP process again and confirmed:
   - the backend ID stayed the same
   - the backend PID stayed the same
   - the same app session remained active
7. Reverted the margin change and confirmed the browser-observed margin returned to `0px`.

## Implementation gap discovered during validation

The original backend refactor was still too CanDoItAll-centric for health-disabled apps. During `pveinvoicing` validation, static-asset hot reload succeeded but the session remained stuck in `pendingChange=true`. The implementation was updated so non-health-probed workspaces can settle watch state from:

1. observed listening URLs
2. `Hot reload of static assets succeeded.`
3. `No C# changes to apply.`

## Remaining follow-up

The full integration suite was partially stabilized and one targeted persistence test had already passed earlier, but the full end-to-end integration matrix was still noisy during this work because of transport timing and process-lock contention. The live browser validations above were used as the decisive acceptance check.
