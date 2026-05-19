# 01-runtime-launch-foundation

## Status

- Status: `Completed`

## Objective

- Expand runtime launch resolution so every supported runtime node with valid trusted metadata can launch PowerShell from the configured folder and offer normal/admin actions.

## Success Criteria

- Runtime launch plans resolve for Script, Environment, Python, .NET, and command-backed Docker nodes.
- Resolved plans include the intended command, working directory, target path, display command, and display name.
- UI action capability resolution can truthfully expose both `runtime:open` and `runtime:admin`.

## Covered Inputs

- `N001`: runtime nodes must start processes and offer normal/admin launch choices.
- `R001`
- `R002`

## Prerequisites

- Bundle readiness gate has passed.
- Existing runtime action rendering tests are still present.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureRuntimeLauncher.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureNodeActionCapabilityResolver.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs

## Deliverables

- Runtime launcher supports command-backed Docker infrastructure nodes.
- Runtime launcher respects configured working directories or path-based fallback directories.
- Runtime actionCapabilities describe both normal and administrator actions for every resolved runtime plan.
- Focused tests prove launch-plan resolution and action rendering.

## Dependency Impact

- `02-02-folder-file-link-actions` depends on stable action menu insertion and path-resolution behavior.
- `03-03-agent-catalog-and-ui-proof` depends on runtime capabilities being truthful before catalog guidance advertises them.

## Validation Depth

- Critical foundation with resolver tests, component action tests, and downstream action-rendering smoke.

## Implementation Steps

1. Add or adjust runtime metadata support for docker command nodes without bypassing workspace guards.
2. Add resolver tests for Script command, Python environment, .NET runtime, and Docker command working directories.
3. Add or adjust page/action tests proving normal/admin actions appear for valid runtime nodes.
4. Record command proof and update execution report rows.

## Scope Exceptions

- Direct UAC approval automation is not required in this phase, but admin launch action presence and request routing must be proven.

## Do Not Do

- Do not execute untrusted commands during tests.
- Do not bypass `IWorkspacePathAccessGuard`.
- Do not change process/workflow start behavior.

## Acceptance Checklist

- [x] Runtime plans resolve for requested runtime categories, including Docker infrastructure commands.
- [x] Runtime actions are hidden for non-runtime nodes.
- [x] Normal and admin actions dispatch separate requests.
- [x] Downstream actionCapabilities smoke remains valid.

## Proof Required

- Targeted .NET tests for runtime launcher and component action rendering.
- Execution report row for entry and closure gate.
- Browser proof may be captured later in `03-03-agent-catalog-and-ui-proof`, but this phase must log planned UI proof.

## Browser Validation Logging

- Route: `/workbench/projects/{projectId}/structure`.
- Viewport: large desktop first pass.
- Actions: create or select valid runtime node, open double-click quick-action/dialog/action surface, assert normal and admin labels are visible.
- Screenshot: runtime quick-action or inspector action screenshot path recorded in `reviews/01-execution-report.md`.
- Review questions: actions readable, not clipped, not hidden behind floating windows, and labels clearly distinguish normal/admin.

## Progression Gate

- Passed. Runtime tests passed and Playwright MCP proof captured `project-structure-runtime-doubleclick-dialog.png`.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
