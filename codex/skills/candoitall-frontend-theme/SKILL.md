---
name: candoitall-frontend-theme
description: "Use when setting, overriding, or refactoring a custom CanDoItAll frontend theme or semantic UI density for BaseLib-driven apps. Covers ThemeHost and CadThemes, Tailwind-owned color, shape, shadow, and spacing tokens, downstream CSS-variable overrides, runtime light-dark switching, and the cda-* component plus cad-* infrastructure naming contract."
---

# CanDoItAll Frontend Theme

Use this when the task is about branding, theme overrides, dark mode, semantic color cleanup, or custom styling for a frontend that uses `CanDoItAll.Components.BaseLib`.

## Choose The Right Path

1. If the task changes the shared contract for every BaseLib consumer, edit the Tailwind sources in this repo.
2. If the task only changes one consuming app, keep BaseLib unchanged and override the shipped CSS variables in the app's own stylesheet.

## Core Rules

- Tailwind is the source of truth for the shared contract. Do not hand-edit `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.
- Prefer semantic tokens first: `--cad-color-*`, `--cad-tone-*`, `--cad-radius-*`, `--cad-shadow-*`, `--cad-space-*`.
- Keep public tone semantics descriptive. Do not introduce shorthand names such as `prim`, `sec`, or `dan`.
- Keep the current naming split stable:
  - `cad-*` for theme host, token infrastructure, and stabilized wrapper surfaces
  - `cda-*` for the existing semantic component family
  - do not introduce new shared non-canvas `zy-*` selectors
- If a page needs a new visual treatment, add or extend tokens before hard-coding colors on the page.

## Density Rules

- Treat compact as task-first composition, not compressed typography or controls. Read [../candoitall-components-mcp/references/compact-ui-composition.md](../candoitall-components-mcp/references/compact-ui-composition.md) before changing page density.
- Use semantic spacing tokens for repeated component rhythm. Remove duplicated wrapper padding and default semantic-element margins at the component boundary instead of applying blanket page resets.
- Preserve whitespace that communicates hierarchy and grouping. Do not reduce control usability, readability, or required open-overlay space to meet a density goal.
- Fix composition before token values: supporting metrics, stacked editors, and duplicate introductions should not consume permanent height when a badge, strip, dialog, or tab is the honest interaction.

## Shared Contract Workflow

1. Read `references/token-contract.md` to locate the existing token families and the files that own them.
2. Change the shared token definitions in `Tailwind/foundation/theme.css`.
3. Update shared component families in `Tailwind/` or BaseLib components only if the current token set cannot express the required UI.
4. Rebuild Tailwind so `wwwroot/css/output.css` is regenerated from source.
5. Validate on a real route, including runtime theme switching when the task changes theme behavior.

## Consumer Override Workflow

1. Read `references/consumer-overrides.md`.
2. Ensure the consuming app wraps the relevant layout or page shell in `ThemeHost`.
3. Load an app-owned stylesheet after BaseLib styles.
4. Override semantic `--cad-*` variables under the chosen `data-cad-theme` scope.
5. If the custom theme key is not `light` or `dark`, pass `ColorScheme` explicitly when native browser controls should still render with the correct scheme.
6. Validate the app on a real route instead of trusting the CSS diff.

## Decision Rule

- If changing one brand color should update buttons, alerts, badges, cards, and text accents consistently, the change belongs in semantic tokens.
- If only one feature screen needs a unique layout treatment, prefer existing shared components plus local composition instead of forking the theme contract.
- If a desired style cannot be described with the existing token families, extend the contract in Tailwind first and only then update component selectors.

## Validation Rule

- Verify at least one large-screen route.
- When theme switching is involved, prove the switch on the same rendered surface during the same session.
- Check readability, contrast, spacing, and whether the app is still using shared components rather than ad hoc markup.
- For density changes, verify the primary task in the first viewport, the intended scroll owner, and a screenshot with each relevant overlay open.
- For affected compound controls, prove that layout responds to the immediate containing width by rendering the control inside realistic narrow grid, card, rail, or dialog columns even at the target wide desktop viewport.

## References

- Read `references/token-contract.md` when editing the shared CanDoItAll theme contract.
- Read `references/consumer-overrides.md` when overriding the shipped theme from a downstream app or when wiring runtime theme selection.
- Read [../candoitall-components-mcp/references/compact-ui-composition.md](../candoitall-components-mcp/references/compact-ui-composition.md) for page, metric, card, form, dialog, and proof decisions; keep those details out of this skill.
