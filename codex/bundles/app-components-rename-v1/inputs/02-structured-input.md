# Structured Input

## Core Objective

- Rename only the main-repo app-specific component facade from `CanDoItAll.Components` to `CanDoItAll.AppComponents`.

## Success Criteria

- The old tracked project path `repo://src/CanDoItAll.Components/CanDoItAll.Components.csproj` no longer exists.
- The renamed tracked project path `repo://src/CanDoItAll.AppComponents/CanDoItAll.AppComponents.csproj` exists.
- The project assembly name, root namespace, Razor namespaces, C# namespaces, and direct app/test imports use `CanDoItAll.AppComponents`.
- `repo://CanDoItAll.slnx`, `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`, and `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj` reference the renamed project path.
- External package references under `CanDoItAll.Components.*` remain unchanged.
- Targeted build and component tests pass.

## Hard Constraints

- Do not touch the sibling component repository at `C:\repositories\CanDoItAll.Components`.
- Do not rename NuGet package references owned by the sibling repo.
- Do not rename `repo://src/CanDoItAll.Components.WebGlSandbox`.
- Keep the fix scoped to rename fallout and directly related docs.

## Allowed Side Effects

- Move tracked files from `repo://src/CanDoItAll.Components` to `repo://src/CanDoItAll.AppComponents`.
- Update app/test consumers that import the old facade namespace.
- Update local documentation that specifically describes the in-repo facade path or project name.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `repo://src/CanDoItAll.Components/CanDoItAll.Components.csproj`
- `repo://CanDoItAll.slnx`
- `repo://src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `repo://tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj`

## Input Coverage Signals

- `N001`: rename exactly `C:\repositories\CanDoItAll\src\CanDoItAll.Components`.
- `N002`: repair projects that use this in-repo project.
- `N003`: do not touch the sibling components repository.

## Dependency And Sequencing Signals

- The path/project rename must happen before references can be validated.
- Namespace repair must happen before build and component tests can pass.
- Stale-reference search must distinguish the old facade from sibling-repo package names and paths.

## Validation Expectations

- Use `rg` to find stale exact facade references.
- Build the renamed project directly.
- Run the component test project that references the facade.
- Run prepared and completed bundle validators.

## Evidence Contract

- `bundle://proof/SB01/transcripts/renamed-project-build.txt`
- `bundle://proof/SB01/transcripts/component-tests.txt`
- `bundle://proof/SB01/transcripts/stale-reference-search.txt`
- `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB01/semantic-invariants.md`

## UI Validation Strategy

- N/A. This is a project identity and compile/reference repair. No rendered layout or browser-visible behavior is intended to change.

## Browser Validation Analytics

- Record a single N/A row in `repo://codex/bundles/app-components-rename-v1/reviews/01-execution-report.md` stating that browser proof is not applicable.

## Working Assumptions

- `AppComponents` should follow repository project naming convention as `CanDoItAll.AppComponents`.
- `CanDoItAll.Components.BaseLib`, `CanDoItAll.Components.CanvasLib`, and other package names remain valid because they come from the sibling repository.

## Primary Risks

- Overbroad replacement could break valid sibling-repo package references.
- Leaving an old project reference would break restore/build.
- Generated `bin`/`obj` artifacts may contain stale strings and must be excluded from source-level stale-reference checks.
