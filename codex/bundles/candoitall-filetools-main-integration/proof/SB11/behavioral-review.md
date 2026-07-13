# SB11 Behavioral Architecture And UX Review

## Decision

- Proof tier: `Behavioral`.
- Checkpoint C result: `Pass`.
- Progression: `SB12 unlocked`.
- Reopen condition: any later story that makes collection browsing authoritative, couples known-file interaction to a browser session, leaks a granted handle on cancellation, or requires editing the project pilot coordinator to add an unrelated scope/source.

## Responsibility Review

| Owner | Responsibility | Review result |
| --- | --- | --- |
| `ProjectFileToolsStorageBindingSource` | Resolve and validate the current Project-to-Storage binding | Focused top-level owner, 83 lines |
| `ProjectFilesPilot` | Coordinate one project browse session and one authorized interaction handoff | Focused interface/coordinator, 112 lines |
| `ProjectFilesPilotSessions` | Own browser and interaction lifetimes and revocation | Focused top-level lifetime owners, 80 lines |
| `CompositeFileToolsStorageBindingProvider` | Select exactly one typed scope binding source | Single-pass, fail-closed extension seam, 42 lines |
| `StorageFileToolsBrowseItemActivator` | Resolve exactly one current source occurrence and grant authority | Single-pass, current-occurrence boundary, 67 lines |
| `StorageFileBrowserItemAuthorizer` | Reauthorize browser items against current source state | Existing focused authorization owner; hot-path allocations removed |
| `ProjectFilesPilotDialog` | Render and orchestrate pilot browse/interaction state | FileTools component composition and explicit states; no authority policy |
| `ProjectsPage` / `ProjectsBoard` | Hold typed open/close state and forward the project identifier | Existing parents gained 25/11 lines only; no browse, content, or policy logic |

The initial 266-line pilot cluster mixed binding, coordination, and lifetime ownership. It was physically split before expansion. No partial class, nested service, service locator, facade-only abstraction, or new project reference was added.

## Concrete Repairs

- Release after a granted-handle failure or cancellation now uses `CancellationToken.None`; caller cancellation can no longer suppress security cleanup and leak a live handle.
- Composite source selection, exact occurrence activation, and browser-item authorization now use allocation-free single-pass selection instead of LINQ arrays, `FirstOrDefault`, or substring allocation.
- Zero and multiple scope owners still fail closed; no fallback source or guessed binding exists.
- The independent known-file handoff remains browser-independent: the browser lifetime is disposed before content is read, and only interaction disposal revokes the handle.
- The FileTools Compact grid repair remains in the shared component package. SB11 introduced no raw host wrapper or CSS override.

## Extension Smoke

`SB11_Composition_IndependentScopeSourceExtendsWithoutCoordinatorChange` registers Project and ProcessRun binding sources through the same composite provider, resolves each typed scope independently, and proves that adding the second source requires no edit to the pilot coordinator. Result: `1/1 Pass`.

## Lifecycle Regression

`Project_files_pilot_cancellation_releases_granted_handle_without_cancelled_cleanup` cancels after the grant, makes interaction construction fail with cancellation, and asserts that the exact granted file is released with a non-cancelled cleanup token. Result: `1/1 Pass`.

## Verification

| Surface | Command scope | Result |
| --- | --- | --- |
| Unit | `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter "FullyQualifiedName~FileAccessAuthorizationTests|FullyQualifiedName~FileToolsIntegrationBoundaryTests"` | `24/24 Pass` |
| Components | `dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj -c Release --filter "FullyQualifiedName~ProjectsPageTests"` | `5/5 Pass` |
| Host | `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter "FullyQualifiedName~ManagedFilesStorageIntegrationTests"` | `8/8 Pass` |
| Web | `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -warnaserror` | `Pass`, 0 warnings, 0 errors |
| Format | project-mode `dotnet format --verify-no-changes` for Integration, Projects, focused Unit, and focused Components sources | `Pass` |

## Performance Scan

The `analyzing-dotnet-performance` static scan covered the seven changed product sources in the pilot coordination, binding, lifetime, activation, authorization, and Razor surface.

| Pattern | Count |
| --- | ---: |
| Comparison-ambiguous `IndexOf`, literal `StartsWith`/`EndsWith`/`Contains` | 0 |
| Allocating `Substring` | 0 |
| `async void`, sync-over-async, `Task.Run` in request work | 0 |
| Case-normalization comparisons or chained replacements | 0 |
| `params`, character LINQ, general LINQ chains | 0 |
| Per-call list/dictionary construction or mutable static dictionary | 0 |
| Per-call `HttpClient`, serializer options, `string.Format`, or large `stackalloc` | 0 |
| Intentional path-separator `Replace` during cold Project scope open | 1 accepted, not a hot-path finding |
| Unsealed implementation classes | 0; all 7 implementation classes are sealed |

| Severity | Count | Top issue |
| --- | ---: | --- |
| Critical | 0 | None |
| High | 0 | None |
| Moderate | 0 | None |
| Info | 0 | None; the cold path-separator normalization is intentional |

Static pattern analysis does not replace profiling. No runtime speedup claim is made; the structural result is used only to prevent known allocation, async, collection, string, and I/O regressions in the changed paths.

## Components And Browser Proof

- The SB10 Components selection remains valid: `FileBrowser.Components` owns collection browsing and `FileInteraction.Components` owns the read-only known-file dialog. BaseLib buttons/layout wrappers remain the host composition primitives.
- Output pixels did not change in SB11, so the six accepted original-resolution images under `proof/SB10/browser/` were inspected and reused as the bundle permits.
- A fresh managed production run used confirmed revision `candoitall-filetools-sb11:1:g0` at `/projects` with the real QuotationPDFs project.
- At `1900x1200`, exact search returned 1 visible / 7 inspected / 1 retained, and double-click replaced browsing with the read-only `pilot-readme.md` interaction.
- At `1440x900`, all seven rows fit, the interaction dialog remained unclipped, the missing search showed 0 visible / 7 inspected / 0 retained, and the `release-notes.txt` action overlay measured `x=1280, y=431, width=160, height=41` inside the viewport.
- The action overlay explicitly displayed `No actions available`; it did not clip or create a second scroll owner.
- Browser console: 0 errors, 0 warnings. Blazor initializer and negotiate requests returned HTTP 200.
- Runtime logs showed bounded browse/search counters and authorized content open with masked handle/actor identifiers. The only warning was the expected development-only missing HTTPS redirect port.
- The watch backend retained a stale startup-health timeout summary even after `isReadyForHotReload=true`, `WaitingForChanges`, and confirmed revision; this is a monitor-state quirk, not product failure, and no UI/build/test assertion depends on that stale field.

## Dependency And Architecture Proof

- Fresh focused snapshot: `snap-20260713080121-9c272781`.
- Scope: Integration.Abstractions, Integration, Infrastructure, Projects, and Web; FileTools Integration, Infrastructure Storage, and Projects namespaces.
- Facts: 5 projects, 151 documents, 280 types, 1,597 members, zero scoped cycles.
- Findings in the six changed C# owners: zero warnings, zero errors, zero findings.
- Four informational DI collector diagnostics remain in unchanged Infrastructure factory registrations; they are not dependency or runtime failures.
- Dependency direction remains Projects -> Integration.Abstractions/Infrastructure plus FileTools UI packages, Integration -> Abstractions/Infrastructure, and Web -> outer composition. Infrastructure has no reverse FileTools/module edge.

## Closure

Checkpoint C is an unqualified `Pass`. Parent ownership is thin, cleanup and extension proof are complete, lifecycle cleanup is cancellation-safe, bounded counters and direct interaction handoff remain proven, and the desktop browser surface is stable. SB12 may begin.
