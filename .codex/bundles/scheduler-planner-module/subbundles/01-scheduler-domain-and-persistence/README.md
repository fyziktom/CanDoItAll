# 01-scheduler-domain-and-persistence

## Status

- `Completed`

## Objective

- Create the SchedulerPlanner module foundation: strongly typed schedule domain, persistence, service contracts, validation, and CRON description support.

## Success Criteria

- SchedulerPlanner module project exists and is registered for EF model discovery and Blazor routing.
- Schedule definitions persist with typed workflow/process target metadata.
- CRON validation and human-readable description generation are isolated behind an adapter service.
- Schedule list/detail/history query contracts exist for the later UI.
- Focused tests prove persistence, validation, and CRON description behavior.

## Covered Inputs

- SPM-R001
- SPM-R004
- SPM-R005
- SPM-R008, query foundation only
- SPM-R010, history model foundation only
- SPM-R016, CRON description package decision

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\CanDoItAll.Composition.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Runtime\AutomationRuntimeContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Runtime\AutomationRuntimeModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`

## Deliverables

- New `CanDoItAll.Modules.SchedulerPlanner` project with marker type and service registration extension.
- `SchedulerPlan` and `SchedulerPlanRun` entities with EF configurations.
- Strongly typed enums/value objects for schedule target kind, run status, target run kind, and owner/trigger key generation.
- SchedulerPlanner application service contracts for create/update/enable/disable/list/detail/history.
- `ICronDescriptionService` implementation and tests.
- Package decision for CRON description, recorded in the execution report.
- Initial EF migration or schema integration following existing migration conventions.

## Dependency Impact

- Subbundle 02 depends on stable schedule ids, trigger keys, and history status semantics.
- Subbundle 03 depends on target kind/run correlation contracts.
- Subbundle 04 depends on query DTOs and validation result shape.
- Weak typing here will leak stringly-typed target behavior into every later phase.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inspect existing module project structure and create the smallest matching SchedulerPlanner project.
2. Add project references to Automation, Processes, AgentFramework/Core as needed, Infrastructure, SharedKernel, and BaseLib only when required.
3. Add marker type, composition project reference, module assembly registration, and runtime service extension registration.
4. Define schedule target/run status types and central owner/trigger key generation constants.
5. Add `SchedulerPlan` and `SchedulerPlanRun` entities and EF configuration with unique constraints for trigger keys and dedupe keys.
6. Add scheduler service contracts and DTOs for schedule creation, updates, list/detail, and history search.
7. Implement CRON validation and description service through a package adapter; verify Quartz-style CRON compatibility.
8. Add integration tests for persistence, validation failures, CRON description, and history query filters.
9. Update `reviews/01-execution-report.md` with package choice, commands, and residual risk.

## Scope Exceptions

- Do not configure Quartz persistent store in this subbundle.
- Do not launch processes or workflows in this subbundle.
- Do not build the full page in this subbundle.

## Do Not Do

- Do not put process/workflow launch dependencies into `CanDoItAll.Modules.Automation`.
- Do not store target semantics only in JSON payloads.
- Do not silently accept invalid CRON or unknown time zones.
- Do not add XML documentation comments.

## Acceptance Checklist

- SchedulerPlanner project compiles.
- EF model includes SchedulerPlanner entities.
- Schedule creation rejects invalid CRON, invalid time zone, missing target, and duplicate trigger key.
- CRON description is persisted and can be recomputed deterministically.
- History query can filter by date range, target kind, target id, status, and search text.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Targeted integration tests covering SchedulerPlanner persistence and CRON description.
- Execution report entries with package versions and migration/schema notes.

## Browser Validation Logging

- N/A. This subbundle does not add browser-visible UI.

## Progression Gate

- Downstream subbundles may continue only after schedule persistence, validation, CRON description, and query contracts pass tests and compile without introducing Automation-to-product module dependencies.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Create the SchedulerPlanner domain/persistence foundation with typed contracts and CRON description support. Do not configure Quartz persistence, launch process/workflow targets, or build UI yet. Capture build/test proof and update the execution report.
```
