# Prefix Summary

## Inventory Result

- Total scanned prefix references:
  - `zy-*`: `832`
  - `cda-*`: `801`
  - `cad-*`: `0`

## Preparation Conclusion

- Prefix stabilization must be treated as a first-class migration phase.
- A direct hard cut from `zy-*` and `cda-*` to `cad-*` would be risky.
- The bundle will introduce `cad-*` as canonical, then carry compatibility selectors while the highest-value non-canvas callers migrate.

## Immediate Hotspots

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor.css`
- `C:\repositories\CanDoItAll\Tailwind\controls\buttons.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\Pages\ResourcesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\Components\ProjectStructureAgentSettingsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Prompts\Pages\PromptGalleryPage.razor`

## Follow-up Rule

- No changed legacy shared non-canvas wrapper surface added during execution should rely on `zy-*` as its only class path.

## Execution Addendum

- The execution pass kept the existing `cda-*` semantic component and tone family stable because the request explicitly treated `cda-button--tone-primary` as the right direction for reusable semantic styling.
- Prefix stabilization therefore focused on the unstable legacy wrapper family still carrying raw `zy-*` naming on shared non-canvas surfaces.
- Changed wrapper components now emit forward-facing `cad-*` classes while keeping compatibility aliases for legacy `zy-*` callers.
- Shared Tailwind legacy files that still define those wrapper surfaces now accept both `cad-*` and `zy-*` selectors where the blast radius was bounded.
