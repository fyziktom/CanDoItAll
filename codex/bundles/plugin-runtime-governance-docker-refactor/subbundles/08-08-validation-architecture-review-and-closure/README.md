# SB08 Validation Architecture Review And Closure

## Status

- `Ready`

## Objective

- Run final validation across the plugin runtime refactor, Docker sample, workflow LLM summary scenario, browser proof, architecture boundaries, performance findings, EF query findings, and bundle closure state.

## Success Criteria

- All prior subbundle gates are complete or explicitly blocked with reasons.
- The completed bundle validator passes.
- Architecture review finds no raw shell exposure, grant confusion, Docker-specific core leakage, EF large-log storage, or missing browser proof.

## Covered Inputs

- `N001` through `N009`
- Requirements `R001` through `R024`

## Prerequisites

- SB01 through SB07 are completed or explicitly blocked.
- Execution report contains commands, browser artifacts, and gate results from each completed subbundle.
- Any blocker found in SB07 is resolved or marked as closure-blocking.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-governance-docker-refactor
- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginManifestContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Pages\PluginsPage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Commands\WorkspaceCommandExecutionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Process\LocalWorkspaceProcessHost.cs

## Deliverables

- Final architecture review covering plugin genericity, grants, host tools, workflow bridge, Docker sample, EF, performance, UI, and observability.
- Completed execution report with subbundle gate results, browser analytics, raw-note closure, commands, and residual risks.
- Completed-stage bundle validator result.
- Final recommendation for any follow-up bundle if OS sandboxing, marketplace packaging, or richer Docker UI is needed.

## Dependency Impact

- This is the closure gate. Weak proof here reopens the responsible earlier subbundle.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Review execution report for every subbundle gate.
2. Re-run targeted test suites listed by completed subbundles.
3. Run browser validation for every changed UI route and review screenshots.
4. Review architecture boundaries for generic plugin model and no raw host access.
5. Review performance and EF findings from SB07.
6. Verify Docker sample workflow proof and LLM summary separation.
7. Run completed-stage bundle validator.
8. Update final closure decision and residual risks.

## Scope Exceptions

- No new feature implementation in this subbundle except small fixes required to close validation failures.
- No expansion into plugin marketplace, OS sandboxing, or advanced Docker management UI.

## Do Not Do

- Do not close the bundle if any critical subbundle lacks proof.
- Do not waive missing browser screenshots for UI changes.
- Do not close with known raw PowerShell, raw shell, or raw command service exposure.
- Do not hide residual risks in summary text without execution report entries.

## Acceptance Checklist

- Bundle validator passes for completed stage.
- Tests listed by subbundles pass or have explicit, justified blockers.
- Browser analytics rows contain route, viewport, actions, assertions, screenshots, and results.
- Architecture review confirms install/enable/grant separation.
- Docker sample proves generic host-tool recipe use and separate LLM summary workflow.
- EF/performance review confirms no large Docker logs in EF and no obvious N+1 grant path.

## Proof Required

- Completed-stage validator command and result.
- Test command list with outcomes.
- Browser screenshot list with visual review notes.
- Architecture review notes and any code review findings.
- Final raw-note closure table with no pending rows.

## Browser Validation Logging

- Route: all UI routes changed by SB04, SB05, SB06, or SB07.
- Viewports: large-screen pass plus narrower-width follow-up for layout changes.
- Playwright actions: replay critical permission settings, workflow missing-grant, and sample workflow result flows.
- Screenshots: final evidence paths for each changed route.
- Review questions: no overlap, no misleading permission text, no secret leakage, and no hidden missing-grant state.

## Progression Gate

- Bundle may close only when completed-stage validation passes and every raw note has proof or a clearly documented blocker.

## Suggested Agent Prompt

```text
Implement SB08 only.
Run final validation and architecture review for the plugin runtime governance refactor. Update the execution report and close only when tests, browser proof, EF/performance review, Docker sample proof, and bundle validator evidence are complete.
```
