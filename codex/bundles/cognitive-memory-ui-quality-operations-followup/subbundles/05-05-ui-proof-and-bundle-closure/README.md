# 05-ui-proof-and-bundle-closure

## Status

- `Completed`

## Completion Evidence

- Focused component tests, focused unit tests, and web build passed.
- Playwright proof at `/cognitive-memory` used a 1920x1080 viewport and captured Quality operations plus Memory pager screenshots.
- Prepared and completed bundle validators passed after execution report updates.

## Objective

Prove the UI follow-up is complete with tests, build, large-screen browser validation, and bundle closure.

## Covered Inputs

- UI-01 through UI-14.

## Prerequisites

- Subbundles 01 through 04 complete.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-ui-quality-operations-followup\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj`

## Deliverables

- Passing targeted unit and component tests.
- Passing module build.
- Large desktop browser proof with tab walk.
- Completed execution report with raw-note closure.
- Prepared and completed bundle validators passed after material edits.

## Dependency Impact

- Final closure only.

## Validation Depth

- End-to-end UI proof.

## Implementation Steps

1. Run targeted unit and component tests.
2. Build CognitiveMemory module.
3. Run large-screen browser proof for `/cognitive-memory`.
4. Update execution report, browser analytics, and raw-note closure.
5. Run prepared and completed validators.

## Do Not Do

- Do not substitute imagegen proposals for browser proof.
- Do not add medium/small proof requirements.
- Do not leave pending raw-note rows.

## Acceptance Checklist

- Tests pass.
- Build passes.
- Browser proof covers every tab at a large desktop viewport.
- Bundle validators pass.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryReviewUiServiceTests" --logger "console;verbosity=minimal" -m:1`
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemoryPageTests" --logger "console;verbosity=minimal" -m:1`
- `dotnet build src\CanDoItAll.Modules.CognitiveMemory\CanDoItAll.Modules.CognitiveMemory.csproj --no-restore -m:1`
- Large desktop browser proof for `/cognitive-memory`.
- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py --stage completed --profile initiative codex\bundles\cognitive-memory-ui-quality-operations-followup`

## Browser Validation Logging

- Record route, viewport, tab actions, screenshots, and pass/fail result in the execution report.

## Progression Gate

- Bundle closes only when tests, build, browser proof, raw-note closure, and validators pass.

## Suggested Agent Prompt

```text
Implement subbundle 05 only. Run final proof, update bundle closure artifacts, and do not close with pending rows.
```
