# Project Structure Output Grounding

## Status

- `Completed`

## Objective

- Make process dispatch grounding include external output folders from relevant project-level planning branches when a process is launched from a nested delivery node.

## Success Criteria

- A fixture matching the live project shape grounds `C:\programovani\dotnet-demo\output`.
- The prompt for a grounded external output root explicitly requires final delivery in the external target before completion.
- Existing unrelated-output tests still prevent unrelated sibling work items from contaminating the prompt.

## Covered Inputs

- R1 External Output Grounding.
- Raw note 1.

## Prerequisites

- Prepared bundle validator passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ProjectPaths.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Grounding.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.ExecutionPrompt.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`

## Deliverables

- Broader, bounded project-level planning focus selection.
- Prompt finalization rule for grounded external output roots.
- Regression tests for the nested delivery plus top-level architecture output-folder shape.

## Dependency Impact

- Future generic Blazor app delivery runs depend on this for correct product root selection.
- SB03 proof may use run paths from these prompts; weak grounding would make projection evidence misleading.

## Validation Depth

- Critical process foundation.

## Implementation Steps

1. Add a failing test for a nested delivery target whose external output path lives under a top-level architecture branch.
2. Update focus-node selection to include relevant planning siblings of target branch ancestors.
3. Update prompt text to forbid completion before final external-target delivery when a grounded external root exists.
4. Run targeted dispatch tests.

## Scope Exceptions

- Does not rerun the full Blazor delivery process.

## Do Not Do

- Do not hard-code `TetrisGame`, `dotnet-demo`, the project id, or the run id.
- Do not include arbitrary unrelated work item paths.

## Acceptance Checklist

- Grounding summary contains `external-target/C/programovani/dotnet-demo/output`.
- Prompt contains an external-target finalization instruction.
- Existing output-root trimming and unrelated-node tests pass.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"`

## Browser Validation Logging

- N/A. This subbundle changes process prompt generation, not UI layout.

## Progression Gate

- Passed. Targeted dispatch tests prove the architecture output folder is grounded and the prompt requires final external-target delivery proof.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
