# Acceptance

- Add a canonical trigger record/model owned by the application.
- Preserve at least cron, timezone, enablement, start/end window, and misfire semantics canonically.
- Add a Quartz-backed runtime projection/bridge for these canonical triggers.
- Quartz job keys and trigger keys must be deterministic and explicit.
- Quartz jobs must publish durable work; they must not execute heavy plugin logic inline.
- Recommended exact types:
  - `AutomationTriggerRecord`
  - `IAutomationTriggerRegistry`
  - `QuartzAutomationSchedulerBridge`
  - `AutomationTriggerFireRequest`
