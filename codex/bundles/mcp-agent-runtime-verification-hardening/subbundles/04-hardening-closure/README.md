# 04 Hardening Closure

## Status

- `Completed`

## Objective

Close the work with repeatable proof, bundle traceability, and the rebuilt app running on port 5032.

## Success Criteria

- Focused unit, component, and integration tests pass.
- Playwright MCP evidence is captured at `1920x1080`.
- The bundle is updated with requirements, traceability, and execution proof.
- The rebuilt app is running on `http://localhost:5032/` with the development workspace.

## Covered Inputs

- R005 Large-Screen UI Verification
- R006 Bundle Closure

## Prerequisites

- `01-mcp-setup-runtime-repair`
- `02-database-catalog-compatibility`
- `03-agent-process-workflow-tool-verification`

## Exact Source References

- `repo://codex/bundles/mcp-agent-runtime-verification-hardening/README.md`
- `repo://codex/bundles/mcp-agent-runtime-verification-hardening/requirements/01-normalized-requirements.md`
- `repo://codex/bundles/mcp-agent-runtime-verification-hardening/traceability/01-requirement-traceability.md`
- `repo://codex/bundles/mcp-agent-runtime-verification-hardening/reviews/01-execution-report.md`
- `repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py`

## Deliverables

- Unit tests: 72 passed.
- Component tests: 10 passed.
- Integration tests: 8 passed.
- Live Playwright MCP setup UI: passed.
- Large-screen UI smoke: passed for agents, projects, workflows, and processes.
- App restarted on `http://localhost:5032/` with the development workspace.

## Dependency Impact

- This closes the verification package and makes the proof durable for future review.

## Validation Depth

- End-to-end regression and closure

## Implementation Steps

1. Capture final test outputs.
2. Capture live development workspace state.
3. Capture Playwright MCP screenshots and snapshots.
4. Update bundle documents.
5. Run bundle validator.
6. Confirm port 5032 is listening.

## Scope Exceptions

- No mobile or tablet UI testing by user request.

## Do Not Do

- Do not mark the bundle closed if tests or Playwright MCP setup fail.
- Do not stop the app after final validation.

## Acceptance Checklist

- Tests passed.
- Browser artifacts captured.
- Bundle validator passed.
- App is running on port 5032.

## Proof Required

- Focused test command outputs.
- Playwright MCP screenshots and snapshots.
- Bundle validator output.
- Port 5032 listener check.

## Browser Validation Logging

- Routes: `/agents?tab=capabilities`, `/projects`, `/agents/workflows`, `/processes`
- Viewport: `1920x1080`
- Required actions: setup pass, navigation smoke, screenshot capture.
- Screenshots: `agents-playwright-mcp-setup-passed-large.png`, `projects-large-screen.png`, `workflows-large-screen.png`, `processes-large-screen.png`

## Progression Gate

- Final closure may be reported only after validator success and port 5032 is listening.

## Suggested Agent Prompt

```text
Implement this subbundle only. Close the bundle with test proof, large-screen Playwright MCP evidence, validator output, and the rebuilt app running on port 5032.
```
