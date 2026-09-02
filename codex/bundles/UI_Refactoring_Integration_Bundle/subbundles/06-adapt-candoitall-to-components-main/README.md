# SB06 — Adapt CanDoItAll To Components Main

**Status:** Completed locally — 32 focused tests pass; source graph builds; legacy references and v2 violations are zero
**Outcome:** Current application consumes the merged Components contract with minimal fixes  
**Proof tier:** Behavioral

## Scope

- BaseLib host asset,
- old raw Material Icons DOM,
- CSS selectors,
- bUnit selectors,
- compile errors caused by current Components,
- coordinated fallback version `V`,
- host-side FileTools composition issues proven by tests.

## Non-goals

- no v2 toolbar/theme/navigation redesign,
- no broad replacement of application UI with newly cataloged Components,
- no mobile redesign,
- no icon-token aesthetic sweep,
- no FileTools-to-Components dependency.

## Static asset host

Ensure `App.razor` loads:

```text
_content/CanDoItAll.Components.BaseLib/css/material-symbols.css
_content/CanDoItAll.Components.BaseLib/css/output.css
```

Preserve current load order unless browser proof identifies a concrete ordering defect.

## Icon migration

Start from `inventories/03-known-icon-migration-surfaces.md`, then rerun repository-wide search.

### Razor

Prefer:

```razor
<Icon Name="@token" />
```

when the component can preserve class/style/accessibility semantics.

### RenderTreeBuilder or required raw span

Use a stable semantic class:

```text
cda-material-icon material-symbols-rounded
```

Preserve `aria-hidden`, title/label behavior, sequence stability, and CSS class composition.

### CSS and tests

Target:

```css
.cda-material-icon
```

Do not use implementation font class as the semantic test hook.

## Compile compatibility

Build after the icon/static-asset slice. For every compile error:

1. confirm it is caused by the current Components main,
2. inspect current component API and compatibility shims,
3. make the smallest call-site update,
4. add/update a focused test,
5. record the change.

Do not infer v2 behavior.

## Version properties

Apply selected `V` to both CanDoItAll fallback properties.

## Search gate

```bash
rg -n "material-icons|material-icons\.css" src tests Tailwind
```

Expected result: zero, excluding explicitly documented third-party/archival content outside the
active product graph.

Also check:

```bash
rg -n "material-symbols-rounded" src tests Tailwind
```

Raw implementation usage must be justified; CSS/tests should prefer `.cda-material-icon`.

## Focused tests

At minimum:

- `AgentCompactListTests`,
- `AgentCatalogPanelTests`,
- `PresentationBadgeListTests`,
- shell/layout tests,
- any component test modified due API changes.

## Acceptance

- source-mode product build succeeds,
- old host asset is gone,
- old semantic selectors are gone,
- icon tests pass,
- no unintended icon fallback on focused rendered tests,
- only minimal current-Components compatibility changes are present,
- v2 guard passes.

## Progression gate

Focused component and product build proof is green.

## Reopen triggers

- Components integration SHA changes,
- visual proof reveals preflight/layout regressions,
- FileTools host flow fails due a true host compatibility issue.
