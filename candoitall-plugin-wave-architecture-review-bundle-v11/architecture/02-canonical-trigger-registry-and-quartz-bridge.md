# Canonical trigger registry and Quartz bridge

## Decision
Use **your own canonical trigger registry** as the source of truth.
Use **Quartz** as the runtime scheduler projection.

## Why this shape is better than making Quartz tables canonical
- The application keeps ownership of schedule semantics and metadata.
- Plugin-facing APIs remain stable even if scheduler implementation details evolve.
- The platform can validate, diff, enable/disable, preview, and inspect trigger definitions in its own model.
- Quartz remains a runtime engine, not a domain model.

## Required canonical trigger fields
At minimum each trigger definition must preserve:

- `Id`
- `OwnerKind` (platform / module / plugin / project / agent)
- `OwnerKey`
- `TriggerKey`
- `IsEnabled`
- `TriggerKind` (cron / once / relative / due-date projection)
- `CronExpression`
- `TimeZoneId`
- `StartAtUtc`
- `EndAtUtc`
- `MisfirePolicy`
- `PayloadJson`
- `DedupeKey`
- `NextPlannedFireAtUtc`
- `LastFiredAtUtc`
- `UpdatedAtUtc`

## Recommended exact types for phase11
- `AutomationTriggerRecord`
- `AutomationTriggerState`
- `IAutomationTriggerRegistry`
- `QuartzAutomationSchedulerBridge`
- `AutomationTriggerFireRequest`

## Required runtime rule
Quartz jobs must stay thin.
A Quartz wakeup may enqueue a durable internal message, but it must not execute heavy plugin logic inline.

## Important Quartz rule for the bundle
Trigger keys and job keys must be explicit and deterministic.
No anonymous runtime-only schedules are allowed for durable automation.
