# Browser proof and bundle closure

## Status

- `Completed`

## Objective

- Finish the containment work with synchronized tests, browser evidence, raw-note closure, and bundle validator passes.

## Covered Inputs

- `Solve it as bundle.`
- `Use components MCP and Chat page example for fit-to-window containment.`
- All browser-proof expectations from the screenshot-driven regression.

## Prerequisites

- Subbundle 01 passed its closure gate.
- Subbundle 02 passed its closure gate.

## Exact Source References

- `C:\repositories\CanDoItAll\cdi_process_workspace_containment_bundle\README.md`
- `C:\repositories\CanDoItAll\cdi_process_workspace_containment_bundle\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\cdi_process_workspace_containment_bundle\traceability\01-requirement-traceability.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProcessManagementBundle.cs`

## Deliverables

- Completed execution report with commands, browser artifacts, analytics review, and raw-note closure.
- Final validator pass at `--stage completed`.
- Honest residual-risk notes if anything remains open.

## Dependency Impact

- Final bundle closure depends on this phase. Weak proof here would leave the screenshot complaint unresolved despite code changes.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run the targeted build and test commands.
2. Capture browser evidence for the processes page and the templates modal.
3. Review the screenshots against the visual questions and record the results immediately.
4. Complete the raw-note closure table.
5. Run the final bundle validator and sync the bundle status fields.

## Scope Exceptions

- None planned.

## Do Not Do

- Do not call the work complete with only code reasoning.
- Do not leave browser analytics or gate results in a pending state.

## Acceptance Checklist

- Commands are recorded with pass or fail outcomes.
- Browser artifacts are recorded with real file paths.
- Raw-note closure is complete and no row remains `Pending`.
- Bundle root README, execution report, and validator state agree.

## Proof Required

- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Process_management_template_library_flows_are_validated_in_browser" -v:minimal`
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\cdi_process_workspace_containment_bundle --profile feedback --stage completed`

## Browser Validation Logging

- Route: `/processes`
- Viewports: desktop required, narrower follow-up required if layout changed under reduced width
- Required actions: navigate, inspect workspace shell, open templates dialog, switch to diagrams, zoom Mermaid, capture screenshots
- Expected artifacts:
- `output/playwright/process-workspace-containment/01-processes-workspace-shell.png`
- `output/playwright/process-workspace-containment/02-template-library-dialog.png`
- `output/playwright/process-workspace-containment/03-template-library-mermaid-contained.png`
- Required review answers: all text readable, no overlap, no clipping, internal scroll regions behave as intended, Mermaid stays contained

## Progression Gate

- This is the terminal subbundle. Closure passes only when tests, browser proof, raw-note closure, and the final bundle validator all pass or are explicitly blocked with evidence.

## Suggested Agent Prompt

```text
Close the bundle only after running the targeted tests, capturing browser evidence, and synchronizing the execution report and validator state.
```
