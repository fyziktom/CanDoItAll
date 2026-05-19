# Structured Input

## UI Scope

- Module route: `/cognitive-memory` and `/memory`.
- Existing tabs: Dashboard, Probe workbench, Settings, Sources, Memory, Review queue, Recall traces, Health, Self-regulation, Scale.
- Required new access: quality diagnostics, cluster planning, dream consolidation, aggregate candidate inspection, aggregate apply path, synthesized recall/reference evidence visibility.
- Required large-list behavior: page at service query level and show explicit pager controls in the UI.

## Hard Rules

- Large-screen only.
- Do not tune medium or small screens.
- Do not add medium/small media queries.
- Do not load all rows before taking a page.
- Do not use generated image artifacts as proof that the shipped UI works.

## Imagegen Proposal Prompts

Prompt 1 produced `proposal-overview.png`: large-screen Cognitive Memory desktop operator workspace with dense tab panes, explicit pagination, and no marketing hero.

Prompt 2 produced `proposal-tabs.png`: tab-by-tab large-screen proposal sheet covering Dashboard, Probe workbench, Quality operations, Sources, Memory, Review queue, Recall traces, Health, Self-regulation, Scale, and Settings.
