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

Workforce replacement uses complete original observations. Read the current workspace and
send `profile.original`, `skills[].original` or `capacityBlocks[].original` back unchanged
with the edited fields. Skill-definition updates send the original catalog item. An absent
identifier expresses creation; it never overwrites a matching name or worker/skill pair.
Unknown update identifiers, a row owned by another worker and stale originals are rejected.
The Web API returns HTTP 409 for stale workforce, skill and capacity conflicts. Deletes in
native UI services require the displayed row observation as well as its identifier.

`IWorkforceProfileEditing` is implemented by the native profile owner, independently of the
legacy HR facade. Native workforce owners freeze mutable input before awaiting and report
canonical accepted state immediately after commit, before notifications or projections.
The main UI retains later input and blocks replay when no receipt establishes an outcome.
Capacity related-project labels remain informational; a linked write also requires the
project admission captured at selection. A read never grants fresh authority for a prior
selection. Read-only allocation rows retain their existing project-lifetime semantics.

The editor input models are mutable form inputs, not backend editors. A host gives an editor its
own draft and submits an independent copy; the application service remains the only validator and
writer. The boundary decisions are recorded in
[CRM / HR UI decoupling: completion record](../../../docs/architecture/crm-hr-ui-completion.md).
