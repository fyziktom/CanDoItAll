# Validation And Browser Proof

## Status

- `Completed`

## Objective

- Prove the template-backed default agents work through builds, tests, source audit, browser validation, and completed bundle validation.

## Success Criteria

- Affected project builds.
- Targeted integration tests pass.
- Source audit confirms obsolete hardcoded default-agent definitions/assets are removed.
- Browser validation opens the local app and confirms expected seeded agents/teams are visible without route errors.
- Completed-stage bundle validation passes.

## Covered Inputs

- R009: regression tests for template loading and seeded teams.
- R010: .NET and Playwright/browser validation that agents work as before.
- N006: explicit Playwright MCP/browser validation.

## Prerequisites

- SB01 loader and template pack implemented.
- SB02 seed migration implemented and buildable.

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentTeamCatalogIntegrationTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web`
- `C:\repositories\CanDoItAll\.codex\bundles\agent-template-teams`

## Deliverables

- Passing targeted build/test output.
- Browser validation screenshots/assertions.
- Completed bundle proof manifests and execution report.

## Dependency Impact

- This is the final closure gate. Any failure reopens SB01 or SB02 depending on whether the issue is template shape, seed materialization, or UI surfacing.

## Validation Depth

- End-to-end regression and closure

## Implementation Steps

1. Run prepared-stage bundle validation.
2. Run affected project build and targeted integration tests.
3. Run source audit for old hardcoded default-agent artifacts.
4. Start the local app if needed.
5. Use browser automation to validate the agent/team surface on desktop and narrow viewport where practical.
6. Capture proof artifacts and run completed-stage bundle validation.

## Scope Exceptions

- Deep LLM provider execution is not required; the goal is seeded catalog/template parity and agent visibility.

## Do Not Do

- Do not claim browser parity from tests alone.
- Do not suppress test failures by weakening assertions.
- Do not close if any raw note remains pending.

## Acceptance Checklist

- Build/test commands exit 0.
- Browser route shows seeded agents/teams and no app error surface.
- Execution report rows cite proof paths.
- Completed bundle validator exits 0.

## Proof Required

- Command transcripts under `proof/SB03/transcripts`.
- Browser screenshot(s) under `proof/SB03/browser`.
- Completed execution report with raw note closure.
- `proof/SB03/manifest.md` with changed-file hashes and proof artifact paths.
- Captured proof manifest: `proof/SB03/manifest.md`.

## Browser Validation Logging

- Target route: local app agent catalog route, expected to be `/agents` or the discovered agent page.
- Required viewport passes: desktop around 1440x900 or wider, plus a narrower follow-up if layout is responsive.
- Required actions/assertions: navigate, wait for app render, assert expected default team/agent names, inspect for visible route/runtime errors, capture screenshot.
- Screenshot paths: `proof/SB03/browser/agents-desktop.png` and optional `proof/SB03/browser/agents-narrow.png`.
- Review questions: Are default teams/agents visible? Are template-oriented agents still present? Is there any route error, blank state, or obvious overlap?

## Progression Gate

- Final closure may proceed only when tests, browser proof, source audit, and completed-stage bundle validation pass.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
