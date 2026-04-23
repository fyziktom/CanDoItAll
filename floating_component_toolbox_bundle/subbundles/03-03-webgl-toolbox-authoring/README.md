# 03 WebGL Toolbox Authoring

## Status

- Status: `Ready`

## Objective

- Add a WebGL floating component toolbox that uses the shared OverlayLib toolbox body and can add a new process role into the 3D scene.

## Covered Inputs

- R1: WebGL can have a different component catalog and should use the same generic toolbox principle.
- R2: add a new role with the component toolbox and validate that it appears in 3D.

## Prerequisites

- Subbundle 01 shared toolbox contract completed and gate passed.
- Existing WebGL overlay windows and chrome host actions remain intact.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\Components\Pages\ProcessWorkbench.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.WebGlSandbox\ProcessWebGlSandboxSession.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessWebGlSceneAdapter.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateCatalogService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEditorModels.cs

## Deliverables

- WebGL sandbox toolbox overlay with process role template items.
- Host toolbar restore/open action for the hidden WebGL toolbox if needed.
- Session logic to add a role from a template and rebuild the surface.
- Command-log entry that identifies the added role.

## Dependency Impact

- Final validation depends on proving the new role appears in the WebGL scene.
- The addition must not interfere with existing WebGL selection, delete, connect, and overlay chrome actions.

## Validation Depth

- Build WebGlSandbox and WebGlLib.
- Add targeted session/unit test if practical for `AddRoleFromToolbox`.
- Playwright MCP add role from toolbox and inspect WebGL scene/DOM/screenshot for the new role.

## Implementation Steps

- Adapt process role templates to generic toolbox items in `ProcessWorkbench.razor`.
- Add an overlay window for the component toolbox using `OverlayWindow` plus `OverlayComponentToolbox`.
- Add session method to create and insert a role template into the working editor.
- Ensure role layout makes the role visible and not immediately filtered out.
- Add or reuse toolbar action to reopen the toolbox if hidden.

## Do Not Do

- Do not persist WebGL sandbox role edits outside the existing sandbox model.
- Do not change process template pack files.
- Do not break WebGL model rendering or existing host chrome actions.

## Acceptance Checklist

- WebGL toolbox opens over the stage.
- Role template items are visible and actionable.
- Clicking a role template increases the role count.
- The new role appears in the 3D scene as a role/person node.
- Existing selection and command overlay windows still work.

## Proof Required

- Build output.
- Targeted session/unit test output if added.
- Playwright MCP screenshot before and after adding a role.
- Browser assertion that a new role label/node is present after the toolbox action.

## Browser Validation Logging

- Log WebGL route, viewport, toolbox click, DOM or scene assertion, screenshot paths, and pass/fail result in `reviews/01-execution-report.md`.

## Progression Gate

- Final bundle validation may start only after a WebGL toolbox role add is visible in 3D.

## Suggested Agent Prompt

- Add a WebGL floating component toolbox using the shared OverlayLib toolbox body. Wire role-template items to sandbox session role insertion, rebuild the surface, and validate the new role appears in 3D.
