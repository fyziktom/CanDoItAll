# Structured Input

## Objectives

- Move `CanDoItAll.Components.BaseLib`, `CanDoItAll.Components.CanvasLib`, `CanDoItAll.Components.Common`, `CanDoItAll.Components.Charts`, `CanDoItAll.Components.Mermaid`, `CanDoItAll.Components.OverlayLib`, `CanDoItAll.Components.WebGlLib`, and `CanDoItAll.Components.Sandbox` into `C:/repositories/CanDoItAll.Components`.
- Create a new components solution there and make every moved project packageable as version `0.1.0`.
- Build the moved projects into NuGet packages and place those private packages under `repo://ExternalPackages` in the main repository.
- Replace main-repo project references to moved component projects with package references. Do not reference moved projects directly from the main repository.
- Keep `CanDoItAll.Components` and `CanDoItAll.Components.WebGlSandbox` in the main repository, but convert their moved-component dependencies to packages.
- Split Tailwind so component-library styles are built in the components repository and CanDoItAll-specific styles are built in the main repository.
- Remove Space3D projects from `repo://CanDoItAll.slnx` and add a separate Space3D solution file.
- Update documentation in both repositories.

## Hard Constraints

- Do not move `CanDoItAll.Components` or `CanDoItAll.Components.WebGlSandbox`.
- Do not use cross-repo project references for the moved components.
- Preserve strongly typed .NET references through package references rather than ad hoc assembly loading.
- Every moved package must include package metadata and a README.
- Main solution must build after package restore from `ExternalPackages`.

## Assumptions

- NuGet package versions use semver `0.1.0` to satisfy the requested `0.1` version in a NuGet-friendly form.
- Local private packages are acceptable as committed `.nupkg` artifacts under `ExternalPackages` because the user explicitly requested that folder.
- Browser visual proof is lower priority than build/package proof because this is primarily a repository and build-graph split; UI smoke is still required when the app can be started.

## Validation Expectations

- Build the components solution in `C:/repositories/CanDoItAll.Components`.
- Pack all moved projects and verify `.nupkg` files exist in `repo://ExternalPackages`.
- Restore/build the main solution from `repo://CanDoItAll.slnx`.
- Run focused component and unit tests if build succeeds.
- Record explicit blockers for any app/browser proof that cannot run in the current environment.
