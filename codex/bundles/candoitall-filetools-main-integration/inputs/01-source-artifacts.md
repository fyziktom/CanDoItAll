# Source Artifacts

## Repository Pins

| Source | Branch | Commit at preparation | Status |
| --- | --- | --- | --- |
| `C:\repositories\CanDoItAll` | `file-tools-browsing` | `355f1d621f4106e1ba2f8709fa5ae09499ddec46` | Clean before bundle creation; bundle files plus the requested `.gitignore` removal are the only intended changes |
| `C:\repositories\CanDoItAll.FileTools` | `main` | `bdfa4a307dbff3316e3c2699d7483f41ff1d91de` | Clean |

Execution must record fresh commits and worktree state in `proof/SB01`; if source anchors moved materially, repair this bundle before product edits.

## Legacy Bundle Evidence

- `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration\README.md`
- `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration\requirements\01-normalized-requirements.md`
- `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration\architecture\07-candoitall-integration.md`
- `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration\architecture\08-cache-and-invalidation.md`
- `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration\architecture\09-ui-assets-and-layout.md`
- `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration\proof\SB08\transcripts\integration-design-audit.md`
- `C:\repositories\CanDoItAll.FileTools\codex\bundles\candoitall-filebrowserintegration\traceability\02-input-coverage.md`

The legacy artifacts prove FileTools transfer and standalone behavior. They do not prove current main-app integration, current dependency direction, endpoint authorization, package intake, or current UI behavior.

## FileTools Product Sources

- `C:\repositories\CanDoItAll.FileTools\README.md`
- `C:\repositories\CanDoItAll.FileTools\docs\host-integration-security.md`
- `C:\repositories\CanDoItAll.FileTools\docs\file-browser.md`
- `C:\repositories\CanDoItAll.FileTools\docs\file-interaction.md`
- `C:\repositories\CanDoItAll.FileTools\docs\build-and-packaging.md`
- `C:\repositories\CanDoItAll.FileTools\docs\package-architecture.md`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.Abstractions`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileBrowser.Core`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileBrowser.Components`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Core`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Components`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Markdown`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.Providers.FileSystem\FileSystemFileBrowserProvider.cs`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileBrowser.Core\Search\ProgressiveFileBrowserSearchStrategy.cs`
- `C:\repositories\CanDoItAll.FileTools\src\CanDoItAll.FileTools.FileInteraction.Components\Models\FileInteractionContentLoader.cs`

## Main-App Evidence

- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage`
- `repo://src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://src/App/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs`
- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.Projects/Pages/Components/ProjectsBoard.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureCanvasDialogs.razor`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/Modules/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`
- `repo://tests/Components/CanDoItAll.Tests.Components`
- `repo://tests/Playwright/CanDoItAll.Tests.Playwright`

## Tool Evidence And Gaps

- Main CodeAnalytics snapshot: `snap-20260713002602-7de53bec`.
- FileTools snapshot attempt: `snap-20260713002618-8e808777`; unusable (`0` projects) because SDK `10.0.301` is not installed.
- Components MCP `components_libraries_list` and `components_recommend` both returned `Transport closed`; no negative component-catalog conclusion is allowed.
- Direct package inventory shows no `CanDoItAll.FileTools.*` packages under `repo://ExternalPackages`.
- Performance preparation used `C:\Users\lucys\.codex\skills\optimizing-dotnet-performance\SKILL.md` and `C:\Users\lucys\.codex\skills\analyzing-dotnet-performance\SKILL.md` plus their detected-category references. Results are captured in `bundle://analysis/03-dotnet-performance-audit.md`.

## Drift Rule

Absolute sibling paths are discovery context, not portable completion artifacts. Execution proof must use `repo://` and `bundle://` for main/bundle artifacts and must record the FileTools commit plus package hashes for cross-repository intake.
