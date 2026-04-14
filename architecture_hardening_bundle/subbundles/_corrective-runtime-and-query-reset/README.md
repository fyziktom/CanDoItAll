# Corrective playbook — runtime and query reset

## Status

- `Completed`
- `2026-04-13`: not triggered because Gate C passed without corrective work.

## Objective

- Repair any Gate C failure where publication, runtime, or read-side responsibilities remain overly concentrated or only cosmetically decomposed.

## Covered Inputs

- `BRQ-009` Publish and version hardening.
- `BRQ-010` Runtime state-machine extraction.
- `BRQ-011` Read-side query hardening.
- `BRQ-016` Repeated architecture review gates.
- `BRQ-017` Corrective-first continuation.

## Prerequisites

- Gate C or equivalent service-split proof has failed.
- Subbundles `08-10` were the most recent implemented phases being reviewed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Publication.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Reads.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessesToolsTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessTemplateProjectionServiceTests.cs
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\02-architecture-gate-memo-log.md

## Deliverables

- A corrected service split where publication, runtime, and query responsibilities have clear seams.
- Updated integration and MCP proof for the repaired surfaces.
- Refreshed execution-report and gate-memo records tied to the Gate C rerun.

## Dependency Impact

- Shared-infrastructure consolidation and workspace decomposition depend on Gate C being trustworthy.
- If this corrective path is weak, later decomposition would still sit on renamed monoliths.

## Validation Depth

- `Corrective critical foundation`

## Implementation Steps

1. Capture the failing Gate C evidence and classify the failure as publication coupling, runtime hotspotting, query overreach, or superficial extraction.
2. Apply the smallest correction that creates a real seam instead of another monolithic file split.
3. Rerun focused integration proof for publish, runtime, and query behavior.
4. Rerun MCP process tests when external contracts or projections changed.
5. Rerun Gate C and update the execution report and gate memo before unblocking downstream work.

## Do Not Do

- Do not rename a hotspot and call it decomposition.
- Do not leave broad-load queries in place just because the tests still pass.
- Do not move logic into a shared dumping ground to satisfy the gate.

## Acceptance Checklist

- Publication, runtime, and query responsibilities are materially healthier at the corrected boundary.
- The repaired split has fresh integration or MCP proof for the affected flows.
- Gate C is rerun and recorded with an explicit pass or a new corrective blocker.

## Proof Required

- Focused integration tests for publish, runtime, and query behavior.
- MCP process tests for affected tool or projection contracts.
- Updated `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md`.

## Browser Validation Logging

- This corrective path is usually non-UI. Record `N/A` unless the fix changed a visible runtime or query-backed workspace behavior that must be proven in the browser.

## Progression Gate

- Gate C passes with explicit evidence that publication, runtime, and query responsibilities are no longer concentrated in disguised monoliths.

## Suggested Agent Prompt

```text
Execute only the runtime-and-query corrective subbundle for a failed Gate C. Repair the concentrated service split, rerun focused integration and MCP proof, rerun Gate C, and do not unblock downstream work until the gate passes.
```
