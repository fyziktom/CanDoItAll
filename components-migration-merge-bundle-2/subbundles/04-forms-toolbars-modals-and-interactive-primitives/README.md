# 04 Forms Toolbars Modals And Interactive Primitives

## Objective

Finish the reusable form-shell, toolbar, modal, callout, ribbon-tab, and tag-editor story so Zyphonote can stop carrying app-local wrappers for generic UI behavior.

## Component Set

- forms and settings:
  - `FormRow`
  - `FormStack`
  - `InlineActions`
  - `SheetField`
  - `ProfileField`
  - `ProfileToggle`
  - `SettingsSwitchLabel`
  - `SettingsSwitchRow`
  - `DebugToggle`
- toolbars and navigation:
  - `Toolbar`
  - `ToolbarActions`
  - `ToolbarFields`
  - `ToolbarRow`
  - `DashboardActions`
  - `ImmersiveRibbonTabs`
- feedback and modals:
  - `Callout`
  - `CalloutTone`
  - `TagTextEdit`
  - `TagTextValueNormalizer`
  - `ZyWorkspaceModal`

## Exact Source References

- `C:\repositories\Zyphonote\src\App.Blazor\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\FormField.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\FormSection.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\FilterBar.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Dialog.razor`
- `..\..\inventories\02-sharedization-matrix.md`
- `..\..\inventories\03-tailwind-and-style-generalization-map.md`

## Implementation Steps

1. Extend or add shared form-shell primitives so profile and settings wrappers can be retired.
2. Finish the shared toolbar and filter-bar surface.
3. Promote `TagTextEdit` into a real shared `TagEditor` control with a colocated normalizer.
4. Turn the stub dialog story into a reusable shared modal shell.
5. Promote the ribbon-tab interaction model into `BaseLib`.

## Hard Rules

- do not keep `DebugToggle` or similar one-off wrappers if `Switch` and shared form primitives already cover the need
- do not keep separate Zyphonote toolbar and `BaseLib` filter-bar stacks long term
- do not add string-based size or tone parameters when a focused enum is stable enough

## Acceptance Checklist

- form-shell and settings wrappers are shared or retired
- toolbar patterns are shared
- tag editor is no longer Zyphonote-only
- shared modal and ribbon-tab patterns are available in `BaseLib`

## Proof Required

- build proof for both repos
- screenshots from `AccountSellerProfile`, `AccountMarketplace`, `AccountEvents`, and any modal flows touched
- ownership diff for forms, toolbars, and modal family files

## Suggested Agent Prompt

```text
Implement subbundle 04 only.

Move the reusable form, toolbar, modal, callout, tag-editor, and interactive primitives from Zyphonote into BaseLib. Retire thin wrappers when existing shared primitives can absorb them, and finish the shared dialog story instead of keeping Zyphonote-specific modal infrastructure.
```
