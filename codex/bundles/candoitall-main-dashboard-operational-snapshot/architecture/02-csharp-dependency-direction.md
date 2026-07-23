# C# Dependency Direction

## Current And Target References

The Web project already references every affected product boundary: AppComponents, Projects module, AgentFramework module/workflow abstractions, Process Application, and Process Projections. Target project references are identical to current references.

```text
CanDoItAll.Web
  -> CanDoItAll.AppComponents
  -> Projects module public query
  -> Workflow Abstractions query contract
  -> Processes Application query contract
  -> AgentFramework Core/module public query

Implementations -> existing stores/EF/projection/file boundaries
Contracts       -X-> implementation projects
AppComponents   -X-> Web or domain modules
Home            -X-> EF/store/full overview services
```

## Forbidden References

- AppComponents to Web, Projects, AgentFramework, or Processes.
- Workflow Abstractions to Core, Runtime, persistence, or module UI.
- AgentFramework Core contract to module UI.
- Process Projections/Core to Web.
- Any new reference used only to make Home reach an implementation type.

## Cycle And New-Project Decision

- New contract project: none; existing public contract/application projects are adequate.
- Expected cycle risk: low if contracts stay in existing outward-facing projects; high if AppComponents receives dashboard domain DTOs or Workflow Abstractions references persistence.
- CodeAnalytics cycle result: unavailable. SB03 must retry or record `dotnet list ... reference`/`.csproj` before-after proof plus successful solution build.

## Required Proof

- `git diff -- '*.csproj' '*.props' '*.targets'` shows no feature-caused project/package reference change.
- `dotnet build CanDoItAll.slnx --no-restore -nologo -v:minimal` succeeds.
- Source search proves `IServiceProvider` resolution is confined to `DashboardSnapshotLoadRunner`, and proves no provider/direct store/context in Home, query services, loader composition, or cache policy.
- Direct tests instantiate each extracted query without constructing Home or a broad facade.
