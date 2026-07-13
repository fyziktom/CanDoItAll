# SB10 Project Files Pilot Behavioral Proof

Date: 2026-07-13.

## Decision

- SB10 result: `Pass`.
- The product proof uses a real saved project, the production project binding source, the production filesystem Storage driver, the packaged FileBrowser/FileInteraction components, current-context opaque authorization, and an independent known-file interaction session.
- Scope remains deliberately narrow: one project source and read-only Markdown/text activation. Editing and broader module stories remain blocked by SB11.

## Implemented Boundary

- `ProjectFileToolsStorageBindingSource` owns Project semantic-scope resolution. It requires a current project row, resolves the bootstrap filesystem storage, confines the source to `managed-files/project-media/files/{projectId:N}`, declares native work limits, and explicitly disables host/session listing retention.
- `ProjectFilesPilotCoordinator` constructs a bounded FileBrowser session and activates a selected item only through `IFileToolsBrowseItemActivator`, which recreates the current scope session and reauthorizes the exact occurrence.
- `ProjectFilesPilotWorkspace` owns the browser lifetime. `ProjectFilesPilotInteraction` owns the independent known-file session and revokes/releases it on disposal.
- `ProjectFilesPilotDialog` renders and orchestrates state only. It uses latest-operation cancellation, disposes browsing before publishing an interaction, keeps an activation local until transfer succeeds, and requires explicit retry after an open failure.
- `ProjectsPage` owns only selected-project dialog state. `ProjectsBoard` raises the typed files callback; neither owner contains storage, authorization, or browser policy.

## Shared Components Decision

- Components discovery selected BaseLib `Dialog`, `Alert`, `LoadingState`, and `EmptyState`; the existing project already consumes BaseLib and does not consume Radzen.
- The product surface uses a full controlled BaseLib `Dialog`, packaged `FileBrowser`, and packaged `FileInteraction`. Existing FileBrowser controls own search, scope, type, sort, refresh, navigation, result list/card view, loading, empty, and error behavior.
- No custom card grid, raw overlay implementation, hand-loaded package asset, Tailwind dependency, or Radzen component was added.

## Work, State, And Render Bounds

- Project native browse limits: 50 returned items, 2,000 inspected items, 50 metadata probes, one concurrent metadata probe, and five seconds.
- Progressive search budget: 32 containers, 2,000 inspected items, five seconds, one concurrent browse, 200 retained matches, and 2 MiB retained snapshot bytes.
- Browser session retention and host browse cache are disabled for the pilot. The host cache mode participates in source-set cache identity.
- The 120-file component fixture renders the first 50 rows. Its progressive exact search inspects 120 items, returns one, retains one, and reports the measured counters; it does not render or retain the complete source.
- FileTools search re-entry tests cover duration cancellation, item/container limits, match-count/byte retention, continuation accounting, and peak concurrency. All 440 FileTools tests pass.
- Superseded dialog operations cancel through a replaced `CancellationTokenSource`; the final state belongs only to the latest operation.

## Authority And Lifetime Proof

- Binding composition fails closed when zero or multiple module owners claim a scope kind.
- Grant and resolve both re-resolve the current semantic binding. A removed project/source, changed root, storage mismatch, or occurrence outside the root cannot mint or reuse authority.
- Item activation does not trust browser state. It recreates a current session, finds and authorizes the exact item, and grants only the requested read-only operation.
- The interaction test replaces the file after browsing, activates it, disposes the browser, reads the replacement bytes through FileInteraction, then verifies the handle is revoked after interaction disposal.
- The real HTTP-host positive now creates a real project, saves the file inside that project's declared root, grants through the production binding source, and serves the current runtime handle. The earlier synthetic Project ID/out-of-root fixture was rejected by the new fail-closed boundary and was corrected rather than weakening authorization.

## Behavioral Commands And Results

```text
dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj -c Release --no-restore --filter "FullyQualifiedName~FileAccessAuthorizationTests|FullyQualifiedName~FileToolsIntegrationBoundaryTests"
Pass: 23/23.

dotnet test tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~Project_files_pilot"
Pass: 4/4.

dotnet restore tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj
Pass; refreshed the local-package assets graph.

dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -c Release --no-restore --filter "FullyQualifiedName~ManagedFilesStorageIntegrationTests"
Pass: 8/8 after the realistic project-bound fixture repair.

dotnet test C:\repositories\CanDoItAll.FileTools\tests\CanDoItAll.FileTools.FileBrowser.Components.Tests\CanDoItAll.FileTools.FileBrowser.Components.Tests.csproj -c Release --no-restore
Pass: 45/45.

dotnet build src\App\CanDoItAll.Web\CanDoItAll.Web.csproj -c Release --no-restore -warnaserror
Pass: 0 warnings, 0 errors.
```

- Focused project-mode `dotnet format --verify-no-changes` passes for Integration.Abstractions, Integration, Projects, the two affected main test files, `ProjectsPageTests`, and the repaired host test.
- User-local SDK `C:\Users\lucys\.dotnet\dotnet.exe` project-mode format passes for FileBrowser.Components and its component tests; folder-workspace whitespace verification also passes.
- Web `staticwebassets.build.json` contains exactly two FileBrowser component assets and four FileInteraction component assets. FileInteraction.Markdown contributes no host asset because it is not referenced by the product.

## Failing-First Repairs

- Progressive search initially lacked duration, concurrency, match-count, and retained-byte limits. SB01 re-entry added the typed budget and deterministic metrics before product consumption.
- Compact FileBrowser with one source initially stretched the source-navigation row through the available dialog height. The packaged component now emits `has-source-navigation` and defines explicit `auto minmax(0, 1fr)` rows for Compact/Minimal navigation layouts; the contract test prevents recurrence.
- FileTools Integration initially reduced the host-wide HybridCache maximum key length to 128, faulting an unrelated AgentFramework key of length 219. The failing composition test now proves host limits are preserved; FileTools validates its own 128-character keys and applies only cooperative minimum host capability.
- The host authorization test initially used an invented Project scope and an out-of-root file. Current binding revalidation correctly rejected it. The fixture now proves the real project/source path.

## Managed Runtime And Browser Proof

- Managed watch session: `app_3d6881bea218454c97879620998347ad`, logical app `candoitall-filetools-sb10`.
- Confirmed revision: `candoitall-filetools-sb10:1:g0`; final state before cleanup was Running/Healthy, runtime ready, watch WaitingForChanges, and no HybridCache key-length fault after AgentFramework warmup.
- Final fresh runtime navigation: `http://127.0.0.1:5502/projects`, one persistent Playwright tab, 1440x900. The real `QuotationPDFs Tests` project opened with seven items; exact `pilot-readme` filtering reported 1 visible, 7 inspected, 1 retained.
- Final console interval: 0 errors. Network: `GET /_blazor/initializers` 200 and `POST /_blazor/negotiate` 200. The managed session was stopped after proof.
- Earlier accepted actions on the same production route:
  - refreshed and browsed the real project source;
  - opened `notes`, observed `nested-check.txt`, and navigated Back;
  - exact and missing-term searches;
  - keyboard Enter activation of Markdown and pointer double-click activation of text;
  - browser replacement by read-only FileInteraction and close/reopen;
  - deletion after refresh followed by explicit visible activation error while browsing remained usable;
  - action popover with `No actions available` wholly inside the 1440px viewport.

## Screenshot Review

| Artifact | Review result |
| --- | --- |
| `browser/project-files-pilot-1900x1200.png` | Pass. Exact search is legible; source/control/result hierarchy is clear; no giant empty navigation pane remains. |
| `browser/project-files-pilot-interaction-1900x1200.png` | Pass. FileInteraction replaces browsing, is visibly read-only, and shows the authorized replacement content. |
| `browser/project-files-pilot-1440x900.png` | Pass. Seven rows, toolbar, breadcrumb, count, and close action fit without lateral clipping. |
| `browser/project-files-pilot-overlay-1440x900.png` | Pass. Popover rectangle is x=1280, width=160 in a 1440px viewport; it remains in the top layer and is not clipped. |
| `browser/project-files-pilot-no-result-1440x900.png` | Pass. Missing search has explicit empty state and 0 visible / 7 inspected / 0 retained metrics. |
| `browser/project-files-pilot-stale-error-1440x900.png` | Pass. Deleted-after-refresh activation yields a visible generic error, retains browsing, and does not expose a path or identifier. |

Visual questions: the primary task is immediately identifiable; search and file results dominate; the dialog has one content scroll owner; controls and results remain aligned at both declared desktop sizes; no decorative surface competes with the workspace; error and empty states are explicit; overlay and close controls remain reachable.

## Architecture And Source Gate

- Fresh focused snapshot: `snap-20260713072501-9c272781`; five projects, 149 loaded documents, 280 scoped types, 1,596 members, 59 DI registrations, zero scoped cycles, and no `ProjectFilesPilot` finding.
- The broader diagnostic snapshot `snap-20260713072254-a584d4e1` confirms the already-declared Infrastructure ControlPlane/Persistence module cycle outside the FileTools scope. No project cycle or new FileTools cycle exists.
- No new partial class, nested service boundary, service locator, `BuildServiceProvider`, sync-over-async, `Task.Run` I/O wrapper, TODO/FIXME/stub, raw authorization log value, or silent provider fallback exists in the SB10 slice.
- Dependency direction remains Projects -> Integration.Abstractions/Infrastructure plus FileTools UI packages; Integration -> Integration.Abstractions/Infrastructure; Infrastructure has no FileTools dependency.

## Shallow-Pass Closure

- A 50-row screen is not accepted as bounded-source proof: the 120-item structural test and FileTools budget tests assert inspected, returned, retained, byte, duration, and concurrency facts.
- A rendered FileInteraction is not accepted as independent handoff proof: the interaction test disposes the browser before reading and proves revocation only when the interaction lifetime ends.
- A stale rendered row is not accepted as authority: current semantic binding and current occurrence are re-resolved during activation/grant/resolve, and the real deleted-file flow fails visibly.

## Progression

SB10 is complete. SB11 may enter to review/refactor this seam, rerun the accepted evidence, and issue the broader-UI progression decision. Any weakening of current binding revalidation, bounded search/state, browser-independent interaction, explicit retry, component layout, or host HybridCache cooperation reopens SB10 or its owning foundation phase.
