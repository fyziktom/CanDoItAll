# C# Dependency Direction

## Current relevant references

```text
CanDoItAll.Web
  -> CanDoItAll.Modules.Workbench
  -> CanDoItAll.Modules.AgentFramework

CanDoItAll.Modules.AgentFramework
  -> CanDoItAll.Modules.Workbench
  -> AgentFramework Core/Models/Persistence

CanDoItAll.Modules.Workbench
  -> Modules.Projects
  -> AgentFramework Core/Models/Workflow abstractions
  -> Processes abstractions/application
```

## Target references

The target graph is identical. No `.csproj` reference is added.

- Workbench declares the Project Structure strategy contract.
- AgentFramework implements the Agent strategy because dependency direction already points `AgentFramework -> Workbench`.
- Workbench must not reference `CanDoItAll.Modules.AgentFramework`.
- Projects owns the mutation-bridge contract; CrmHr implements its CRM-facing side and Workbench supplies the WorkItem metadata side. This avoids CrmHr-to-Workbench coupling while allowing one serializable transaction.

## Forbidden references

- Workbench to the AgentFramework module.
- Projects or CRM contracts to Workbench UI/application implementations.
- a shared/Common project introduced solely to evade a cycle.
- strategy selection through `IServiceProvider` or a service locator.
- CrmHr to Workbench for project-structure metadata mutation.

## Cycle risk

The main risk is calling `HrAgentUsageAnalyticsService` directly from Workbench, which would require the forbidden reverse reference. The Agent strategy implementation therefore lives in AgentFramework and is supplied through `IEnumerable<IProjectStructureTaskResourceCostStrategy>`.

## New contract projects

None. The contract is narrow, use-case-owned, and fits the existing Workbench project. A new project would be disproportionate.

## Required proof

- before/after relevant `.csproj` diff shows no new reference.
- affected project builds.
- DI smoke resolves exactly one strategy for every enum value in the full application/module registration.
- a negative dispatcher test rejects missing and duplicate kinds.
