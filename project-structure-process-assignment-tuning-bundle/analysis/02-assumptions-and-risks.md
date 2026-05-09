# Assumptions And Risks

## Assumptions

- `All` mode means summary/review mode and should be selected by default when the staffing dialog opens.
- "Main candidate" means the currently selected candidate when one exists; otherwise the highest-scored resolvable candidate.
- Tools can be represented from non-skill agent capability assignments, with MCP server capability names included as tools.
- Skills can be represented from `CapabilityKind.Skill` agent capability assignments.
- A concise readonly details dialog is acceptable if it mirrors the useful agent settings sections without enabling edits.

## Critical Path Risks

- If candidate enrichment is loaded after the launch plan maps to UI state, badges will look empty until the next reload. Refresh metadata before mapping or remap after metadata load.
- If the role drilldown replaces the summary grid without an `All` mode, the user loses the requested summary-review separation.

## Validation Risks

- Some live HR launch plans may contain only one candidate per role. Component tests must cover multiple-candidate ordering even if browser proof has fewer live candidates.
- Provider loading should not block the assignment modal. If providers fail to load, model tooltip falls back to the candidate or agent model.
- Tooltips are hover/focus driven and can be hard to capture reliably. Browser proof must include at least one open tooltip screenshot or proof JSON state.

## Reopen Triggers

- Browser screenshot shows the dialog body not using the available fullscreen width.
- Clicking a role still shows all roles instead of the role-specific candidate ranking.
- Plus-card fails to open the all-agent picker.
- Metadata badges are absent from summary or role candidate cards.
- Readonly details dialog shows editable settings fields or cannot open above the fullscreen overlay.
