# Assumptions And Risks

## Working Assumptions

- Agent teams are durable AgentFramework catalog records, not CRM-HR organization units.
- The organization workspace catalog is the correct storage boundary because teams are part of technical agent management.
- Process HR matching should prefer the selected team but preserve role completion by selecting out-of-team candidates when needed.
- "Matching modal" can be satisfied by an explicit HR matching dialog launched from the launch-plan detail surface, with visible post-match candidate markers in the role candidate matrix.

## Critical Path Risks

- If team membership is modeled on `AgentDefinition.Tags`, team identity and rename/delete semantics will be weak; use structured catalog records instead.
- If selected-team matching fully excludes out-of-team candidates, required roles could remain unresolved contrary to the architect note.
- If process matching requires new persisted process columns, migrations for SQLite and PostgreSQL would expand scope; prefer candidate metadata and plan summaries unless a hard persistence gap appears.
- If the agent catalog normalization drops unknown or new catalog properties, teams could disappear on save; update normalization and invariant validation together.

## Validation Risks

- Component tests may pass while browser layout clips the team tree or modal card grid, so Playwright proof is required.
- HR matching semantics depend on AgentFramework-to-CRM-HR projection; tests must create or use bound technical agents rather than unprojected catalog-only agents.
- Metadata markers must survive reloading `GetLaunchPlanAsync`, otherwise the UI cannot mark out-of-team candidates after match completion.

## Reopen Triggers

- Reopen subbundle 01 if team records are not preserved through catalog save/load, import, seed normalization, or agent deletion.
- Reopen subbundle 02 if the tree does not filter by team, if membership selection is single-select only, or if one agent cannot appear under multiple teams.
- Reopen subbundle 03 if selected-team matching hides required roles, fails to mark out-of-team selections, or changes existing launch candidate scoring for no-team flows.
- Reopen subbundle 04 if browser proof cannot show the tree, the membership modal open state, and process HR matching team selection.
