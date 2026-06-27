# Manager selected-run usage context

## Status

- `Completed`

## Objective

- Give Manager chat selected-run cost and token context and avoid disabling runtime tools for ordinary cost/token questions.

## Success Criteria

- Manager tab runtime load options request selected-run usage telemetry.
- Manager prompt includes selected-run cost and token metrics when loaded.
- Natural cost/token questions keep runtime/workspace tools enabled.

## Covered Inputs

- R003, R004, R005, N004, N005.

## Prerequisites

- Prepared bundle validation passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessManagerChatPromptClassifier.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceShellTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProcessManagerChatPromptClassifierTests.cs`

## Deliverables

- Bounded Manager tab runtime usage load.
- Selected-run usage section in manager prompt.
- Classifier behavior narrowed to explicit "already/preloaded context only" prompts.
- Targeted component and unit tests.

## Dependency Impact

- Browser closure depends on this because the user's cost/token prompt is the concrete regression.

## Validation Depth

- Process-critical prompt/context closure with component and unit proof.

## Implementation Steps

1. Add/adjust component tests for Manager tab load options and prompt text.
2. Patch Manager tab load options to include usage telemetry.
3. Add selected-run usage formatting to `BuildManagerChatPrompt`.
4. Narrow `ProcessManagerChatPromptClassifier`.
5. Run targeted component and unit tests.

## Scope Exceptions

- If the local database has no runtime usage observations for a real run, the prompt must state that usage is not available in the loaded projection instead of fabricating numbers.

## Do Not Do

- Do not load artifacts or full event history just for cost/token questions.
- Do not change runtime usage persistence.

## Acceptance Checklist

- Manager tab request has `IncludeSelectedRun = true`, `IncludeUsageTelemetry = true`, `IncludeHistory = false`, and `IncludeMetricHistory = false`.
- Prompt sent to the manager contains actual/estimated cost and token totals from projection stats.
- Natural "how much did it cost and how many tokens" prompt does not disable runtime tools.

## Proof Required

- Targeted component and unit test commands.
- Execution report row updated.

## Browser Validation Logging

- Deferred to subbundle 03.

## Progression Gate

- Tests prove Manager chat loads and sends usage context.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
