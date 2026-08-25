# BR01 — Create the ProviderManagement compile boundary

## Objective

Create the new outer AgentFramework ProviderManagement project and enforce its zero-Workspace dependency before moving behavior.

## Required implementation

1. Add:
   - `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/CanDoItAll.Modules.AgentFramework.ProviderManagement.csproj`
   - `ProviderManagementModuleAssemblyMarker`
   - `ProviderManagementServiceCollectionExtensions` with a minimal registration entry point
2. Add the project to the canonical solution and affected central build metadata.
3. Add its assembly marker to existing module/EF configuration discovery where appropriate.
4. Add project references only to lower-level dependencies allowed by `TARGET-BOUNDARY.md`.
5. Add a focused architecture test that fails when:
   - ProviderManagement references Workspace by project
   - ProviderManagement source references a Workspace namespace/type
6. Introduce or relocate only the minimal neutral provider application/runtime contracts required to prevent future cycles. Reuse suitable existing contracts instead of duplicating them.
7. Do not move provider implementation yet unless required to prove the compile boundary.

## Design constraints

- This project is not Razor.
- It is not an inner MAF project.
- It must not reference the AgentFramework Razor module.
- It must not reference Web, Workbench, Workspace, or feature UI projects.
- Do not introduce a new DbContext.

## Acceptance

- New project builds.
- Composition/module discovery builds.
- Architecture test proves zero Workspace dependency.
- Existing application behavior is unchanged.
- Final guard may still report expected ownership violations from later subbundles, but no new violation is introduced.

## Focused validation

- restore canonical solution once if needed
- build new project
- build Composition or the smallest project that validates marker discovery
- run the new architecture test only
- `git diff --check`

## Commit

`BR01: create provider management boundary`
