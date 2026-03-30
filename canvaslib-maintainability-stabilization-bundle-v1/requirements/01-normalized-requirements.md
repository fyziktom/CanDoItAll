# Normalized Requirements

## R001 Canonical CanvasLib Asset Ownership

- CanvasLib must keep one canonical JS/CSS file copy for the active asset surface in source control.
- The active repo surface must not keep parallel identical `wwwroot\css` + `wwwroot\css-src` or `wwwroot\js` + `wwwroot\js-src` trees.

## R002 Duplicate Audit Beyond CanvasLib

- The bundle must inventory other repo duplicate patterns relevant to the same stabilization concern.
- If a legacy duplicate canvas implementation exists and is unreferenced, the bundle must either retire it or record an explicit exception with evidence.

## R003 CanvasLib Component Folder Reorganization

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components` must be reorganized into topic-based subfolders that reduce the current flat root folder density.
- Public component namespaces and behavior must remain stable.

## R004 Canvas Graph Folder Reorganization

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Graph` must be reorganized into topic-based subfolders that group related behaviors and primitives.

## R005 Large File Decomposition

- Mixed-responsibility files must be split into coherent classes or records without changing their functional contract.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs` is a required split target.

## R006 Behavior Preservation

- Shared canvas features must continue to work as before on project-structure, prompt-factory, and calendar routes.
- Public static asset URLs must remain valid for consuming modules.

## R007 Closure Audit

- The final closure must include:
  - asset pipeline proof
  - build and test proof
  - browser proof
  - duplicate audit proof
  - large-file and folder-density audit proof

## R008 Scope Discipline

- Repo-wide hotspots outside the requested CanvasLib and duplicate-cleanup scope may be recorded as follow-up work, but the bundle must not silently expand into unrelated module rewrites.
