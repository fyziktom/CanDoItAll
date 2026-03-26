# Bundle 1 Gap Audit

## What Bundle 1 Got Right

- it established the right ownership direction: shared primitives belong in CanDoItAll
- it split `BaseLib`, `CanvasLib`, and app-specific libraries conceptually
- it correctly refused to copy `zyphonote-compat.css` into shared ownership
- it identified an initial wave of Zyphonote components that should move to `BaseLib`

## What The Real Filesystem Shows Now

### Wave-1 Zyphonote Components Still Missing From `BaseLib`

Bundle 1 wave-1 Zyphonote candidates:

- `Badge`
- `BadgesGroup`
- `Callout`
- `Divider`
- `EmptyState`
- `Eyebrow`
- `FactTable`
- `FormRow`
- `FormStack`
- `InlineActions`
- `ListGroup`
- `ListItem`
- `MetaList`
- `MonoText`
- `MutedInline`
- `PageHeader`
- `PageHeaderActions`
- `PageShell`
- `PanelCard`
- `Pill`
- `PillList`
- `PlainList`
- `SectionHead`
- `SectionHeading`
- `SmallText`
- `StatusChip`
- `Toolbar`
- `ToolbarActions`
- `ToolbarFields`
- `ToolbarRow`
- `WorkspacePanel`
- `WorkspaceSplit`

Current exact-name result in `BaseLib`:

- present: `EmptyState`, `PageHeader`
- still missing: every other component in that list

### Missing Professional Ownership Correction

Bundle 1 treated Zyphonote adoption mainly as an app-rewiring phase. The current problem is deeper:

- `Zyphonote.Components` still exposes nearly the whole `App.Blazor\Components` folder by wildcard
- many current library components are one-class wrappers and should not survive as permanent app-owned types
- some important migration candidates were left out entirely from bundle 1 even though they are generic:
  - `Avatar`
  - `CardActions`
  - `CardButton`
  - `CardGrid`
  - `FooterText`
  - `ImmersiveRibbonTabs`
  - `TagTextEdit`
  - `ZyWorkspaceModal`

## Bundle 2 Corrections

- classify every relevant component family by actual target owner and action type
- add `BaseLib` subfoldering and support-type colocation
- stop wildcard ownership in `Zyphonote.Components`
- explicitly distinguish:
  - promote to shared library
  - merge into an existing shared primitive
  - retire wrapper
  - keep local
  - move feature-local
