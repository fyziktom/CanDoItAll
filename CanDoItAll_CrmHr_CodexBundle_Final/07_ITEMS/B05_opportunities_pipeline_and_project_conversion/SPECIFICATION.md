# Specification

## Objective

Build the opportunity board, structured stage progression, stage history, partner-sourced deals, lost reasons, and conversion of won opportunities into CanDoItAll project context.

## Scope

- Implement opportunities with stage history, value, probability, expected close, and source.
- Provide a BaseLib-friendly board or grouped-column visualization by stage.
- Add lost reasons and partner-sourced deal handling.
- Implement conversion to project with party linkage preservation.

## Services and entities involved

**Services**

- `CrmService`
- `ProjectPartyIntegrationService`
- `ProjectsService`

**Entities / concepts**

- `Opportunity`
- `OpportunityStageHistory`
- `OpportunityPartyLink`

## Bundle-specific implementation notes

1. Follow the global architecture documents first.
2. Keep the module inside `CanDoItAll.Modules.CrmHr` unless the file reference list explicitly points to another module for integration changes.
3. Reuse the existing CanDoItAll services listed in `FILE_REFERENCES.md` instead of inventing parallel registries or orchestration layers.
4. Keep database changes additive and backward compatible where Workbench or existing modules already persist data.
5. Any UI added here must stay inside BaseLib + normal Razor patterns.

## Detailed functional outcomes

- **CRM-06** As a sales director, I can maintain an opportunity with stage, value, probability, and expected close date so forecast conversations have structured data.
- **CRM-07** As a pre-sales lead, I can link one opportunity to multiple parties such as customer, partner, internal sponsor, and delivery unit so pursuit structure is explicit.
- **CRM-08** As a sales director, I can move opportunities through a pipeline so teams have a common operating model.
- **CRM-09** As an account executive, I can record lost reason and competitor context when an opportunity closes unsuccessfully so the business can learn.
- **CRM-10** As an account manager, I can convert a won opportunity into a project context without retyping customer, partner, and delivery unit data so handoff is fast and accurate.
- **CRM-13** As a partnership manager, I can mark partner-sourced opportunities and partner contribution so channel business is visible.
- **CRM-15** As a sales operations analyst, I can filter opportunities by stage, owner, delivery unit, partner, and customer so the pipeline is explorable.
- **CRM-16** As an account manager, I can maintain renewal and upsell opportunities separately from net-new work so account growth is visible.
- **CRM-18** As a business director, I can see account summaries and open opportunities from the CRM/HR home screen so I do not have to reconstruct pipeline from projects.
- **CRM-24** As a sales director, I can view stage history and recent movement on opportunities so forecast quality and stagnation are visible.
- **PRJ-11** As a sales lead, I can convert an opportunity to a project while preserving linked parties and history so handoff does not fragment data.

## Out of scope inside this bundle

- Bundles that are listed as dependencies but handled elsewhere stay out of this bundle.
- Do not prematurely solve later-wave concerns unless the dependency chain requires a small seam.
- Do not introduce payroll, marketing automation, or canvas-based UI work here.

## Definition of success

- Opportunities can move across stages and stage history is recorded.
- Won opportunity conversion creates or links a project and keeps party context.
- Lost opportunities keep loss reason and are still historically visible.
- Pipeline UI is readable and validated with screenshots.
