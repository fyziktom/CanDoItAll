# Project Rename And Reference Repair

## Status

- `Completed`

## Objective

- Rename the main-repo app-shell facade project to `CanDoItAll.AppComponents` and repair every direct source-controlled consumer of that project identity.

## Success Criteria

- `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj` exists.
- `repo://src/CanDoItAll.Components/CanDoItAll.Components.csproj` no longer exists.
- Project assembly/root namespace and compiled facade source use `CanDoItAll.AppComponents`.
- Solution, web app, and component tests point to the renamed project path.
- Direct old facade imports are repaired while `CanDoItAll.Components.*` package imports remain unchanged.
- Targeted build, targeted tests, stale-reference search, and anti-stub audit pass.

## Covered Inputs

- `N001`: rename exactly `C:\repositories\CanDoItAll\src\CanDoItAll.Components`.
- `N002`: repair projects that use this in-repo project.
- `N003`: do not touch the sibling components repository.

## Prerequisites

- Prepared-stage bundle validator passed on 2026-05-30.
- No earlier implementation subbundles exist.

## Exact Source References

- `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj`
- `repo://src/CanDoItAll.AppComponents/_Imports.razor`
- `repo://src/CanDoItAll.AppComponents/ComponentNamespaceMarker.cs`
- `repo://src/CanDoItAll.AppComponents/Components/AppShell.razor`
- `repo://src/CanDoItAll.AppComponents/Components/AppShellMode.cs`
- `repo://src/CanDoItAll.AppComponents/Components/AppShellNavigationMode.cs`
- `repo://src/CanDoItAll.AppComponents/Components/AppTabStrip.razor`
- `repo://src/CanDoItAll.AppComponents/Components/TunableComponentBoundary.razor`
- `repo://src/CanDoItAll.AppComponents/Components/TuningBoundaryRequest.cs`
- `repo://CanDoItAll.slnx`
- `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`

## Deliverables

- Renamed project directory and project file.
- Updated assembly/root namespace and C#/Razor facade namespaces.
- Updated solution and project references.
- Updated direct app/test namespace imports.
- Updated local docs for the renamed in-repo facade.
- Proof artifacts under `bundle://proof/SB01/`.

## Dependency Impact

- The web app build depends on the renamed project reference resolving correctly.
- Component tests depend on the renamed namespace resolving app-shell types.
- Documentation and future agent guidance depend on the distinction between `CanDoItAll.AppComponents` and sibling `CanDoItAll.Components.*` packages.

## Validation Depth

- Critical foundation: source graph rename, targeted build, direct component tests, stale-reference audit, semantic adequacy evidence, proof manifest, and anti-stub audit.

## Implementation Steps

1. Move `repo://src/CanDoItAll.Components` to `repo://src/CanDoItAll.AppComponents` and rename the `.csproj`.
2. Update the project assembly/root namespace to `CanDoItAll.AppComponents`.
3. Update C# and Razor namespaces inside the renamed project.
4. Update `repo://CanDoItAll.slnx`, web project, and component test project references.
5. Update direct exact app/test imports from `CanDoItAll.Components` to `CanDoItAll.AppComponents`.
6. Update docs that specifically name the old in-repo facade.
7. Run targeted validation and capture transcripts.

## Scope Exceptions

- Do not rename `CanDoItAll.Components.*` package references or namespaces.
- Do not rename `CanDoItAll.Components.WebGlSandbox`.
- Do not edit `C:\repositories\CanDoItAll.Components`.

## Do Not Do

- Do not perform broad string replacement over `CanDoItAll.Components.` package namespaces.
- Do not change component behavior, styling, or app-shell layout.
- Do not delete or rewrite unrelated generated artifacts.

## Acceptance Checklist

- Old project path no longer exists.
- New project path exists and is included in the solution.
- Web app and component tests reference the renamed project path.
- Exact old facade namespace imports are gone from source-controlled compiled consumers.
- Targeted build passes.
- Component tests pass.
- Stale-reference search shows only expected sibling package/repo or WebGlSandbox references.

## Proof Required

- `bundle://proof/SB01/transcripts/renamed-project-build.txt`
- `bundle://proof/SB01/transcripts/component-tests.txt`
- `bundle://proof/SB01/transcripts/stale-reference-search.txt`
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`
- Failing-first proof: N/A - process/non-production rename; stale-reference search is the adversarial negative proof.

## Browser Validation Logging

- N/A. This subbundle changes project identity and references only; no browser-visible behavior is intended.

## Progression Gate

- SB01 can close only after targeted build, component tests, stale-reference search, anti-stub audit, proof manifest, semantic invariant contract, and completed-stage bundle validator all pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
