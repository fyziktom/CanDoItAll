# CanDoItAll.Modules.Projects.Contracts

## Purpose

The narrow slice of Projects-owned public contracts that lightweight consumers need without the
Projects implementation: the project lifetime admission (`ProjectWriteAdmission` and its rejection
exception), `ProjectAssignmentReference` and `ProjectAssignmentAdmission`, the party assignment
detail, upsert request, affiliation context, role, party-type and rate-unit contracts,
`ProjectNodeDetails` and `ProjectNodeReference`, `ProjectStatus`, and the project record query
contracts with the `IProjectRecordQueryService` read port. The types keep the
`CanDoItAll.Modules.Projects` namespace; only the declaring assembly moved.

Everything else (entities, the admission service, bridges, query implementations) stays in
[CanDoItAll.Modules.Projects](../CanDoItAll.Modules.Projects/README.md), which references this
assembly.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`

Validation command, from the repository root:

    dotnet build src/Modules/CanDoItAll.Modules.Projects.Contracts/CanDoItAll.Modules.Projects.Contracts.csproj

## Dependencies

The authoritative dependency list is in
[CanDoItAll.Modules.Projects.Contracts.csproj](CanDoItAll.Modules.Projects.Contracts.csproj). It
references only `CanDoItAll.SharedKernel`.

## Architecture Notes

The slice exists because the CRM / HR rendering library renders project assignments, staffing and
opportunity conversion on these contracts; see
[CRM / HR UI decoupling: completion record](../../../docs/architecture/crm-hr-ui-completion.md).
A project admission is still issued only by the Projects admission service; the record type alone
grants nothing.
