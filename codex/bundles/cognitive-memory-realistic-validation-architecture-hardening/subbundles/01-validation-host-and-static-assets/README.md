# 01-validation-host-and-static-assets

## Status

- `Ready`

## Objective

Harden local validation startup so static assets, Blazor hydration, and production-like hosting failures are explicit and testable.

## Required Edits

- Add startup diagnostics for missing static web assets.
- Document supported validation startup modes.
- Add a smoke test or scripted check for `_framework/blazor.web.js`.

## Closure Proof

- Development startup returns 200 for `_framework/blazor.web.js`.
- Production-like startup either succeeds or reports a precise configuration error.
- Evidence is captured under the validation bundle proof directory.

## Covered Inputs

- Static asset hosting failed in a no-build production-like startup path until the app ran in Development from the web project directory.

## Prerequisites

- The web project builds and can be started from `src/CanDoItAll.Web`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`

## Deliverables

- Startup diagnostics or validation notes that distinguish missing static assets from application startup failures.

## Dependency Impact

- Later browser validation depends on this subbundle because UI proof is invalid if the host serves stale or missing static assets.

## Validation Depth

- Use a real host check for `_framework/blazor.web.js` and capture the route, status code, and environment.

## Implementation Steps

- Confirm supported startup modes, add diagnostics where needed, then capture proof from the web project working directory.

## Do Not Do

- Do not hide static asset failures behind silent fallback hosting behavior.

## Acceptance Checklist

- Static asset readiness is explicit before UI validation starts.
- A production-like failure reports actionable configuration state.

## Proof Required

- Build output and HTTP status proof for the Blazor framework asset.

## Browser Validation Logging

- Record large-screen route, viewport, action, expected status, and screenshot path when UI proof is run.

## Progression Gate

- Proceed only when the validation host can serve the Cognitive Memory module reliably or the blocker is explicitly recorded.

## Suggested Agent Prompt

- Harden Cognitive Memory validation startup by proving static asset readiness and documenting supported host modes without changing unrelated web behavior.
