# Structured Input

## Core Objective

- State the primary outcome without implementation detail.

## Hard Constraints

- List the non-negotiables.

## Source Artifacts

- Reference the files, docs, screenshots, or prompts that define the task.

## Input Coverage Signals

- List each raw note or artifact that cannot be safely collapsed, merged, or deferred.

## Dependency And Sequencing Signals

- Note which requested outcomes obviously unlock or block later work.

## Validation Expectations

- Describe the proof required before implementation is complete.

## UI Validation Strategy

- If the task is UI-related, note the large-screen Playwright pass, screenshot review questions, and narrower-width follow-up plan.

## Browser Validation Analytics

- If the task is UI-related, note how each subbundle will log route, viewport, Playwright MCP actions, assertions, screenshot paths, and result.

## Working Assumptions

- Record the assumptions made during bundle preparation.

## Primary Risks

- Record the main delivery, UI, architecture, or regression risks.
# Structured Input

## Requested UX

- Replace the current baseline-seeding action with a templates browser action.
- Keep the user inside Process management instead of routing to a different page.
- Use a fullscreen modal rather than a narrow dialog.
- Use a left list panel and a right preview panel.
- Keep the modal open after successful imports.

## Requested Categories

- Process templates
- Role templates
- Artifact templates

## Requested Preview Capabilities

- Searchable card list in the left panel
- Category tabs in the left panel
- Tree view for the selected template structure
- Mermaid rendering with pan and zoom
- Markdown rendering
- Json rendering

## Requested Import Behaviors

- Import full process template into the current process library
- Import a role template into the current definition authoring flow
- Import an artifact template into the current definition authoring flow
- Import a role directly from a process preview without importing the whole process

## Known Domain Constraints

- Full process import already exists through `ProcessTemplateProjectionService` and `ProcessesService.ImportAsync`.
- There is no persisted standalone roles library entity in the current Processes module.
- There is no persisted standalone artifacts library entity in the current Processes module.
- Artifact expectations currently live on process steps, so artifact import must target a step in the current definition editor.
