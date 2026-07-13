# SB13 Behavioral Proof

## Decision

- Status: `Pass`.
- Scope: authorized Project Structure project/node collection browsing, direct image/PDF interaction, hostile metadata rejection, focused floating-window behavior, and process-policy ownership progression smoke.
- Progression: SB14 is unlocked. Arbitrary path authority, a browser call on direct known-file open, duplicated action dispatch, a new page partial, page-owned browser lifetime, or process semantics returning to Workbench reopens SB13.

## Behavior And Authority

`ProjectStructureFileActionCoordinator` is the single collection action owner. It creates typed project or node requests, resolves neutral semantic scopes, constructs bounded FileBrowser sessions, maps sources back to scopes, and transfers an authorized selected occurrence into an independently owned read-only FileInteraction. Project aggregate browsing reuses `IProjectFileScopeProvider`; node collection and known-file meaning are resolved by `IProjectStructureNodeFileScopeProvider`.

Direct image/PDF opening does not call the collection coordinator. `ProjectStructureKnownFileInteractionCoordinator` resolves one typed known-file occurrence and constructs one authorized FileInteraction session. Its constructor and source contain no FileBrowser session factory, `FileBrowserSession`, browse, or search dependency. Component spies prove one FileInteraction and zero FileBrowser instances for both direct media paths.

Node metadata is loaded again from the current database before authorization. Absolute paths, URI-like values, rooted paths, traversal segments, missing nodes/bindings, unsupported object kinds, ambiguous storage references, and stale semantic scope keys fail before a storage provider is invoked.

## Architecture And Responsibility Result

| Owner | Responsibility | Result |
| --- | --- | --- |
| `ProjectStructureFileRequests.cs` | Typed collection request and node-scope key contracts | 75 lines |
| `ProjectStructureFileScopeResolver.cs` | Current node/binding resolution and fail-closed scope mapping | 295 lines after contract extraction |
| `ProjectStructureFileActionCoordinator.cs` | Collection session, source routing, activation, and workspace lifetime | 282 lines |
| `ProjectStructureKnownFileInteractionCoordinator.cs` | One known-file direct interaction | 89 lines; zero browser dependency |
| `ProjectStructureFileBrowserWindow.razor` | Focused floating-window state and latest workspace ownership | 339 lines |
| `ProjectStructureAttachmentPreviewDialog.razor` | Existing direct attachment dialog lifecycle | 116 lines |

The architecture review initially found the resolver file at 367 lines because it also held typed request/key contracts. Those contracts were extracted before closure, reducing the resolver to 295 lines without adding an interface or changing behavior. No new `ProjectStructurePage.*.cs` file, nested policy, service locator, or action hierarchy was added. Existing `ProjectStructurePage.ToolWindows.cs` contains only typed open/close callbacks and interaction/window replacement; it does not browse, search, authorize, or retain provider state.

The SB13 progression gate also moved the pre-existing process-run root interpretation out of Workbench. `ProcessRunArtifactRootPolicy` now lives in Processes.Application, and `ProjectStructureProcessProjectionContributor` consumes its typed resolution. A direct policy/source smoke proves Workbench no longer defines process root semantics.

## Automated Proof

| Surface | Command scope | Result |
| --- | --- | --- |
| Unit | SB13 resolver/coordinator/direct interaction, authority, composition, FileTools boundary, and process ownership filters | `54/54 Pass` |
| Components | attachment dialog, floating browser window, action adapter, and page move filters | `28/28 Pass` |
| Integration | real authorized-files endpoint with current project binding and current handle | `1/1 Pass` |
| Build | `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release --no-restore -warnaserror` | `Pass`, 0 warnings, 0 errors |
| Format | focused Workbench, Projects, Integration, Integration.Abstractions, Processes.Application, and unit-test owners | `Pass` |
| Broad unit regression | all tests except the repository transient-artifact policy test | `2584/2585 Pass` in 5m54s; the sole unrelated failure expects capability seed pack `v9` while product source is already `v10` |

The broad Workbench format command still reports the pre-existing whitespace defect in `ProjectStructureAgentRuntimeToolProvider.cs` lines 1658-1662. The focused changed-owner format commands pass; no unrelated 1,600-line owner was reformatted.

## Performance And Scale Review

The collection coordinator caps semantic sources at 64, browse pages at 50, inspected items at 2,000, metadata probes at 50 with concurrency one, and progressive search at 32 containers, 2,000 items, five seconds, concurrency one, 200 matches, and 2 MiB retained state. Session retention is Disabled. Source and revision lists are capacity-sized and bounded by the same source cap; there is no unbounded collection inside provider item loops.

The focused static scan found zero `async void`, sync-over-async, `Task.Run`, per-call `HttpClient`, whole-file read, or blocking wait signals in the changed scope/coordinator/activation/process-policy owners. The floating component replaces and disposes workspaces explicitly; known-file cleanup uses non-cancelled release after authority has been granted.

## Managed Browser Proof

Primary production proof used managed Release runtime revision `ProcessInstance 2026-07-13T10:36:32.6611091+00:00:46332` at `http://127.0.0.1:5503`.

- The real Quotation project toolbar opened one project source. Searching `pilot-readme` returned one visible item after seven inspected items. Include-subprojects changed the revision from `9875d24bf691` to `3043047885cd`.
- Minimize/restore preserved the workspace and revision. Move changed the window from approximately `x=1444,y=136` to `x=1144,y=276`; resize changed `440x560` to `560x660`. At 1440x900 the window remained bounded at `x=864,y=224` with 16 px right/bottom clearance.
- The action popover rendered outside the floating window without clipping. FileBrowser remained the single results scroll owner, chrome stayed fixed, and the document had no lateral overflow at 1900x1200.
- Double-clicking `pilot-readme.md` opened read-only FileInteraction through authorized content.
- Double-clicking real PDF and image nodes opened the existing attachment dialog with exactly one FileInteraction and zero FileBrowser nodes. The image used authorized blob content; Chromium displayed its normal PDF object fallback while the direct FileInteraction path remained present.
- A real storage-backed infrastructure node opened one node source with no project Include-subprojects control. Changing its prefix to `../../secret` produced the explicit `Files unavailable` state and `The node file metadata escapes its storage scope`; Retry remained fail-closed.
- The temporary proof storage node was deleted through the UI and its database count was verified as zero. The temporary development database profile was removed and the original active profile `d871e46b-bab6-48ee-ba23-08da223bf8f0` was restored before the managed session stopped.
- Final navigation recorded 0 console errors and 0 console warnings. Initializer and negotiate requests returned 200; managed logs contained the bounded search/open revisions and no unhandled/fail entry.

Visual inspection found that the interaction screenshot still displayed the collection badge `Resolving` after content opened. The final component now reports `File open` and `Authorized read-only file`; a real FileBrowser-row double-click component regression proves the transition and absence of `Resolving`. This label-only repair, final logging, contract-file extraction, and process-policy ownership move were followed by a zero-warning Release build and rebuilt focused unit/component suites. The accepted screenshot records the pre-repair label while its interaction structure, authority path, geometry, and content remain representative.

### Browser Artifacts

- `browser/sb13-project-files-1900x1200.png`
- `browser/sb13-project-file-interaction-1900x1200.png`
- `browser/sb13-direct-pdf-dialog-1900x1200.png`
- `browser/sb13-direct-image-dialog-1900x1200.png`
- `browser/sb13-project-files-1440x900.png`
- `browser/sb13-node-file-scope-1440x900.png`
- `browser/sb13-hostile-node-error-1440x900.png`

## Dependency And C# Gate

Two fresh focused CodeAnalytics snapshot attempts failed because the installed server transport was closed. Closure therefore used the checked project-reference graph plus the successful full Web build as the dependency/cycle proof rather than inventing a snapshot identifier.

- Integration.Abstractions has no project reference.
- Integration references only Integration.Abstractions and Infrastructure.
- Projects references Integration.Abstractions, Infrastructure, and SharedKernel; it has no Workbench edge.
- Processes.Application references process-core projects, the process driver abstraction, and Git; it has no Workbench/module edge.
- Workbench already references Processes.Application and consumes `ProcessRunArtifactRootPolicy`; Processes never references Workbench.
- The complete Release Web graph builds successfully, so no project-reference cycle was introduced.

Source assertions find no `browse-files` branch in any `ProjectStructurePage.*.cs`, no new untracked page partial, no browser dependency in the direct known-file coordinator, no remaining Workbench process root policy, and no scoped performance anti-pattern match.

## Closure

All six SB13 acceptance checks pass. Direct known-file interaction and collection discovery remain separate, hostile metadata fails before provider I/O, the floating desktop contract is proven, and the process-owned root-policy consumer smoke unlocks SB14.
