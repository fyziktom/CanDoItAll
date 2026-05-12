# Validation And Environment Setup

## Status

- `Completed`

## Objective

- Validate the shipped API/skill changes, run local setup, and close the bundle with durable proof.

## Covered Inputs

- N004
- N005
- R005
- R006

## Prerequisites

- Subbundle 01 is completed or blocked with exact validation notes.
- Subbundle 02 is completed or blocked with exact validation notes.

## Exact Source References

- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\codex\skills\candoitall-api-workflows\SKILL.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`
- `C:\repositories\CanDoItAll\codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py`

## Deliverables

- Validation command proof.
- Reinstall/setup proof.
- Local skill path proof.
- Final execution report and raw-note closure.

## Dependency Impact

- This is the final closure phase. Weak proof leaves the user's restart test unreliable.

## Validation Depth

- End-to-end setup and closure.

## Implementation Steps

1. Run targeted API tests.
2. Run the MCP reinstall script unless a hard blocker appears.
3. Verify `%USERPROFILE%\.codex\skills\candoitall-api-workflows\SKILL.md`.
4. Update execution report and raw-note closure.
5. Run completed-stage bundle validator.

## Scope Exceptions

- Browser proof is not required because no browser-visible UI changes are made.

## Do Not Do

- Do not claim local Codex has loaded the new skill before restart; only verify the skill is installed on disk.

## Acceptance Checklist

- Tests/build commands recorded.
- Reinstall command recorded.
- Local skill file exists.
- User is told restart is still needed for live skill testing.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter WorkflowApiIntegrationTests`
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools\Reinstall-CanDoItAllMcps.ps1 -RepoRoot C:\repositories\CanDoItAll`
- `Test-Path $env:USERPROFILE\.codex\skills\candoitall-api-workflows\SKILL.md`
- `python codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py .codex\bundles\workflow-api-control-skill --profile initiative --stage completed`

## Browser Validation Logging

- N/A - validation and local setup only.

## Progression Gate

- Final closure passes only when API tests, reinstall/local skill proof, and completed-stage bundle validator agree.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Run targeted validation, run the MCP reinstall/setup script, verify the workflow API skill exists in the user Codex skill root, update the execution report and raw-note closure, and run the completed-stage bundle validator.
```
