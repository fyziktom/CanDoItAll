# Implementation prompt

Implement **B05 — Opportunities, pipeline, stage history, and project conversion** for CanDoItAll.

## Bundle goal

Build the opportunity board, structured stage progression, stage history, partner-sourced deals, lost reasons, and conversion of won opportunities into CanDoItAll project context.

## Hard rules

- follow `03_ARCHITECTURE/*` and `02_REQUIREMENTS/SCOPE_AND_NON_FUNCTIONAL_DECISIONS.md`
- keep UI in BaseLib / Razor / HTML only
- do not introduce canvas components
- preserve backward compatibility for existing project/workbench flows where relevant
- add or update tests listed in `FILE_REFERENCES.md`
- add screenshot evidence requirements from `SCREENSHOT_REQUIREMENTS.md`

## Implementation steps

1. Inspect all files in `FILE_REFERENCES.md`.
2. Implement the data model / service changes required for this bundle.
3. Implement the route or UI changes required for this bundle.
4. Wire search/activity/integration seams if this bundle requires them.
5. Add automated tests at the correct level.
6. Execute browser validation and capture screenshots.
7. Write a concise evidence note summarizing code changes, tests, and screenshots.

## Bundle-specific targets

- Implement opportunities with stage history, value, probability, expected close, and source.
- Provide a BaseLib-friendly board or grouped-column visualization by stage.
- Add lost reasons and partner-sourced deal handling.
- Implement conversion to project with party linkage preservation.

## Stories that must be satisfied in this bundle

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

## Stop conditions

Do not mark this bundle done until all acceptance criteria pass and the screenshot evidence is semantically reviewed.
