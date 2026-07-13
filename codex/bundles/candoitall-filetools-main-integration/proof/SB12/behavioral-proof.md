# SB12 Behavioral Proof

## Decision

- Status: `Pass`.
- Scope: project portfolio Files view, shared project/subproject filtering, deterministic source-set replacement, focused project-card Files dialog, and read-only known-file handoff.
- Progression: SB13 is unlocked. A later filter divergence, stale source acceptance, reverse module reference, dialog lifecycle defect, or collapsed embedded-browser layout reopens SB12.

## One Source Of Truth

`ProjectFileFilterProjection` is the sole Cards/Files projection. It owns the typed filter values, deterministic project order, cycle-safe hierarchy closure, include-subprojects behavior, and fingerprint. `ProjectsPage` constructs it once; `ProjectsBoard` sends the same instance to `ProjectPortfolioCards` and `ProjectFilesPortfolioPane`. The Files pane does not reproduce filter or hierarchy policy.

The portfolio coordinator resolves every projected Project semantic scope through the accepted binding/session factory, builds an ordered aggregate FileBrowser source set, and commits its source-to-scope map only after `UpdateSourcesAsync` succeeds. Its revision includes the projection fingerprint, ordered project identifiers, browse-session revisions, binding/source details, storage fingerprint, and catalog storage/scope revisions. A removed source is rejected as `Conflict` before authority is requested.

## Architecture And Responsibility Result

| Owner | Responsibility | Result |
| --- | --- | --- |
| `ProjectsPage.razor` | Page data/state and one shared projection | 782 lines after SB12; 798 before SB12 |
| `ProjectsBoard.razor` | Filter controls, hierarchy shell, and Cards/Files tab orchestration | 583 lines after extraction; 666 before SB12 |
| `ProjectPortfolioCards.razor` | Card rendering and card actions | Focused 187-line component |
| `ProjectFileFilterProjection.cs` | Pure typed filter/hierarchy/fingerprint policy | Focused 289-line directly tested owner |
| `ProjectFilePortfolio.cs` | Aggregate source construction, atomic replacement, activation routing | Focused 185-line coordinator |
| `ProjectFilePortfolioSessions.cs` | Typed source-set revision and owned workspace lifetime | Focused 88-line lifetime owner |
| `ProjectFilesPortfolioPane.razor` | Latest-request UI orchestration and browser/interaction replacement | Focused 320-line pane |
| `ProjectFilesDialog.razor` | One-project focused dialog | Kept outside `ProjectModalHost` |

No partial class, nested policy service, service locator, workspace facade, or page-local recursive filter was added. The four fresh CodeAnalytics `COMPLEXITY-002` informational findings describe cohesive typed records/coordinators with 9-14 source members; none is a large-file warning or mixed-responsibility owner.

## Components Decision

Direct Components server recommendation/catalog calls selected the existing BaseLib `Tabs`/`TabsItem` contract with `WorkspaceSecondary`, `FillHeight`, and hidden panel overflow, plus the existing generic `CheckBox<TValue>` wrapper for Include subprojects. Exact component metadata, usage, and examples were inspected before editing. No host clone of either component and no Radzen dependency was introduced.

## Automated Proof

| Surface | Command scope | Result |
| --- | --- | --- |
| Unit | `ProjectFileFilterProjection`, FileTools integration boundary, and storage browse cache filters | `27/27 Pass` |
| Components | `ProjectsPageTests` | `19/19 Pass` in 1m43s after the final layout repair |
| Integration | `ProjectsServiceIntegrationTests` and `ManagedFilesStorageIntegrationTests` | `12/12 Pass` |
| Build | `dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -p:TreatWarningsAsErrors=true` | `Pass`, 0 warnings, 0 errors |
| Format | focused Projects project `dotnet format --verify-no-changes --no-restore` | `Pass` |

Direct regressions prove deterministic ordering/fingerprint, cycle-safe descendant closure, include-subprojects scope/fingerprint change, stale selection removal, source removal and stale-key rejection, catalog-revision source replacement with valid-location preservation, Cards/Files projection identity, explicit retry after portfolio failure, browser disposal before content handoff, Back reconstruction, and unauthorized/stale rejection.

## Performance Review

The required focused static scan covered the sixteen changed product C#/Razor owners and found zero `async void`, sync-over-async, `Task.Run`, per-call `HttpClient`, ambiguous `IndexOf`, `Substring`, case-fold comparison, literal string search without explicit comparison, chained `Replace`, character LINQ, `params`, whole-file reads, `ReadToEnd`, per-call `JsonSerializerOptions`, `string.Format`, or `stackalloc` signals.

Ten explicit collections and three LINQ materializations were inspected. They are bounded result/source-map/projection state, mostly capacity-sized; four are pre-existing session-factory collections. Ordered project identifiers are materialized once per projection and adjacency is grouped once per filter evaluation. No collection is created inside the provider item loop or retained without the 64-source/project cap. This is a source-level review, not a runtime allocation benchmark.

## Managed Browser Proof

Primary production proof used managed revision `candoitall-filetools-sb12:1:g2` at `http://127.0.0.1:5502/projects` with the real `QuotationPDFs Tests` project and seven real managed files.

- Selecting `QuotationPDFs Tests` changed the shared projection from four visible cards/zero file sources to one card/one source.
- Files displayed `QuotationPDFs Tests files`, `1 project(s)`, `1 source(s)`, and revision `9112779f2d09`.
- Exact search `pilot-readme` returned one visible item after seven inspected items.
- Unchecking Include subprojects preserved the valid single-project source count and changed the source-set revision to `09d53dd41715`, proving the typed filter is fingerprinted even when this project has no descendants.
- Double-click and keyboard Enter both replaced FileBrowser with the read-only `pilot-readme.md` interaction. The browser workspace was disposed first; the content remained readable. Back rebuilt the file source.
- The project-card Files action opened the focused `Files · QuotationPDFs Tests` dialog. The dialog FileBrowser occupied 1758 px of its 1792 px desktop content frame and was closed/reopened normally.
- Missing search produced `0 visible`, `7 inspected`, and the explicit `No matching items` state. The product error/retry branch is covered by the focused component test rather than by corrupting production storage.
- The action popover was viewport-fixed at `x=1265, y=801, width=160, height=41` at 1440x900 and displayed `No actions available` without clipping.
- At 1900x1200, document client/scroll dimensions were exactly 1900x1200. At 1440x900, width remained 1425/1425 with no lateral overflow; the document was the single actual vertical scroll owner for the 1083 px desktop board.
- The primary run recorded 0 console errors, 0 console warnings, and successful Blazor `/projects`, initializer, and negotiate requests.
- Managed logs record bounded searches (`Returned=1/0`, `Inspected=7`, retained counts/bytes), source revisions `9112779f2d09` and `09d53dd41715`, authorized opens with masked handles/actor values, and successful hot reload generations 1 and 2.

The final wide list screenshot was recaptured from the already-built Release DLL after a Playwright resize compositor artifact. A normal managed RunOnce Release build attempt hit the runner's existing Windows 260-character artifacts-path limit; the product Release build itself had already passed from the repository output path.

### Browser Artifacts

- `browser/project-portfolio-files-1900x1200.png`
- `browser/project-portfolio-interaction-1900x1200.png`
- `browser/project-card-files-dialog-1900x1200.png`
- `browser/project-portfolio-files-1440x900.png`
- `browser/project-portfolio-no-result-1440x900.png`
- `browser/project-portfolio-overlay-1440x900.png`

## Dependency And C# Gate

- Fresh focused snapshot: `snap-20260713091027-759c0917`.
- Facts: 5 projects, 156 documents, 292 scoped types, 1,673 members, zero scoped cycles.
- Four changed-slice findings are informational member-count notices for the typed filter/projection, coordinator, and workspace lifetime. There is no changed large-source warning, dependency warning, layering warning, or error.
- The four DI diagnostics are the unchanged informational Infrastructure factory-registration limitations.
- Comparing snapshot `snap-20260713080121-9c272781` to the final snapshot reports no project-reference or package-reference change in any of the five scoped projects.
- Source assertions find no Projects reference to Workbench or Resources.

Dependency direction remains Projects -> Integration.Abstractions/Infrastructure and selected FileTools UI packages; Integration -> Abstractions/Infrastructure; Infrastructure and Abstractions have no reverse module/FileTools edge.

## Closure

All five SB12 acceptance checks pass. The shared project-scope semantics and direct extension seam are ready for SB13 Project Structure reuse; no SB10/SB11 foundation was reopened.
