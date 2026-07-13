# SB14 Behavioral Proof

## Decision

- Status: `Pass`.
- Scope: current process-run managed artifact/output roots, fail-closed root ownership, Disabled host/session retention, focused run-files UI, independent read-only interaction, and live external mutation.
- Progression: SB15 is unlocked. A generic relative-path authority rule, stale launch-root reuse, host/session caching, dashboard-owned browser behavior, a Processes-to-Workbench edge, or a scoped/singleton lifetime regression reopens SB14.

## Behavior And Authority

`ProcessRunArtifactRootPolicy` is owned by Processes.Application. It accepts only the managed `artifacts/process-runs/{currentRunId}` and `output/.../process-runs/{currentRunId}` namespaces, collapses paths to run/product roots, and rejects absolute, traversal, wrong-run, and unrelated relative paths. Current-root aggregation is capped at 512 launch-variable sets, 512 variables per set, 64 unique roots, 128 path segments, and 4,096 characters per candidate.

`ProcessRunFileScopeProvider` loads the current runtime state and step assignments on every resolution. A scope key contains the run ID and SHA-256 root fingerprint; storage binding re-resolves current run data before catalog access. Removed or changed roots therefore fail with `Conflict` and cannot continue through an old semantic scope. Absolute external product targets are ignored. The binding declares `FileToolsHostBrowseCacheMode.Disabled`.

`ProcessRunFilesCoordinator` reconstructs the source set for every open/refresh, declares `FileBrowserStateRetentionMode.Disabled`, bounds search to 32 containers, 2,000 items, five seconds, concurrency one, 200 matches, and 2 MiB retained state, and transfers a selected occurrence to an independently owned read-only FileInteraction. Browser disposal does not revoke the interaction; interaction disposal releases the exact authorized handle.

## Architecture And Responsibility Result

| Owner | Responsibility | Result |
| --- | --- | --- |
| `ProcessRunArtifactRootPolicy.cs` | Pure managed current-run root policy | 240 lines; Processes.Application ownership |
| `ProcessRunFileScopeContracts.cs` | Neutral typed process-run scope-set contract | 56 lines; no project reference |
| `ProcessRunFileScopeProvider.cs` | Current runtime/assignment resolution and Disabled storage binding | 225 lines |
| `ProcessRunFilesCoordinator.cs` | Source aggregation, bounded browser construction, and authorized activation | 156 lines |
| `ProcessRunFileSessions.cs` | Browser and interaction lifetime ownership | 77 lines |
| `ProcessRunFilesDialog.razor` | Focused loading/error/refresh/browser/interaction UI | 299 lines |
| `LiveProcessesDashboard.razor` | Run ID plus open/close orchestration only | 32 added lines, one removed line; zero FileBrowser/integration behavior dependency |

The managed runtime composition gate found that the new scoped process binding source could not be consumed by the existing singleton composite binding graph. The repair makes `IFileToolsStorageBindingProvider`, `IStorageFileAccessAuthorizationCoordinator`, and `IFileToolsBrowseSessionFactory` scoped. A direct lifetime regression test and runtime `ValidateScopes` startup prove the graph; no service locator or scope creation was added to product services.

## Automated Proof

| Surface | Command scope | Result |
| --- | --- | --- |
| Unit | process root policy/provider/coordinator | `17/17 Pass` after the final performance repair |
| Unit lifetime/authority regression | FileTools boundary, authorization, policy/provider/coordinator | `43/43 Pass` |
| Components | process-run dialog refresh/error plus thin dashboard entry | `3/3 Pass` |
| Integration | managed-files storage/authorization host | `8/8 Pass` |
| Build | `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release --no-restore --property:TreatWarningsAsErrors=true` | `Pass`, 0 warnings, 0 errors |
| Format | focused SB14 owners and lifetime/test repair | `Pass` |
| Diff hygiene | `git diff --check` | `Pass`; repository line-ending conversion notices only |

The full pre-SB14 broad unit run remains `2584/2585 Pass`; its sole unrelated failure expects capability seed pack `v9` while product source is already `v10`. SB14 changes have rebuilt and passed all affected authority, composition, provider, component, and integration owners.

## Performance And Scale Review

The standard .NET performance scan covered all five new C# owners. It found zero sync-over-async, `async void`, `Task.Run`, substring, culture-default comparison, chained replacement, parameter-array, whole-file read, per-call `HttpClient`, or unsealed class signal. Four implementation classes are sealed; four record declarations are immutable/sealed by contract.

The scan initially found a double dictionary lookup in root deduplication and multi-enumeration LINQ in scope validation. Closure replaced them with `TryAdd` and a single bounded `HashSet` pass. The remaining seven LINQ sites and six list/dictionary allocations construct capped root/source/revision results around database/storage calls; none occurs inside provider item enumeration, and the coordinator pre-sizes source lists to the declared scope count.

## Managed Browser Proof

The initial managed project-run lane failed before runtime because its generated `.mcp-state/artifacts/app-projects/...` destination pushed existing template copy paths beyond the Windows 260-character limit. The successful proof used the already warning-clean Release DLL through the managed `PublishedDll` lane, avoiding any product or build-target workaround. Managed session `app_7486f3d8485c4e4593ae97bdd2c2bb91` reached `Healthy` at `http://127.0.0.1:5504`.

A controlled local process fixture was created through the current Processes HTTP API after a successful launch preflight. Run `ffa34b43-40b1-4920-a84b-0d9277ed489e` opened from `/processes/live?runId=...`; its details dialog exposed only the thin `Files` entry. The run-files dialog resolved one real managed artifact root and displayed `Always current`, the source revision, and the explicit Disabled cache/retention notice.

- Initial browse showed `before.txt`; double-click opened its authorized read-only content.
- While the browser was open, the fixture replaced `before.txt` and created `after.txt` under the current managed run root.
- `Refresh sources` rebuilt the workspace; the dialog showed both files and the updated modified time.
- Reopening `before.txt` returned `SB14 replaced content after refresh.`, proving current bytes rather than retained browser/session data.
- The component negative renders an explicit forbidden error and Retry; policy/provider tests reject traversal, absolute, wrong-run, unrelated namespace, stale fingerprint, missing run, over-count, and external absolute product target before catalog/provider access.
- At 1900x1200 and 1440x900, dialog chrome remained fixed, FileBrowser owned the result scroll area, the overlay had no lateral overflow, and the read-only interaction remained bounded.
- Browser console: 0 errors, 0 warnings. Blazor initializer and negotiate requests: 200. The only managed runtime warning after proof was the explained cancellation race from stopping the disposable process fixture; it is unrelated to FileTools browsing/authority.

The fixture used `execute=false`, was cancelled immediately after proof, read back `Cancelled` with projection backlog zero, and both temporary files were removed. No database profile or catalog selection was changed.

### Browser Artifacts

- `browser/sb14-run-details-files-entry-1900x1200.png`
- `browser/sb14-current-run-files-1900x1200.png`
- `browser/sb14-current-file-open-1900x1200.png`
- `browser/sb14-refresh-new-file-1440x900.png`
- `browser/sb14-replaced-file-open-1440x900.png`

## Dependency And C# Gate

Fresh focused CodeAnalytics snapshot attempts again failed because the installed server transport closed. Closure therefore uses the checked project-reference graph, source assertions, scoped lifetime regression, and successful full Release Web graph instead of inventing a snapshot identifier.

- Integration.Abstractions has no project reference.
- Integration references only Integration.Abstractions and Infrastructure.
- Processes.Application has no module or Workbench reference.
- Modules.Processes references Processes.Application and Integration.Abstractions, but not Workbench.
- Workbench already references Processes.Application and consumes the process-owned policy; dependency direction is one-way.
- The full Release Web graph builds successfully, so no project-reference cycle was introduced.

Source assertions find zero FileBrowser/FileInteraction/storage-binding/session behavior token in `LiveProcessesDashboard`, zero service-locator use in new owners, zero Processes-to-Workbench edge, and explicit Disabled host/session modes. No partial class, nested policy, broad facade, or generalized process-history redesign was added.

## Closure

All five SB14 acceptance checks pass. Processes owns current-run root meaning, stale or escaped authority fails closed, live mutation is visible on refresh, the dashboard remains thin, the lifetime graph is scope-correct, and desktop behavioral proof unlocks SB15.
