# Structured Input

## Objectives

- O1: Replace the primary Templates tab with a button on the Workflows tab.
- O2: Load the workflow template pack only after the template catalogue dialog opens.
- O3: Present template basics in the catalogue dialog with Preview actions.
- O4: Present a read-only canvas preview dialog for the selected template.
- O5: Add selected templates to user drafts with deterministic conflict prefixes.
- O6: Rename/debrand SEAMARK-specific workflow templates into generic offer-analysis examples.
- O7: Validate implementation against generated design proposals using large-screen screenshots.

## Hard Constraints

- Use existing component library primitives before custom layout wrappers.
- Do not introduce a fallback that silently hides template-loading errors; report errors explicitly in the dialog.
- Do not load templates during Workflows page initialization, refresh, or non-template tab changes.
- Do not keep SEAMARK or equivalent company-specific wording in generic workflow template names, descriptions, node labels, output asset titles, or routing instructions.
- Skip small and medium viewport proof.

## Assumptions

- The catalogue and preview are the only new dialogs required by the request.
- "Same workflow already there" means an existing workflow definition already uses the same base template name, ignoring a leading two-digit numeric prefix.
- The first draft keeps the template name as-is; subsequent drafts use `01 <name>`, `02 <name>`, and so on.

## Validation Expectations

- Component tests cover the UI state transitions and draft naming.
- Template loader/unit tests cover SEAMARK removal and generic naming.
- Large-screen Playwright proof captures both open dialogs and records comparison notes against `bundle://evidence/design/*.png`.
