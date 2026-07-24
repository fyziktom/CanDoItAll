# C# Dependency Direction

## Current Project References

Relevant inspected direction:

```text
CanDoItAll.Web
  -> CanDoItAll.AppComponents
  -> CanDoItAll.Modules.CrmHr
  -> CanDoItAll.Modules.Projects

CanDoItAll.Modules.CrmHr
  -> CanDoItAll.Modules.Projects
  -> CanDoItAll.Modules.Workspace
  -> Foundation/Infrastructure/SharedKernel
  -> BaseLib/Gantt packages

CanDoItAll.Modules.Projects
  -> CanDoItAll.AppComponents

CanDoItAll.AppComponents
  -> SharedKernel
  -> BaseLib/Common/CanvasLib
  -> FileTools abstractions
```

The visual indentation above summarizes relevant direct references; it is not an automated transitive graph. CodeAnalytics cycle proof was unavailable.

## Target Project References

```text
CanDoItAll.Web
  -> modules and AppComponents for composition

CanDoItAll.Modules.CrmHr
  -> CanDoItAll.AppComponents
  -> CanDoItAll.Modules.Projects
  -> existing lower-level dependencies
  -> BaseLib/Gantt/Charts packages

CanDoItAll.Modules.Projects
  -> CanDoItAll.AppComponents

CanDoItAll.AppComponents
  -> SharedKernel and component packages only
```

The new `CrmHr -> AppComponents` edge points toward a domain-neutral UI project and should not create a cycle because AppComponents does not reference modules. This must be verified before SB01 closes.

## Forbidden References

- `CanDoItAll.AppComponents -> CanDoItAll.Modules.CrmHr`
- `CanDoItAll.AppComponents -> CanDoItAll.Modules.Projects`
- `CanDoItAll.Modules.Projects -> CanDoItAll.Modules.CrmHr`
- `SharedKernel -> UI or module projects`
- `CrmHr core/query logic -> CanDoItAll.Web`
- `Financial projection -> chart component types`
- Any new circular module/UI reference introduced to reuse a component.

## Cycle Risk

- Projects already references AppComponents; adding CRM/HR -> AppComponents is directionally parallel and should remain acyclic.
- CRM/HR already references Projects, so Projects must not host CRM-specific adapters or reference CRM/HR.
- CRM/HR can expose a typed static route catalog without referencing Web; Web already references the CRM/HR module and consumes the catalog during descriptor construction.
- A shared filter contract must not import `PartyType`, `OpportunityStage`, or Project models. Use the generic filter type at the composition boundary.

## New Contract Projects

- None planned. Existing AppComponents and SharedKernel boundaries are sufficient.
- Stop and redesign before adding a new project if the proposed extraction would otherwise create a cycle; do not add the cycle.
- A new contract project is justified only if execution proves a stable contract must be shared by AppComponents and multiple domain modules and cannot remain generic.

## Required Before/After Proof

- Record relevant `<ProjectReference>` lines before and after SB01/SB04.
- Run `dotnet list <affected.csproj> reference` or equivalent non-mutating project-reference inspection.
- Run `dotnet build CanDoItAll.slnx --no-restore`.
- If CodeAnalytics transport is restored, capture a scoped dependency snapshot and cycle result for AppComponents, CRM/HR, Projects, SharedKernel, and Web. If unavailable, explicitly record `Unavailable`; do not claim automated cycle proof.
- Source assertion: no AppComponents file imports CRM/HR or Projects namespaces.
- Source assertion: no new feature partial class is added.
- Unit tests instantiate the record-browser loader/query and financial projection without constructing the old large services/pages.
