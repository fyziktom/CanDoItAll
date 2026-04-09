# Codex task — PRM-F14

Implement **Operational intelligence, improvement backlog, and training-opportunity loop** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Reuse CRM-HR, Activity, Automation, Validation, TestLab, and Security seams where the bundle says so.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

- The module can turn outcome telemetry and conformance signals into process-level improvement candidates.
- Improvement requests are separated from live execution state and can be routed to owner/governance review.
- Training-opportunity markers can be generated without contaminating normal execution queries.
- The design remains compatible with a later intelligence-lake layer.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/ProcessInsightsService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessImprovementModels.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessInsightsIntegrationTests.cs`