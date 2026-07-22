# Token Contract

## Source Of Truth

- Shared theme tokens live in `Tailwind/foundation/theme.css`.
- The generated output lives in `src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css`.
- Tailwind imports the shared theme stack from `Tailwind/input.css`.

Never edit `output.css` directly.

## Runtime Host

- `src/CanDoItAll.Components.BaseLib/Components/Layout/ThemeHost.razor`
- `src/CanDoItAll.Components.BaseLib/CadThemes.cs`

`ThemeHost` sets `data-cad-theme` on a wrapping element and applies `color-scheme` when it can resolve one.

`CadThemes` currently ships:

- `CadThemes.Light`
- `CadThemes.Dark`

`CadThemes.ResolveColorScheme` only resolves those two keys. For a custom key such as `brand-light`, pass `ColorScheme="light"` or `ColorScheme="dark"` yourself when needed.

## Token Families

### Page and surfaces

- `--cad-color-page-bg`
- `--cad-color-surface`
- `--cad-color-surface-soft`
- `--cad-color-border`
- `--cad-color-border-soft`

### Text

- `--cad-color-text-strong`
- `--cad-color-text`
- `--cad-color-text-muted`
- `--cad-color-text-soft`
- `--cad-color-inverse`

### Elevation and shape

- `--cad-radius-control`
- `--cad-radius-surface`
- `--cad-radius-panel`
- `--cad-shadow-soft`
- `--cad-shadow-strong`

### Semantic density

- `--cad-space-compact-gap` for related inline controls and metadata
- `--cad-space-content-gap` for content within one semantic region
- `--cad-space-section-gap` between page sections
- `--cad-space-surface-padding` for cards and panels
- `--cad-space-dialog-padding` for dialog chrome and body padding

These tokens define shared rhythm, not a global compression switch. Component structure, readable type, and usable controls remain authoritative.

### Semantic tones

- Primary: `--cad-tone-primary-solid-*`, `--cad-tone-primary-soft-*`
- Secondary: `--cad-tone-secondary-soft-*`
- Success: `--cad-tone-success-soft-*`
- Info: `--cad-tone-info-soft-*`
- Warning: `--cad-tone-warning-soft-*`
- Danger: `--cad-tone-danger-soft-*`
- Neutral helpers: `--cad-tone-light-soft-*`, `--cad-tone-base-soft-*`, `--cad-tone-dark-soft-*`

Each tone family is designed so shared components can pull background, border, hover, and foreground from one semantic source.

## Shared Component Families Already Bound To Tokens

These are the first places to inspect before adding new styling:

- Buttons: `Tailwind/controls/buttons.css`
- Badges: `Tailwind/controls/badges.css`
- Alerts: `Tailwind/feedback/alerts.css`
- Cards and summary tiles: `Tailwind/surfaces/cards.css`
- Typography helpers: `Tailwind/typography/text.css`
- Page headers: `Tailwind/navigation/page-header.css`
- Fields and inputs: `Tailwind/forms/fields.css`
- Dialogs and dialog scaffolds: `Tailwind/modals/dialogs.css`
- Tree view: `Tailwind/navigation/treeview.css`
- Tabs support helpers: `Tailwind/navigation/tabs.css`
- Legacy wrapper aliases stabilized toward `cad-*`: `Tailwind/layout/sheets.css`, `Tailwind/layout/stats.css`, `Tailwind/forms/tag-editor.css`, `Tailwind/controls/buttons.css`

## Naming Guidance

- Keep semantic shared component classes on the current `cda-*` family unless there is a deliberate migration plan.
- Keep theme or wrapper infrastructure on `cad-*`.
- Do not add new shared non-canvas `zy-*` selectors.

## Practical Rule

If the request changes brand color, rounding, or repeated component rhythm for the whole product, stay at the token layer first. If one page wastes space, fix its component composition before changing global tokens. Use [../../candoitall-components-mcp/references/compact-ui-composition.md](../../candoitall-components-mcp/references/compact-ui-composition.md) for density decisions; use local composition for unique chrome.
