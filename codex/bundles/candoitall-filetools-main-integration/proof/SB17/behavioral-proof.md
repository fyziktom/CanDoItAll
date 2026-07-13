# SB17 Expansion Architecture Cleanup Gate

## Decision

- Result: `Pass` without qualification.
- Checkpoint: E / critical expansion progression gate.
- SB18 is explicitly unlocked. There is no owner reopen list.

## Concrete Repair

The audit rejected the first shape of SB16 lifecycle orchestration. `ProjectStructurePage.razor` is already part of a 24-file, 10,000+ line partial cluster; adding cancellation-source ownership, replacement, supersession, and asynchronous disposal there would have made the page responsible for resource lifetime and left that behavior difficult to test.

SB17 extracted `ProjectStructureKnownFileInteractionSlot`, a sealed 182-line owner constructed by the page with the coordinator delegate. The page now only opens/closes the dialog and updates visible feedback. The slot owns the current interaction, active operations, replacement, cancellation, and disposal. Direct tests cover replacement/release and superseded-open cancellation. A final review then found and repaired a close-versus-completion race in which a captured operation could dispose its token source before `CloseAsync` cancelled it. Cancellation is now completion-aware and tolerates only the explicit already-completed disposal race.

No interface, DI registration, new partial, service locator, facade, command hierarchy, or unrelated refactor was introduced.

## Before/After Hotspot Inventory

Counts compare `HEAD` to the final worktree. “Members” is a deterministic declaration-line heuristic for public/private/protected/internal fields, properties, and methods; it is a trend signal, not a Roslyn semantic count.

| Owner | Files | Lines | Members | Project refs | Final responsibility |
| --- | ---: | ---: | ---: | ---: | --- |
| `ProjectsPage.razor` | 1 -> 1 | 773 -> 782 (+9) | 79 -> 83 (+4) | Projects 2 -> 3 | Filter/tab/dialog orchestration only |
| `ProjectsBoard.razor` | 1 -> 1 | 666 -> 583 (-83) | 57 -> 62 (+5) | same project | Shared portfolio-card rendering and callbacks; net shrink |
| `ProjectStructurePage.*` partial cluster | 24 -> 24 | 10,658 -> 10,812 (+154) | 702 -> 714 (+12) | Workbench 20 -> 21 | Canvas/window/dialog orchestration only; no scope/content/save/cache/session lifetime |
| `ProjectStructureKnownFileInteractionSlot` | 0 -> 1 | 0 -> 182 | 0 -> 17 | same project | One direct interaction lifetime and cancellation boundary |
| `LiveProcessesDashboard.razor` | 1 -> 1 | 2,888 -> 2,919 (+31) | 176 -> 179 (+3) | Processes module 20 -> 21 | Files entry/open/close orchestration only |
| `ResourcesPage.razor(.cs)` | 2 -> 2 | 512 -> 596 (+84) | 36 -> 41 (+5) | Resources 8 -> 10 | Registry/Browse tab selection and refresh only |
| `RuntimeHostServiceCollectionExtensions.cs` | 1 -> 1 | 1,027 -> 1,030 (+3) | 73 -> 73 | Composition 24 -> 25 | Declarative integration registration only |

The Project Structure cluster remains legacy debt, but SB17 does not disguise that debt with another partial. Its migrated behavior is outside the cluster and directly testable. The remaining delta is thin view wiring distributed only across existing responsibility-named partials (`ToolWindows`, `NodeEditing`, `SelectionPanel`, `Workflows`, and the Razor surface).

## Boundary And Dependency Result

- Projects adds only Integration.Abstractions and contains no Workbench or Resources dependency.
- Workbench, Processes, and Projects consume Integration.Abstractions. Resources consumes Integration plus Integration.Abstractions because promotion/reopen invokes implementation behavior at its application boundary.
- Composition adds the Integration implementation edge. Infrastructure remains unchanged at one project reference and has no FileTools/Integration/module edge.
- Every added edge has `reverse-path=False`; no new project cycle exists.
- Workbench process projection and Processes file scope both consume `Processes.Application.ProcessRunArtifactRootPolicy`. The former Workbench policy source is deleted.
- The only added `GetRequiredService` hit is the Infrastructure composition alias from concrete `StoragePlacementService` to `IStoragePlacementService`, needed by the revision-publishing decorator. Runtime behavior contains no newly added service location and `BuildServiceProvider` remains absent.

## Package And Intent Result

The modules reference only the formats they use. Projects uses FileBrowser Core/Components and FileInteraction Components. Workbench additionally selects FileInteraction Core/Markdown and Mermaid 0.1.3. Processes and Resources select FileBrowser Core/Components plus FileInteraction Core/Components. Unsupported formats remain explicit inert states.

Collection coordinators alone construct `FileBrowserSession`. `ProjectStructureKnownFileInteractionCoordinator`, its slot, and its dialog contain zero FileBrowser references. The current browser showed one `.cdi-ft-interaction`, zero `.cdi-ft-browser`, zero unsigned preview elements, and no `/storage/objects/preview` request for direct Markdown. The Project Structure graph adapter deliberately emits an empty media preview URL even when legacy storage descriptors still carry that route for unrelated consumers.

## Test, Scale, Build, And Format Result

- Affected unit suite: 123/123, including the real 100,000-entry filesystem envelope and all Projects/Workbench/Processes/Resources/integration-boundary cases.
- Final post-repair Project Structure scope/interaction suite: 16/16.
- Affected component suite: 61/61.
- Affected real PostgreSQL integration suite: 11/11.
- Final Release Web build with warnings as errors: 0 warnings, 0 errors.
- Scoped whitespace verification for the SB17 C# owner/test: Pass.
- Full `git diff --check`: exit 0; line-ending notices only.

The repository-wide formatter is not a valid bundle signal: it reports thousands of pre-existing whitespace/style findings in unrelated files. Those files were preserved. The scoped formatter and warning-clean build validate the owned edits.

## Performance Result

The `analyzing-dotnet-performance` scan covered 30 expansion-owner files and 3,571 lines. It found zero `async void`, sync-over-async, `Task.Run`, per-call `HttpClient`, `Substring`, chained replacement, `params`, or unsealed implementation class. The single serializer-options construction is called once by a static field. The six case-normalization calls are invariant or hash normalization. All string search hits use typed collection membership, character overloads, or explicit `StringComparison`. LINQ materialization and mutable collections are bounded scope/catalog/provider assembly, not provider page loops; capacities are supplied where cardinality is known.

No performance optimization claim is made. This is a structural regression gate, backed by the accepted 100,000-entry measured test.

## Browser And Cleanup Result

Fresh managed Release runtime `app_c1c2777c13e542889c838ede3f55be31` exercised Projects, Project Structure collection browsing, Live Processes, Resources, and a controlled direct Markdown interaction. The direct interaction opened, closed, reopened, switched to Edit, saved 165 bytes, navigated away, and reopened with the persisted paragraph and revision 7. Logs show two masked authorized opens and one masked 165-byte save. Dialog and interaction client/scroll widths were 1150/1150 and 1071/1071. Console errors/warnings were zero and Blazor initializer/negotiate returned 200.

After the cancellation race repair, final-source runtime `app_6788b76f0fed4a46bec53d5191a43566` repeated direct open/close/reopen. It ended with one interaction root, zero browser roots, zero unsigned preview elements, visible final content, clean console, and only Blazor initializer/negotiate non-static requests.

Both controlled projects were released/deleted. Current lease bodies were empty, project lists omitted both IDs, residue scans found zero matching paths, and both managed runtime sessions were stopped.

## Tool Fallback And Final Gate

CodeAnalytics and Components MCP transports remained closed on fresh calls. Their absence is recorded, not converted into a fictional success. Deterministic source, graph, package, build, test, scale, and browser evidence all agree.

Checkpoint E passes unqualified. SB18 may perform final regression/security/closure only; it does not need to repair an SB17 owner.

