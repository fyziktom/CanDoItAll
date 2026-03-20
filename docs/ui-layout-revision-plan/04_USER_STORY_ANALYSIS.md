# User Story Analysis

## Working Assumption

This product behaves like a local delivery workbench used by one person wearing multiple hats rather than a large multi-tenant SaaS app.

That same person may move through planning, content assembly, validation, testing, and environment setup in one session.

## Major User Archetypes

### 1. Delivery Lead / Project Architect

Primary screens:

- Dashboard
- Projects
- Project Structure
- Project Calendar
- Resources

Primary questions:

- What am I working on now?
- What project context is missing?
- What phase am I in?
- What artifacts should exist next?

What matters first:

- current project
- current phase
- missing setup
- next task entry points

### 2. Prompt Engineer / AI Operator

Primary screens:

- Prompt Gallery
- Prompt Factory
- Resources
- Settings

Primary questions:

- Which prompt artifacts already exist?
- What prompt context should I assemble?
- What project resources and blocks should feed the prompt?
- Which provider profile is active and trustworthy?

What matters first:

- selected project
- selected blueprint/template
- available blocks/resources
- build/save/send actions

### 3. Quality Reviewer / QA Lead

Primary screens:

- Validation Center
- Test Lab
- Project Calendar
- Activity

Primary questions:

- What is being reviewed?
- What evidence exists?
- What findings matter most?
- What still needs approval or follow-up?

What matters first:

- selected artifact
- severity/decision summary
- open findings
- evidence and last run status

### 4. Workspace Admin / Integration Maintainer

Primary screens:

- Settings
- Resources
- Automation
- Activity

Primary questions:

- Are providers healthy?
- Are secrets and resources correctly linked?
- What background jobs failed?
- Is the workspace configured well enough for the next prompt/test cycle?

What matters first:

- current environment status
- provider health
- secret/resource linkage
- failures requiring intervention

### 5. Operational Observer

Primary screens:

- Dashboard
- Activity
- Automation

Primary questions:

- What changed recently?
- Which work surfaces are active?
- Are background operations succeeding?

What matters first:

- recent actions
- current work items
- failures or blocked work

## Jobs To Be Done By Screen Category

### Dashboard

Job:

- help me resume work quickly and choose the next meaningful task

Current mismatch:

- it explains the system more than it starts the workflow

### Project Setup / Planning

Job:

- help me create and refine a project with enough structure to move directly into execution

Current mismatch:

- the wizard exists, but the page surrounding it does not clearly frame list selection, progress, or next steps

### Resource Registry

Job:

- help me register the right project assets and connectors without ambiguity

Current mismatch:

- the dynamic editor is powerful, but the page makes the user do too much parsing alone

### Prompt Library

Job:

- help me find, compare, and maintain reusable prompt artifacts

Current mismatch:

- versions and usage exist, but they are not staged in a way that helps comparison or quick scanning

### Prompt Workbench

Job:

- help me assemble, govern, and send a prompt session without losing context

Current mismatch:

- the workbench itself is strong, but the surrounding shell still competes for attention

### Validation / Testing

Job:

- help me review artifacts deterministically, record results, and decide what happens next

Current mismatch:

- the pages store the right information, but the layout does not prioritize summary, findings, and action resolution clearly enough

### Activity / Automation

Job:

- help me understand recent work and long-running operational state

Current mismatch:

- useful data exists, but the pages are light on scanning, grouping, and filtering affordances

### Settings

Job:

- help me configure the workspace safely without mixing unrelated admin tasks

Current mismatch:

- workspace defaults, secrets, and providers are all on one long page with limited separation

## Pain Reduction Opportunities

1. reduce repeated page introductions and move users to action faster
2. make standard management pages visibly consistent
3. expose selected state clearly in every list/detail layout
4. make primary actions predictable at the page header or sticky footer
5. keep power-user density, but group it into sections with readable rhythm
6. give protected workbench routes a quieter, focus-oriented shell mode

