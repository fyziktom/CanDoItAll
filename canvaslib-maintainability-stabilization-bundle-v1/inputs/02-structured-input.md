# Structured Input

## Core Objective

- Leave CanvasLib with one canonical asset tree and a maintainable C# topology, while preserving the current browser behavior and public asset URLs.

## Hard Constraints

- Preserve runtime behavior on project-structure, prompt-factory, and calendar surfaces.
- Do not silently change public component namespaces or break consumer imports.
- Do not change published asset URLs unless an equivalent compatibility path is explicitly preserved.
- Remove duplicate asset copies rather than keeping parallel source/public mirrors in the active repo surface.
- Reorganize CanvasLib `Components` and `Canvas\Graph` into topic-based subfolders.
- Split combined model or contract files where they currently mix unrelated concerns, including `CanvasWorkbenchContracts.cs`.
- Treat unreferenced legacy canvas copies as a stabilization problem, not as a permanent parallel implementation.

## Source Artifacts

- Raw user request in `inputs/00-original-request.md`
- Visual Studio screenshot described in `inputs/01-source-artifacts.md`
- Current CanvasLib asset pipeline files and current folder topology in the repo
- Previous CanvasLib reorganization bundle for context only, not as an authority over the new scope

## Input Coverage Signals

- `assure we have just one valid copy of folders/files in repo` cannot be reduced to only hiding folders in Solution Explorer
- `analyze other parts of the repo for potential duplicities like this` requires a repo-level duplicate inventory, not only CanvasLib
- `too large files, too many files in one folder are not ok` requires explicit hotspot evidence and a closure audit
- `organize components in Components folder in CanvasLib to sub folders` requires a real folder plan, not only rename noise
- `same for Canvas.Graph folder` requires a parallel topic grouping for backing classes
- `assure that all functions are always preserved all is working as before` requires behavioral validation, not only compile success

## Dependency And Sequencing Signals

- Asset ownership must close first because later component and graph moves should happen against the final canonical asset layout.
- Component and graph folder moves must finish before the bundle can claim large-folder stabilization.
- Contract splitting must happen before final file-size and maintainability closure proof.
- Legacy duplicate retirement, if safe, should happen before the final duplicate audit so the closure claim is honest.

## Validation Expectations

- Asset manifest and include generation commands must pass.
- CanvasLib and web builds must pass.
- Component tests must pass.
- Playwright browser proof must cover the affected shared canvas surfaces.
- Repo duplicate audit must prove the CanvasLib asset mirror is gone and record any remaining explicit exceptions.
- CanvasLib line-count and folder-density audits must show the targeted cleanup actually happened.

## UI Validation Strategy

- Run the first browser pass at a large desktop viewport on the project-structure surface because it exercises the shared workbench shell.
- Recheck prompt-factory and calendar routes because they also consume CanvasLib assets and components.
- Review screenshots for missing static assets, broken menus, clipped overlays, shell spacing regressions, and any render failure caused by file moves.
- Follow the large-screen pass with a narrower-width pass on at least one workbench route to make sure reorganization did not break responsive asset loading.

## Browser Validation Analytics

- `reviews/01-execution-report.md` must log route, viewport, Playwright actions, screenshots, and result for each UI-relevant subbundle.
- Asset ownership proof must include direct route loads for project structure, prompt factory, and calendar.
- Final closure must cite the concrete Playwright tests or screenshot artifacts used as regression proof.

## Working Assumptions

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\_Imports.razor` keeps the CanvasLib namespace stable across folder moves.
- The active solution is `C:\repositories\CanDoItAll\CanDoItAll.slnx`.
- `CanDoItAll.ComponentKit` is legacy and not part of the active solution unless later proof shows an external dependency.
- The current `build-assets.cjs` script performs copy synchronization only; there is no transform that justifies storing two identical asset trees in source control.

## Primary Risks

- Moving Razor component files into subfolders can break consumer resolution if namespace rules drift.
- Removing duplicate asset trees can break static asset loading if manifest generation and include components are not updated together.
- Retiring legacy `ComponentKit` canvas copies can be unsafe if a hidden reference exists outside the solution graph.
- Splitting shared workbench models can break JSON state normalization or event contracts if type names or defaults change.
