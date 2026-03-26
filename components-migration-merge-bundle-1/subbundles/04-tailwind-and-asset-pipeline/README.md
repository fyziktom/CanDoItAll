# 04 Tailwind And Asset Pipeline

## Objective

Move shared styling ownership to CanDoItAll, establish a shared Tailwind source model, and replace CDN/runtime asset ambiguity with explicit library-owned assets.

## Exact Source References

- `C:\repositories\CanDoItAll\Tailwind\package.json`
- `C:\repositories\CanDoItAll\Tailwind\input.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\wwwroot\css\output.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit\wwwroot\canvas-workbench.css`
- `C:\repositories\Zyphonote\Tailwind\package.json`
- `C:\repositories\Zyphonote\Tailwind\input.css`
- `C:\repositories\Zyphonote\src\App.Components\wwwroot\css\output.css`
- `C:\repositories\Zyphonote\src\App.Components\Radzen\Blazor\Tabs.razor.css`
- `C:\repositories\Zyphonote\src\App.Blazor\wwwroot\zyphonote-compat.css`
- `C:\repositories\Zyphonote\src\App.Blazor\wwwroot\app.css`
- `C:\repositories\Zyphonote\src\App.Blazor\wwwroot\brand.css`
- `C:\repositories\Zyphonote\src\App.Server\Components\App.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor`

## Implementation Steps

1. Create a shared Tailwind source owned by CanDoItAll for `BaseLib`.
2. Keep `CanvasLib` CSS separate at first unless the extracted canvas CSS becomes cleanly splittable.
3. Move shared wrapper/component CSS into:
   - inline Tailwind utilities where readable
   - `@layer components` sources where reusable
   - isolated CSS files only when complexity justifies it
4. Treat `zyphonote-compat.css` as a quarry, not a dependency:
   - extract only the selectors needed for promoted shared components
   - rewrite them into neutral shared sources
   - leave app-specific selectors in Zyphonote
5. Vendor icon assets locally in `BaseLib`.
6. Keep `FontAwesomeIconCatalog` only as a migration bridge if needed.
7. Remove the Font Awesome CDN dependency from Zyphonote after local asset ownership exists.
8. Ensure both apps compile their own app-specific CSS separately from the shared CSS.

## Recommended Output Ownership

- `BaseLib`
  - owns wrapper/component output CSS
  - owns local icon assets
- `CanvasLib`
  - owns `canvas-workbench.css` and canvas JS/CSS assets
- `CanDoItAll.Web`
  - owns only CanDoItAll app-specific CSS
- `Zyphonote`
  - owns only Zyphonote app-specific CSS

## Hard Rules

- do not import Zyphonote global CSS into `BaseLib`
- do not keep CDN icons in the final shared story
- do not generate shared library CSS from the Zyphonote repo
- do not convert complex CSS to Tailwind if the result becomes harder to maintain

## Acceptance Checklist

- shared CSS is built from CanDoItAll-owned Tailwind sources
- app-specific CSS is clearly separated from shared CSS
- the Tabs shared styling is owned by `BaseLib`
- the icon story is local and reproducible
- `CanvasLib` asset paths are explicit and stable

## Proof Required

- tailwind script/config diff
- list of shared asset files now owned by CanDoItAll
- before/after note for the CDN removal
- build proof that both apps resolve shared static assets correctly

## Suggested Agent Prompt

```text
Implement subbundle 04 only.

Establish the shared Tailwind and asset pipeline so BaseLib and CanvasLib own their CSS and assets from the CanDoItAll side. Do not import zyphonote-compat.css wholesale. Vendor icon assets locally and remove CDN dependence only after the local asset path is working. Do not start sandbox or app rewiring in this phase.
```
