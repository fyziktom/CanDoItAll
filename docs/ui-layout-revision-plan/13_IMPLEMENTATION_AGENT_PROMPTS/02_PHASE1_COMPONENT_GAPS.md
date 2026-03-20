# Prompt: Phase 1 Component Gaps

You are adding the minimum shared `ComponentKit` primitives required to stop page-level improvisation.

## Scope

- `CanDoItAll.ComponentKit`
- page-composition components only

## Read First

- `../07_COMPONENT_LIBRARY_GAP_ANALYSIS.md`
- `../08_LAYOUT_PATTERNS_AND_ASCII_SKETCHES.md`
- `../09_RECOMMENDED_DESIGN_RULES.md`

## Build Only What Phase 1 Needs

Prioritize:

- `PageScaffold`
- `ListDetailShell`
- `ListPanelHeader`
- `FormSection`
- `StickyActionFooter`
- `EmptyState`
- `FilterBar`
- small helpers such as summary or key-value blocks only if needed by the next page batches

## Rules

- add these to `CanDoItAll.ComponentKit`, not `CanDoItAll.Components`
- do not build a full dialog system
- do not build an advanced grid system
- keep the APIs narrow and page-composition oriented

## Expected Outputs

- new shared components with clear responsibilities
- enough styling and slots to migrate the first page batch without page-local wrappers
- no protected workbench changes

## Self-Check Before Finishing

- Can Projects and Resources migrate onto these components without inventing new page-local layout wrappers?
- Did you avoid overlay/platform scope creep?
- Did you keep the component ownership inside `ComponentKit`?

