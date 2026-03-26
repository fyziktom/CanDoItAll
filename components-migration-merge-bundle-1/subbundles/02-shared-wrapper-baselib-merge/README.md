# 02 Shared Wrapper BaseLib Merge

## Objective

Create `CanDoItAll.Components.BaseLib` and move the reusable wrapper library into it, using Zyphonote’s stronger implementation where the two projects differ.

## Exact Source References

Primary wrapper sources:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Primitives\ComponentPrimitives.cs`
- `C:\repositories\Zyphonote\src\App.Components\Radzen\Blazor`
- `C:\repositories\Zyphonote\src\App.Components\Radzen\RadzenPrimitives.cs`
- `C:\repositories\Zyphonote\src\App.Components\CssClassBuilder.cs`
- `C:\repositories\Zyphonote\src\App.Components\StyledComponentBase.cs`
- `C:\repositories\Zyphonote\src\App.Components\UiButton.razor`
- `C:\repositories\Zyphonote\src\App.Components\UiCard.razor`
- `C:\repositories\Zyphonote\src\App.Components\UiField.razor`
- `C:\repositories\Zyphonote\src\App.Components\UiSection.razor`

Shared-wrapper diff reference:

- `..\..\inventories\01-shared-wrapper-diff.md`

## Scope

- merge the shared wrapper components
- split helper ownership between `Common` and `BaseLib`
- keep the public API explicit under `CanDoItAll.Components.BaseLib`
- prepare compatibility staging for app adoption

## Implementation Steps

1. Create `BaseLib` root namespace and component namespace.
2. Move or recreate the wrapper components in `BaseLib`.
3. Use Zyphonote source as the baseline for the 22 differing files listed in the wrapper diff inventory.
4. Move the 18 identical files mechanically, then align namespace and helper usage.
5. Split `RadzenPrimitives.cs` and `ComponentPrimitives.cs` carefully:
   - neutral low-level helpers to `Common`
   - component-specific enums/services/helpers to `BaseLib`
6. Bring over:
   - `CssClassBuilder`
   - `StyledComponentBase`
   - `StepsNavigationPosition`
   - `Tabs.razor.css`
7. Strip or isolate app-branded compatibility looks:
   - `ButtonLook.SheetCard*`
   - `ButtonLook.Legacy*`
   - any other wrapper features that only make sense because Zyphonote app CSS still exists
8. Keep those branded looks out of the long-term shared API unless they are renamed and documented as neutral variants.
9. Promote `UiButton`, `UiCard`, `UiField`, and `UiSection` only if they still add value after `BaseLib` surfaces are in place; otherwise keep them app-local or delete them later.
10. Add or update bUnit coverage in `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`.

## Design Rules

- no `Radzen.*` final namespace
- no hidden dependency on Zyphonote global CSS
- no direct CDN requirement for icons
- no silent fallback logic that hides missing styles or icons

## Acceptance Checklist

- `BaseLib` compiles on its own
- the full wrapper set exists in `BaseLib`
- Tabs keyboard behavior, badges, visibility, and styling are preserved
- input wrappers preserve `Disabled` and `InputLook`
- component tests cover the new shared owner
- old CanDoItAll and Zyphonote projects can be adapted against the new library without losing behavior

## Proof Required

- list of moved wrapper files
- test results for updated component tests
- a short note documenting which branded variants were kept, renamed, staged, or rejected

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Create CanDoItAll.Components.BaseLib and move the shared wrapper library into it. Use the Zyphonote wrapper implementations as the baseline for the files identified as different in the bundle inventory. Keep the API strongly typed and neutral. Do not import zyphonote-compat.css or any app-global stylesheet into BaseLib. Do not start app rewiring yet.
```
