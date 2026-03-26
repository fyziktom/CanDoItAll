# Tailwind And Style Generalization Map

## Shared Styling Ownership

- shared component styling remains owned by CanDoItAll
- shared CSS generation must not happen in Zyphonote
- `BaseLib` should prefer Tailwind utility composition and small family-scoped CSS files
- app-specific visual refinements stay in Zyphonote page CSS or Zyphonote-local component CSS

## Extraction Strategy By Component Shape

### Pure One-Class Wrappers

Examples:

- `BadgesGroup`
- `FormStack`
- `InlineActions`
- `ListGroup`
- `MetaList`
- `PlainList`
- `ToolbarActions`
- `ToolbarRow`
- `WorkspaceSplit`

Strategy:

- do not copy raw class names into `BaseLib`
- replace them with utility-first markup in the shared component itself
- if an existing `BaseLib` primitive already models the structure, retire the wrapper instead of recreating it

### Reusable Structure With App-Specific Refinements

Examples:

- `FactTable`
- `ListItem`
- `SheetCard`
- `Toolbar`
- `ToolbarFields`
- `ProfileField`
- `ProfileToggle`

Strategy:

- promote the base structure into `BaseLib`
- move shared spacing, border, background, and slot layout into Tailwind markup
- keep marketplace, seller-profile, or other page-specific refinements in Zyphonote-local CSS classes layered on the consuming page or domain component

### Existing `BaseLib` Legacy Debt To Avoid Expanding

Current issues already visible in `BaseLib`:

- `ButtonLook` contains legacy values such as `SheetCard` and `SheetCardGhost`
- `StatusBadge` uses a string tone instead of a typed enum
- `TextBlock` still maps some styles through legacy class names like `small`, `mono`, and `foot`

Bundle-2 rule:

- do not add more Zyphonote naming into shared enums
- add typed shared appearance models where the API is stable
- keep temporary compatibility adapters isolated and explicitly disposable

## CSS Files That Must Not Be Imported Into `BaseLib`

- `C:\repositories\Zyphonote\src\App.Blazor\wwwroot\zyphonote-compat.css`
- `C:\repositories\Zyphonote\src\App.Blazor\wwwroot\brand.css`
- `C:\repositories\Zyphonote\src\App.Server\wwwroot\css\server-shell.css`

## CSS Files That Should Be Mined Component By Component

- `C:\repositories\Zyphonote\src\App.Blazor\Components\FactTable.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\ListItem.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\SheetCard.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\Toolbar.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\ToolbarFields.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\ToolbarRow.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\ProfileField.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\Components\ProfileToggle.razor.css`

These files mostly describe app-level variants layered on top of generic structure. Extract only the stable base structure.

## Tailwind Guidance For Implementation Agents

- keep shared component markup readable
- do not force every advanced layout into unreadable inline utility soup
- use a small scoped CSS file only when it genuinely improves clarity
- keep shared tokens neutral; do not encode Zyphonote product semantics such as `seller-profile`, `sheet`, or `marketplace` into `BaseLib`
