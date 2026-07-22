# Compact UI Composition

Use this reference for CanDoItAll page, list, form, metric, card, tab, and dialog decisions. Compact means task-first and space-efficient, not smaller text, weak hit targets, or indiscriminate compression.

## Start With Page Roles

Record these decisions before writing markup:

- **Primary surface:** the list, editor, canvas, report, or workflow users came to operate.
- **Supporting content:** filters, metrics, help, history, diagnostics, and secondary views. Keep only what must remain visible beside the primary task.
- **First viewport:** the useful task state that should be visible before page scrolling at the target viewport.
- **Scroll owner:** one intentional page, panel, list, or workbench owner. Avoid nested scrolling unless the interaction requires it.

Use one compact `PageHeader` for identity, context, and page actions. Do not repeat the same introduction in a hero card. Let available space enlarge the primary surface or a useful sparse-page presentation rather than adding decorative padding.

## Choose The Smallest Honest Composition

### Metrics

- Use a `Badge`, count beside a heading, or `CompactStatStrip` when numbers support another task.
- Use `SummaryTiles`, `StatsGrid`, or metric cards when comparison and trend scanning are central, or when the page would otherwise be meaninglessly empty.
- Do not give a label/value pair a full card solely to fill a grid.

### Lists And Editors

- Make the collection the primary surface when browsing, searching, or comparing items is the page task.
- Use a `Dialog` for independent create/edit flows that can load on open and close without losing required list context.
- Keep an editor inline or split beside the list when continuous comparison, preview, or rapid multi-item editing makes simultaneous visibility necessary.
- Use tabs for mutually exclusive supporting views such as details, history, validation, or configuration. Do not hide the primary action or required error state behind an inactive tab.
- Load dialog-specific data when opening when it is safe and useful. Show loading and failure states explicitly; do not silently fall back to stale or empty data.

### Cards

- Use `CardGrid` for item collections and the structured `Card` slots: `Media`, `Header`, `Tags`, content, and `Actions`.
- Center bounded media at the top when present. Keep comparable card media and body rhythm consistent without stretching images.
- Keep identity/status near the header, descriptive content in the body, tags grouped, and actions aligned at the bottom.
- Keep the whole card clickable only when it has one unambiguous destination. Use explicit buttons for multiple actions.
- Prefer a row, list, or table when card chrome does not improve recognition or comparison.

### Forms And Text Areas

- Let normal fields use the available form width. Avoid narrow text areas for prose-oriented content.
- Use `TextAreaSize.Compact` for short notes or constrained fragments, `Standard` for typical descriptions, and `Extended` for long-form content. Use explicit `Rows` when domain content gives a better default.
- Group related fields with `FormSection`, `FormRow`, and `FormStack`; do not add a card around every field group.
- Keep validation adjacent to its field and reserve persistent page height only for states users need before submission.

### Dialogs

- Use `ModalSize.Compact` for confirmations and short single-section forms.
- Use `ModalSize.Medium` for typical multi-field create/edit flows.
- Use `ModalSize.Wide` for multi-column, preview, comparison, or dense configuration layouts.
- Use `ModalSize.Full` only for workbench-like tasks that genuinely need most of the viewport.
- Use dense chrome for ordinary task dialogs, but preserve clear title, description, validation, and action hierarchy.
- Keep primary and cancel actions in a stable footer. If content grows, let the body own dialog scrolling while the header and footer remain usable.

## Density And Spacing

Prefer component density parameters and semantic spacing tokens over page-local margin stacks:

- `--cad-space-compact-gap` for related inline controls or metadata;
- `--cad-space-content-gap` for content within one semantic region;
- `--cad-space-section-gap` between page sections;
- `--cad-space-surface-padding` for cards and panels;
- `--cad-space-dialog-padding` for dialog chrome and body padding.

Keep whitespace that communicates grouping. Remove duplicated outer/inner padding, browser-default heading or paragraph margins inside components, and repeated wrappers that create no semantic boundary. Do not globally shrink controls or typography to manufacture density.

## Container-Aware Compound Controls

- Make compound controls such as tabs, segmented controls, filter/action rows, and multi-field groups respond to their immediate containing width. A viewport media query alone is insufficient because a wide desktop page can still place the control in a narrow grid track, card, rail, or dialog column.
- Prefer intrinsic layout, wrapping, or container-query behavior that preserves semantic order, readable labels, usable hit targets, and visible focus states when the containing block narrows.
- Do not assume a control is responsive because it works in a full-width sandbox example. Validate it in the same constrained composition where consumers will use it.

## Component Selection Sequence

1. Describe the primary task, supporting content, stats role, editor interaction, and expected content length in `components_recommend`.
2. Inspect each recommended component and its real usage examples.
3. Compose with shared components and semantic sizes before adding structural CSS.
4. Improve BaseLib when repeated product markup is compensating for a missing shared slot, size, or layout contract.

## Browser Proof

At the target viewport:

1. Capture the normal state and verify the primary surface is useful in the first viewport.
2. Identify and verify the single intended scroll owner; check lateral overflow and nested scroll traps.
3. Open relevant dialogs, menus, dropdowns, tooltips, and floating surfaces. Capture and inspect the open state, including layering, clipping, internal scrolling, and action visibility.
4. Check long labels, realistic text-area content, validation, loading, empty, and error states where applicable.
5. Place affected compound controls inside representative narrow grid, card, rail, and dialog columns and verify that their layout follows the containing width even at a wide desktop viewport.
6. For reusable basic BaseLib components, repeat at small, medium, and large viewports. Application pages remain large-screen desktop by default unless narrower behavior is explicit scope.

Reject the result when users must scroll past repeated introductions or oversized supporting cards before reaching the primary task, when independent editors permanently displace the list, or when compact styling harms readability or control usability.
