# Codex task — PRM-F16

Implement **Role and agent templates, staffing briefs, and sourcing handoffs** inside the uploaded CanDoItAll solution.

## Constraints

- Treat CRM-HR as the canonical owner of reusable role / agent templates.
- Treat `CanDoItAll.Modules.Processes` as the canonical owner of process roles, references, snapshots, and runtime state.
- Do not create a second staffing or recruiting subsystem inside Processes.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- A process actor role can optionally reference a reusable manager-approved role/agent template instead of free-text only.
- Templates capture modality, required skills/capabilities, allocation intent, and fallback/supervisory expectations.
- HR can open staffing, recruiting, or AI-agent sourcing work from unresolved process role gaps without losing process context.
- Published process versions snapshot the selected template version and key requirement summary.
- Runs snapshot the resolved assignee, eligible pool/fallback metadata, and rebind reasons.
- AI-oriented templates still resolve through CRM-HR identities and future execution bridges rather than direct runtime coupling.

## Recommended first files to touch

- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrRecruitingServices.cs`
- `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrAssignmentsPage.razor`
- `src/CanDoItAll.Modules.CrmHr/Pages/CrmHrRoleTemplatesPage.razor (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorTemplateBridge.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorServices.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessDesignerPage.razor`
- `tests/CanDoItAll.Tests.Integration/ProcessesStaffingTemplateIntegrationTests.cs (new)`
