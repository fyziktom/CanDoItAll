# 03 Cards Lists Shells And Workspace Primitives

## Objective

Move the reusable card, list, page-shell, workspace, and metric surfaces into `BaseLib`, and collapse the Zyphonote wrapper debt around them.

## Component Set

- card and shell family:
  - `ActionCard`
  - `AuthCard`
  - `PanelCard`
  - `HeroCard`
  - `ParitySectionCard`
  - `SheetCard`
  - `SheetCardTop`
  - `SheetCardHeading`
  - `CardActions`
  - `CardButton`
  - `CardGrid`
  - `PageShell`
  - `WorkspacePanel`
  - `WorkspaceSplit`
  - `WorkspacePanelTone`
- list and display family:
  - `FactTable`
  - `ListGroup`
  - `ListItem`
  - `MetaList`
  - `PlainList`
  - `LegalToc`
  - `LegalTocNav`
- metric family:
  - `StatBox`
  - `StatsCardRow`
  - `StatsGrid`
  - `BuilderStatBox`
  - `BuilderStatStrip`
  - `CardStatsWithNumber`
  - `PriceBar`
  - `PriceRow`
  - `PriceBarTone`
- supporting shells:
  - `SheetGrid`
  - `SheetSection`
  - `SheetNote`

## Exact Source References

- `C:\repositories\Zyphonote\src\App.Blazor\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Card.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\SectionCard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\ListDetailShell.razor`
- `..\..\inventories\02-sharedization-matrix.md`
- `..\..\inventories\03-tailwind-and-style-generalization-map.md`

## Implementation Steps

1. Establish the shared card family and remove the need for multiple Zyphonote card wrappers.
2. Promote shared list and fact-display primitives.
3. Introduce reusable workspace and page-shell surfaces where the structure is stable across apps.
4. Create a shared metric-card family instead of preserving builder, stat, or price names.
5. Keep page-specific visual refinements in Zyphonote-local CSS layered on top of the shared structure.

## Hard Rules

- do not preserve `Sheet*`, `Builder*`, or `Price*` names in shared ownership unless the term is genuinely generic
- do not copy app-scoped list item CSS wholesale into `BaseLib`
- do not keep multiple card wrappers when a typed shared appearance model is cleaner

## Acceptance Checklist

- card and list surfaces live in `BaseLib`
- page-shell and workspace wrappers no longer depend on Zyphonote naming
- metric surfaces are shared and typed
- marketplace, dashboard, legal, and my scores pages still render correctly

## Proof Required

- build proof for both repos
- screenshots from `AccountMarketplace`, `AccountDashboard`, `AccountMyScores`, and `LegalPrivacy`
- ownership diff for the card and list families

## Suggested Agent Prompt

```text
Implement subbundle 03 only.

Move the reusable card, list, page-shell, workspace, and metric primitives from Zyphonote into BaseLib. Prefer a small number of strong shared families over many thin wrappers, and keep app-specific refinements outside the shared library.
```
