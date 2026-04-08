# Reinstall rerun and closure

## Status

- `In progress`

## Objective

- Reinstall the updated MCP, rerun the affected Zyphonote scenarios and regression checks, close both raw findings, and validate the bundle to completion.

## Covered Inputs

- `REQ-06`
- Prior parity rerun scorecard
- Both residual findings as closure targets

## Prerequisites

- `subbundles/01-project-inventory-classification-and-filtering`
- `subbundles/02-focused-context-legacy-intent-compatibility`
- Prepared-stage bundle validation has passed.

## Exact Source References

- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CanDoItAll.Mcp.CodeAnalytics.csproj`
- `C:\repositories\CanDoItAll\candoitall-codeanalytics-gap-closure-bundle-v1\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\candoitall-codeanalytics-gap-closure-bundle-v1\README.md`
- `C:\repositories\zyphonote\bundles\2026-04-08-codeanalytics-vs-sharptools-evaluation\subbundles\02-scenario-ground-truth-and-benchmark-tasks\01-scenario-matrix.md`

## Deliverables

- Reinstall proof for the updated MCP
- Updated rerun scorecard for the gap scenarios and regression path
- Closed findings or new follow-up findings if anything still remains
- Completed-stage bundle validation proof

## Dependency Impact

- This is the closure phase. Weak proof here invalidates the entire bundle.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Build and reinstall the updated MCP.
2. Re-run the affected Zyphonote inventory and focused-context scenarios against the installed server.
3. Regress the prior parity answer path enough to prove the bundle did not damage the existing `47 / 50` state.
4. Update the bundle README, execution report, and finding status.
5. Run the completed-stage validator and repair any documentation defects before exit.

## Scope Exceptions

- If a Codex restart is required for refreshed native schema proof, document that explicitly and continue closure using installed-server proof until the user refreshes the session.

## Do Not Do

- Do not change the Zyphonote scenario matrix or answer key.
- Do not claim the findings are closed without installed-server proof.
- Do not leave the bundle at completed stage with `Ready` or `In progress` subbundles.

## Acceptance Checklist

- Reinstall succeeds.
- Scenario 1 primary answer is precise and supporting projects remain visible.
- `Behavior` compatibility path succeeds.
- Existing focused-context `TroublePath` path still succeeds.
- Bundle documentation and validators both show completion.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CanDoItAll.Mcp.CodeAnalytics.csproj --no-restore`
- `powershell -NoProfile -ExecutionPolicy Bypass -File C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1 -RepoRoot C:\repositories\CanDoItAll`
- Installed-server query proof for the two fixed gaps
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\candoitall-codeanalytics-gap-closure-bundle-v1 --profile initiative --stage completed`

## Browser Validation Logging

- `N/A`

## Progression Gate

- Final closure only: reinstall, rerun proof, raw-note closure, and completed-stage validation must all pass.

## Suggested Agent Prompt

```text
Implement the reinstall rerun and closure subbundle only. Do not change the scenario matrix, prove the two findings are actually closed on the installed MCP, and do not finish until the completed-stage validator passes.
```
