# C# Dependency Direction

## Current Project Facts

Snapshot `snap-20260713002602-7de53bec` shows no project cycle in the seven-project integration slice. Projects -> Infrastructure; Resources -> Infrastructure + Projects; Workbench -> Infrastructure + Projects + Resources; Processes module -> Infrastructure; Composition -> all module owners; Web -> Composition and selected modules.

## Target References

| From | Allowed new references | Reason |
| --- | --- | --- |
| Infrastructure | none to FileTools/Integration/modules | native storage must remain reusable and inward |
| Integration.Abstractions | FileTools Abstractions and, only if contract needs it, FileBrowser Core | stable host integration contracts |
| Integration | Integration.Abstractions, Infrastructure, FileTools Abstractions/Core, HybridCache package | outer adapter/security/cache implementation |
| Projects | Integration.Abstractions, FileTools Core/Components/Interaction packages selected for its UI | consume neutral scope and render UI |
| Workbench | Integration.Abstractions and selected FileTools UI/runtime packages | implement project/node scope and window |
| Processes module | Integration.Abstractions and selected FileTools UI/runtime packages | implement run scope/dialog |
| Resources | Integration.Abstractions and selected FileTools UI/runtime packages | implement source catalog/promotion UI |
| Composition/Web | Integration implementation + modules | concrete registration and endpoints |

## Forbidden References

- FileTools repository/project -> any main project.
- Infrastructure -> FileTools or Integration.
- Integration.Abstractions -> Infrastructure, persistence, Web, or modules.
- Integration implementation -> modules.
- Projects -> Workbench/Resources.
- Processes/Application -> Workbench.
- Domain/runtime behavior -> Web or component packages.
- Any inner project -> Composition to obtain services.

## Cycle Strategy

If a needed type creates a cycle, stop. Move only stable records/interfaces to Integration.Abstractions or an existing correct Contracts project. Do not use `object`, reflection, duplicated DTOs, `Common`, or service location.

## Proof

- Read all affected `.csproj` files before each reference edit.
- Capture before/after direct-reference tables and package graph.
- Refresh scoped CodeAnalytics snapshot and `dependencies_get` after SB06 and each module wave.
- Run affected builds and composition smoke.
- Confirm the existing Persistence/ControlPlane module cycle is unchanged and no new project/module cycle appears.
