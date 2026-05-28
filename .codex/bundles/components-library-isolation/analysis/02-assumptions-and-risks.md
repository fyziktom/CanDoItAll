# Assumptions And Risks

## Assumptions

- The components repo should own package source and Tailwind source for the moved libraries.
- The main repo should consume component packages from `ExternalPackages` and should keep only CanDoItAll-specific Tailwind source.
- The moved component package version is `0.1.0`.
- The local package source is repo-relative through `NuGet.config`.

## Risks

- Keeping package version `0.1.0` can create stale global NuGet cache behavior after repacks; documentation must call out cache clearing or version bumping for future component changes.
- Web project packaging for `CanDoItAll.Components.Sandbox` may require explicit `IsPackable`.
- Static web assets from local packages must flow into Blazor apps exactly as project-referenced RCL assets did.
- Tailwind source scanning changes can accidentally drop generated utility classes if source roots are incomplete.

## Critical Path Risks

- If package metadata and inter-package dependencies are wrong, the main solution may restore but fail compile or runtime static asset discovery.
- If any direct project reference remains to a moved component, the build graph is not isolated and the main build-time goal is not met.
- If Tailwind output paths are wrong, the app may build but load incomplete styling.

## Validation Risks

- Full browser proof may be blocked by app startup time or local database/runtime configuration. Build and targeted test proof remain required; browser blockers must be explicit.
- Component package restore can pass from a stale global cache. Validation should clear or avoid stale packages before main restore where practical.

## Reopen Triggers

- Any `ProjectReference` in the main repo points to a moved component project.
- Any moved component project remains in `repo://src`.
- Any moved package lacks README/package metadata or version `0.1.0`.
- `CanDoItAll.slnx` still contains Space3D or moved component projects after the split.
- `dotnet build CanDoItAll.slnx` fails from missing packages, missing static assets, or Tailwind output assumptions.
