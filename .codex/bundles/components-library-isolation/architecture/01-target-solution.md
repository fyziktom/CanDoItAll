# Target Solution

## End State

- `C:/repositories/CanDoItAll.Components` owns the isolated component libraries, their solution file, package metadata, component Tailwind source, and component sandbox.
- `repo://ExternalPackages` contains the built private packages for the moved projects.
- The main repo has no source folders for the moved projects and no project references to them.
- The main solution still contains `CanDoItAll.Components`, `CanDoItAll.Components.WebGlSandbox`, app/module/test projects, tools, and non-Space3D source.
- A separate Space3D solution owns Space3D projects outside the main slnx build path.

## Boundaries

- No API redesign of the component libraries unless required by package isolation.
- No behavioral refactor of Blazor components.
- No NuGet server or external feed setup; local packages live in `ExternalPackages`.
- No movement of `CanDoItAll.Components` or `CanDoItAll.Components.WebGlSandbox`.

## Package Boundary

- Moved component libraries can retain project references among themselves inside the components repo so package dependency metadata is generated normally.
- Main repo projects consume packages with `PackageReference Include="CanDoItAll.Components.*" Version="0.1.0"`.
- Main repo package restore is enabled through a repo-local `NuGet.config` source pointing to `ExternalPackages`.

## Tailwind Boundary

- Component Tailwind builds in the components repo and emits `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css` for the BaseLib package.
- Main Tailwind builds in the main repo and emits `src/CanDoItAll.Web/wwwroot/css/output.css` for CanDoItAll-specific classes.
- Web app markup loads BaseLib output first, then CanDoItAll-specific output.

## Validation Strategy

- Build and pack components repo first.
- Restore/build main repo against `ExternalPackages`.
- Run focused tests that compile component consumers.
- Attempt app/browser smoke if build succeeds; if blocked, record the blocker with command transcript.
