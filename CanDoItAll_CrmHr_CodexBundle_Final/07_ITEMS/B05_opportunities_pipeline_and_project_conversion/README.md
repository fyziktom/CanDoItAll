# B05 — Opportunities, pipeline, stage history, and project conversion

## Purpose

Build the opportunity board, structured stage progression, stage history, partner-sourced deals, lost reasons, and conversion of won opportunities into CanDoItAll project context.

## Dependencies

B01, B02, B03, B04, B10

## Main stories covered

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

## Main routes

- `/crm-hr/crm`

## Execution status

- Implemented on `2026-04-03` after reconciling the preserved architect package with newer CRM/HR home behavior, B10 project-party integration, and the current opportunity editor structure.
- `/crm-hr/crm` now contains a real opportunity workspace with board columns, stage-aware editing, filters, partner-linked parties, lost-reason capture, stage history, and project conversion for won opportunities.
- The CRM service now persists richer opportunity context and converts won opportunities into project context without dropping party assignments.
- `/crm-hr` now surfaces open pipeline preview items only; closure browser proof reflects that live contract by keeping a dedicated open opportunity for the home preview.
- Closure proof is recorded in `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\reviews\01-execution-report.md` and browser evidence is stored under `C:\repositories\CanDoItAll\evidence\crm-hr\b05\`.

## Done when

- Opportunities can move across stages and stage history is recorded.
- Won opportunity conversion creates or links a project and keeps party context.
- Lost opportunities keep loss reason and are still historically visible.
- Pipeline UI is readable and validated with screenshots.
