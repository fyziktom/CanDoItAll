# Acceptance

- Add canonical trigger persistence with cron, timezone, enablement, start/end window, and misfire semantics.
- Add a trigger registry API.
- Add Quartz-backed runtime projection / rehydration.
- Quartz jobs must publish durable work rather than executing plugin business logic inline.
- Recommended exact types:
  - `AutomationTriggerRecord`
  - `IAutomationTriggerRegistry`
  - `QuartzAutomationSchedulerBridge`
  - `AutomationTriggerFireRequest`
