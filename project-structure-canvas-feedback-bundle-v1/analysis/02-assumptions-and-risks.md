# Assumptions And Risks

## Assumptions

- Record the assumptions made during bundle preparation.

## Critical Path Risks

- Identify the subbundles that unlock later work and the regressions that would force rework if they are wrong.

## Validation Risks

- Record where proof may be weak, blocked, environment-dependent, or expensive to reproduce.

## Reopen Triggers

- List the conditions that must reopen an earlier subbundle instead of letting later work continue.
# Assumptions And Risks

## Working Assumptions

- The existing project-structure route and smoke-test harness remain the canonical surface for browser proof.
- The new color architecture should extend existing node preset resolution rather than introduce a parallel styling registry outside the workbench module.
- Common block type changes will initially target the standard catalog-backed block families the user called out, not arbitrary unsupported object kinds.
- Note-to-block conversion will derive the destination block title from the first meaningful line of note text and preserve the full original note content in the converted block body or notes field.
- Copying the subtree id structure will use a deterministic root-first text format that preserves hierarchy shape.

## Critical Path Risks

- If the visual preset architecture is not unified first, later subbundles will either duplicate palette logic again or block on a refactor.
- Cut and paste of hierarchical selections crosses JavaScript keyboard handling, clipboard serialization, server-side mutation, and canvas refresh behavior, so partial implementation can corrupt project structure state.
- Subtree-to-subproject transfer may require new service behavior inside the projects module rather than a page-only orchestration change.
- Type mutation and note conversion can easily lose metadata if the transformation rules are not explicit and strongly typed.

## Validation Risks

- Canvas interactions are timing-sensitive, so Playwright proof must use stable waits and screenshot review instead of brittle click sequences alone.
- Visual assertions can produce false confidence if tests only inspect palette keys and never confirm rendered styles.
- Clipboard flows may behave differently in the browser runtime than in component tests, especially when descendant selection and keyboard shortcuts are involved.
- Multiline note editing must be validated both while editing and after save, otherwise newline handling bugs can hide in the display layer.

## Reopen Triggers

- Reopen any subbundle if a later Playwright pass shows color collisions, unreadable contrast, or missing semantic distinction between node categories.
- Reopen clipboard or hierarchy work if paste, cut, or subtree transfer drops descendants, duplicates nodes incorrectly, or breaks parent-child structure.
- Reopen note work if `Shift+Enter` is not stable, if plain `Enter` becomes ambiguous, or if conversion loses meaningful note content.
- Reopen closure if screenshots are missing, if execution report rows remain pending, or if any raw note can only be argued closed through reasoning instead of shipped proof.
