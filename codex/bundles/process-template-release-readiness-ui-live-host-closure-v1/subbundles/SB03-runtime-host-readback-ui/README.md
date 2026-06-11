# SB03: Runtime-host operator readback

## Status
- Status: `Completed`

## Objective
Close or explicitly classify the UI gap for runtime-host manager diagnostics/readback.

## Covered Inputs
- REQ-003: Expose runtime-host manager diagnostics/readback in operator-visible run detail UI or create an explicit API endpoint with a tracked UI follow-up.

## Prerequisites
- SB02 must be completed or honestly blocked without being counted as release proof.
- Existing runtime-host readback records must remain read-only and tied to real process run/step ids.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationReadback.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerRuntimeHostDryRunReadback.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime
- repo://src/CanDoItAll.Web
- repo://tests/CanDoItAll.Tests.Playwright

## Deliverables
- Preferred: run-detail operator panel/section showing runtime-host status, audit id/hash, lane/capability key, evidence reference count, denial category/code/message, no-mutation flags, and mutation-permission flags.
- Acceptable fallback: stable API/readback route with tracked UI follow-up and honest release classification.
- Tests proving readback is tied to real process run/step ids and remains non-mutating.

## Dependency Impact
- SB04 and SB07 depend on the run detail/readback route classification.
- SB08 must cite this proof when deciding whether the release is UI-ready.

## Validation Depth
- Backend/API or UI test proof for real run/step readback.
- Large desktop Playwright proof if UI changes are made.
- Negative proof that runtime-host readback does not grant mutation permissions.

## Implementation Steps
1. Prefer UI implementation if feasible: add run-detail operator readback surface.
2. Add API/read service if run detail needs a backend endpoint.
3. If UI is too large, add a stable API/readback service route, a clear UI backlog item, and mark UI as not release-blocking only with API proof.

## Do Not Do
- Do not mutate process state through runtime-host surfaces.
- Do not introduce execution-capable process drivers.

## Acceptance Checklist
- Runtime-host readback is tied to real process run/step ids.
- Large desktop Playwright proof exists if UI changes.
- API/facade proof exists if no UI route is implemented.
- No runtime-host mutation permissions.

## Proof Required
- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`
- Passing transcript for API/UI readback proof.
- Failing-first or adversarial transcript for mutation-permission rejection.
- Browser screenshot and Playwright transcript if UI changes are made.

## Browser Validation Logging
- If UI changes are made, record route, `1900x1200` viewport, Playwright MCP evidence, screenshot path, visual review result, and pass/fail in the execution report.
- If API fallback is used, record `N/A - API fallback` with the API transcript path.

## Progression Gate
- SB04 may start only after runtime-host readback is classified as UI-proven, API-proven with explicit UI follow-up, or blocked without being counted as UI release proof.

## Suggested Agent Prompt
Implement the smallest operator-visible runtime-host readback surface or API fallback for SB03, capture proof and browser analytics if needed, then run the closure gate before SB04.
