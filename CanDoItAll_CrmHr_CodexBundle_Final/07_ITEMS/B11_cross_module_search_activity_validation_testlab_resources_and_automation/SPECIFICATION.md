# Specification

## Objective

Finish enterprise integration by indexing CRM/HR artifacts, writing activity events, linking owners to resources, validation, and tests, and wiring reminder-style automation jobs.

## Scope

- Write CRM/HR search documents and activity entries.
- Add owner/responsible-party links to Resources, Validation, and Test Lab where relevant.
- Expose reminder-style automation jobs for stale next actions and lifecycle tasks.
- Prove cross-module visibility.

## Services and entities involved

**Services**

- `PartyDirectoryService`
- `CrmService`
- `HrService`
- `AiAgentService`
- `ProjectPartyIntegrationService`
- `AutomationWorkspaceService`

**Entities / concepts**

- `SearchDocument`
- `ActivityEntry`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **DIR-14** As a project manager, I can see a party activity timeline so I understand the latest interactions, assignments, and changes before acting.
- **DIR-15** As an executive assistant, I can open a party directly from global search so the directory behaves as a first-class application surface.
- **CRM-20** As a commercial operations lead, I can receive reminders for overdue next actions so opportunities do not stall silently.
- **PRJ-12** As a quality lead, I can link validation runs and test plans to responsible parties so accountability is clear.
- **PRJ-13** As a resource owner, I can link resources to owning or maintaining parties so operational ownership is visible.
- **X-02** As a platform owner, I can index parties, interactions, opportunities, workforce records, and agent profiles in global search so the module is discoverable.
- **X-03** As a platform owner, I can write activity entries for major CRM/HR changes so the timeline reflects relationship work.
- **X-08** As a platform owner, I can seed default opportunity stages, relationship stages, and other lookup values so the module works immediately after startup.
- **X-15** As an automation owner, I can trigger reminders and onboarding follow-up jobs from CRM/HR data so the module participates in operational automation.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- CRM/HR entities appear in global search where safe.
- Major CRM/HR actions appear in Activity.
- Resources/Validation/Test Lab can reference responsible parties.
- Automation workspace can show CRM/HR reminder jobs or equivalent status.
