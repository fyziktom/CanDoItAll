# C# Dependency Direction

## Current Project References

All selected sources are compiled by `CanDoItAll.Modules.Workbench.csproj`. It already references Projects and the required process/application/contracts projects.

## Target Direction

```text
ProjectStructurePage (UI orchestration)
  -> ProjectStructureProcessLaunchContextBuilder
  -> ProjectStructureSurface / ProjectStructureNode / graph conventions

ProjectStructureProcessNodeService (application adapter)
  -> ProjectStructureProcessLaunchContextBuilder

ProjectStructurePage (hierarchy dialog orchestration)
  -> ProjectStructureProjectHierarchySelectionPolicy
  -> ProjectHierarchyLinkSummary
```

## Forbidden Direction

- extracted types must not reference the page, Blazor component state, `IServiceProvider`, persistence, or external SDKs;
- contracts/models must not reference the page implementation;
- no new `.csproj` reference may be added;
- no canonical persistence mutation may enter either policy.

## Cycle Result

No project reference changes are planned, so the existing graph cannot gain a project cycle from this extraction. Direct `.csproj` inspection and a successful affected build are the closure proof.
