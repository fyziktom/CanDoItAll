# 07 CanDoItAll Components Split And Adoption

## Objective

Turn `CanDoItAll.Components` into the CanDoItAll-only composition library after `BaseLib` and `CanvasLib` exist, then rewire CanDoItAll modules and the web app to the new shared libraries.

## Exact Source References

Current CanDoItAll-only candidates:

- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components\AppShellMode.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\Components\AppTabStrip.razor`

Current consumers:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace`

## Implementation Steps

1. Move CanDoItAll-only shells and compositions into the final `CanDoItAll.Components` project.
2. Replace `CanDoItAll.ComponentKit` and old `CanDoItAll.Components` references with:
   - `Common`
   - `BaseLib`
   - `CanvasLib`
   - `CanDoItAll.Components`
3. Update imports/usings in CanDoItAll modules.
4. Keep CanDoItAll-specific behavior close to the app-specific library.
5. Remove redundant generic components from `CanDoItAll.Components` once the app uses the shared owner.
6. Update `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor` asset references to the new shared library asset locations.

## Hard Rules

- do not re-copy generic wrappers back into `CanDoItAll.Components`
- do not let app-specific library own shared CSS/assets
- do not remove compatibility paths before the app builds and renders cleanly

## Acceptance Checklist

- CanDoItAll app and modules compile against the new shared library layout
- `CanDoItAll.Components` contains only CanDoItAll-specific compositions
- no CanDoItAll module references the old mixed ownership structure accidentally
- app asset includes point at the new shared libraries

## Proof Required

- reference graph diff for CanDoItAll projects
- build output
- targeted screenshot proof for CanDoItAll main shell and major module pages

## Suggested Agent Prompt

```text
Implement subbundle 07 only.

Rewire CanDoItAll to the new shared library structure after BaseLib, CanvasLib, and the asset pipeline are stable. Move CanDoItAll-only shells into CanDoItAll.Components and remove generic ownership from the app-specific library. Do not start Zyphonote rewiring in this phase.
```
