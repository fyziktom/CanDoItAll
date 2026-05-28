# 01-components-repo-foundation

## Status

- `Completed`

## Objective

- Move the eight requested component projects into `C:/repositories/CanDoItAll.Components`, create a new components solution, add package metadata/readme support, split component Tailwind source there, and produce local packages.

## Covered Inputs

- REQ-001, REQ-002, REQ-003, component side of REQ-007.

## Prerequisites

- None.
- Confirm both worktrees are clean or only contain known bundle preparation files before moving source.

## Exact Source References

- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.BaseLib`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.CanvasLib`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Common`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Charts`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Mermaid`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.OverlayLib`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.WebGlLib`
- `C:/repositories/CanDoItAll.Components/src/CanDoItAll.Components.Sandbox`
- `repo://Tailwind`
- `C:/repositories/CanDoItAll.Components`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`

## Deliverables

- Moved project folders under `C:/repositories/CanDoItAll.Components/src`.
- `C:/repositories/CanDoItAll.Components/CanDoItAll.Components.slnx`.
- Shared package metadata with package version `0.1.0`, package readme inclusion, and package info.
- Component Tailwind workspace and BaseLib output.
- Built `.nupkg` files for all moved projects.

## Dependency Impact

- SB02 cannot start until packages exist. Weak package metadata or missing project moves invalidate all main-repo package conversion proof.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Move the eight project folders from main `src` to components repo `src`.
2. Copy/move component Tailwind source into the components repo and keep only component-owned CSS there.
3. Add components repo `Directory.Build.props`, package metadata, root package scripts, and solution membership.
4. Build the components solution.
5. Pack all moved projects to a staging package folder.
6. Record package inventory and source assertions.

## Scope Exceptions

- Do not move `CanDoItAll.Components` or `CanDoItAll.Components.WebGlSandbox`.

## Do Not Do

- Do not add references from the components repo back to the main repo.
- Do not change component behavior or public APIs unless required to compile after the move.
- Do not publish packages to an external feed.

## Acceptance Checklist

- All eight requested project folders exist in the components repo and do not exist in main `src`.
- Components solution includes the moved projects.
- `dotnet build` succeeds for the components solution.
- `dotnet pack` produces version `0.1.0` packages.
- Package metadata includes README and useful description.

## Proof Required

- `proof/SB01/manifest.md` with changed-file hashes, package inventory, transcripts, source assertions, and anti-stub audit.
- `proof/SB01/semantic-invariants.md` naming the package/source isolation invariants.
- Transcript for components solution build.
- Transcript for package creation.
- Source assertion proving no components repo project references the main repo.
- Anti-stub audit for TODO/NotImplemented/package placeholder markers in production source and package metadata.

## Browser Validation Logging

- N/A. This phase is build/package foundation only.

## Progression Gate

- Pass only when packages exist and the components solution builds from the components repo without main-repo project references.

## Suggested Agent Prompt

```text
Implement SB01 only. Move the requested component projects to the components repo, create solution/package metadata, build and pack them, and capture proof. Stop if the components repo needs a main repo project reference.
```
