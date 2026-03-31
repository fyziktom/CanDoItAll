# baselib-component-tone-and-radius-adoption

## Status

- `Ready`

## Objective

- Move high-value BaseLib primitives and shared CSS families from hard-coded palette utilities onto the new semantic theme contract, including shared radii.

## Covered Inputs

- `N01`, `N02`, `N04`, `N05`
- `R01`, `R02`, `R05`

## Prerequisites

- Subbundles `01`, `02`, and `03` completed and trusted

## Exact Source References

- `C:\repositories\CanDoItAll\Tailwind\controls\buttons.css`
- `C:\repositories\CanDoItAll\Tailwind\forms\fields.css`
- `C:\repositories\CanDoItAll\Tailwind\surfaces\cards.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\page-header.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\treeview.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\tabs.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\Button.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Badges\Badge.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Badges\StatusBadge.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\Alert.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Typography\TextBlock.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\PageHeader.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TreeView.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TreeViewNodeRow.razor`

## Deliverables

- BaseLib tone-bearing primitives mapped to semantic variables
- Shared radii aligned through the theme contract
- Reduced direct palette switching logic in BaseLib

## Dependency Impact

- Route migration must trust these primitives. If the primitives still hard-code colors or radii, route cleanup only hides the problem instead of solving it.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Migrate shared Tailwind component families to semantic tokens.
2. Update BaseLib primitives that still switch on hard-coded palette classes.
3. Keep descriptive enums and map them onto the new semantic families.
4. Smoke-test one dependent route so the primitives are proven in context.

## Scope Exceptions

- Route-level raw utility cleanup belongs to subbundle `05`.

## Do Not Do

- Do not scatter direct palette utilities back into Razor components.
- Do not rename app/module markup prefixes in this phase unless needed to preserve BaseLib primitive behavior.

## Acceptance Checklist

- Buttons, badges, alerts, key cards, page headers, tabs, or treeview surfaces no longer depend on raw palette utilities for their primary tone behavior.
- Shared radii are pulled from the theme contract instead of being silently duplicated.
- BaseLib still builds and renders on consuming routes.

## Proof Required

- Solution build
- Focused component or component-test run where practical
- One dependent-route screenshot proving the new primitive contract in context

## Browser Validation Logging

- Target routes: `/` and `/groups/foundations`
- Viewports: `1600x1000` plus one narrow-width pass if layout changes
- Required actions: inspect button, badge, alert, and card surfaces after the token migration
- Evidence paths: `evidence/theme-home-desktop.png`, plus focused component screenshots if needed
- Review questions: Do the semantic tones stay consistent across multiple primitives and do the new radii still feel deliberate rather than arbitrary?

## Progression Gate

- Solution build and at least one dependent route must prove that migrated primitives pick up the semantic theme contract consistently.

## Suggested Agent Prompt

```text
Implement this subbundle only. Move BaseLib primitives and shared component-layer CSS onto the semantic theme contract. Preserve descriptive enums and do not push raw palette utilities back into Razor.
```
