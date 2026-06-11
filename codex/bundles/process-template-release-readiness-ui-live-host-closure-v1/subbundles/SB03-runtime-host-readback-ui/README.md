# SB03: Runtime-host operator readback

## Objective
Close or explicitly classify the UI gap for runtime-host manager diagnostics/readback.

## Exact source references
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerReadOnlyVerificationReadback.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessManagerRuntimeHostDryRunReadback.cs
- repo://src/CanDoItAll.Modules.Processes/Runtime
- repo://src/CanDoItAll.Web
- repo://tests/CanDoItAll.Tests.Playwright

## Implementation options
Prefer UI implementation if feasible:
1. Add run-detail operator panel/section showing:
   - runtime-host status,
   - audit id/hash,
   - lane/capability key,
   - evidence reference count,
   - denial category/code/message,
   - no-mutation and mutation-permission flags.
2. Add API/read service if run detail needs a backend endpoint.

Fallback if UI is too large:
1. Add a stable API/readback service route.
2. Add clear UI backlog item in docs.
3. Mark UI as not release-blocking only if Playwright/API proof shows operator can retrieve the readback through an existing route or endpoint.

## Acceptance checklist
- Runtime-host readback is tied to real process run/step ids.
- Large desktop Playwright proof if UI changes.
- API/facade proof if no UI route is implemented.
- No runtime-host mutation permissions.
