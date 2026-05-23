# SB02 Anti-Stub Audit

## Agent Context

- The disabled contributor test asserts both status and trace reason.
- The test also asserts no recall messages were produced and no recall orchestrator request was recorded.
- The request intentionally lacks project scope, matching the reported failure shape.

## Scheduled Automation

- The disabled scheduled automation test configures invalid downstream inputs that would fail if the guard were too late.
- Recording fakes for ingestion and consolidation expose accidental downstream calls.
- Assertions require empty request collections for both services.

## Workflow Executors

- Source assertions cover all three workflow executors: recall, probe, and learning proposal.
- Each executor reads `ICognitiveMemoryAutomationSettingsService.GetAsync` and returns an explicit skipped JSON payload before deserializing or validating project-specific executor settings.
