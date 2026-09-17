# CanDoItAll.Modules.CrmHr.Contracts

## Purpose

Feature-owned public contracts of the CRM / HR module: the party, CRM, workforce, recruiting,
staffing and agent-directory enums; the workspace, list, page and query records; the editor
input models the application services accept; the narrow read ports the catalog browsers and
pickers inject (`IPartyRecordQueryService`, `IWorkforceRecordQueryService`,
`IOpportunityPipelineQueryService`, `IAiAgentDirectoryQueryService`,
`IPartyOrganizationAffiliationReader` and the affiliation command port that extends it); the pure
`WorkforceRecordClassificationPolicy` and `RecruitmentConversionPolicy`; and
`CrmHrRouteCatalog`. The types keep the `CanDoItAll.Modules.CrmHr` namespace, so existing
consumers compile unchanged and JSON payloads are identical; only the declaring assembly moved
out of the module.

Entities, EF configuration, services, bridges, audit writers and agent-context builders stay in
[CanDoItAll.Modules.CrmHr](../CanDoItAll.Modules.CrmHr/README.md).

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`

Validation command, from the repository root:

    dotnet build src/Modules/CanDoItAll.Modules.CrmHr.Contracts/CanDoItAll.Modules.CrmHr.Contracts.csproj

## Dependencies

The authoritative dependency list is in
[CanDoItAll.Modules.CrmHr.Contracts.csproj](CanDoItAll.Modules.CrmHr.Contracts.csproj). It
references `CanDoItAll.SharedKernel`, the
[Projects contracts](../CanDoItAll.Modules.Projects.Contracts/README.md) (project admissions carried
by staffing and conversion inputs) and `CanDoItAll.AgentFramework.Models` (the technical agent and
recruiting evidence models the agent directory projects). It must not reference Infrastructure,
persistence, Web or any module implementation; `CrmHrUiModuleBoundaryTests` guards that.

## Architecture Notes

The editor input models are mutable form inputs, not backend editors. A host gives an editor its
own draft and submits an independent copy; the application service remains the only validator and
writer. The boundary decisions are recorded in
[CRM / HR UI decoupling: completion record](../../../docs/architecture/crm-hr-ui-completion.md).
