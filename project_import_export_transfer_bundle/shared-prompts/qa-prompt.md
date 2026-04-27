# QA Prompt

Validate the shipped work against the raw notes, not only the code diff.

Required checks:

- Transfer all projects from one profile to another via `DatabaseTransferService`.
- Export all projects to a zip package and import that package into an empty target profile.
- Confirm project cards, hierarchy links, structure nodes, object links, bindings, references, view state, and layout overrides survive both modes.
- Confirm existing transfer groups for processes, agents, providers, and ProjectStructure MCP settings still preview normally.
- Run browser proof on the existing data-source transfer UI and `/projects` zip UI.

Visual questions for UI proof:

- Is every label readable without zooming?
- Are controls aligned with the existing operational UI style?
- Do buttons and inputs fit on desktop and narrower viewports?
- Are busy/message states visible and not overlapping content?
