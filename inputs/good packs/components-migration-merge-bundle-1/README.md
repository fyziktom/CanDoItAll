# Components Migration Merge Bundle 1

This bundle is a planning and coordination package only. It does not implement the migration. All content in this folder is meant to guide future implementation agents working step by step.

## Mission

Consolidate the reusable component story around CanDoItAll while preserving the existing strength split:

- `CanDoItAll` remains the source of truth for canvas-related contracts, components, JS, and styling.
- `Zyphonote` contributes the stronger Razor wrapper implementations and several reusable page-surface patterns.
- app-specific UI stays app-specific unless it is clearly generic, stable, and worth owning centrally.

## Discovery Summary

- Current shared wrapper projects:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Components`
  - `C:\repositories\Zyphonote\src\App.Components`
- Current mixed canvas/shell project:
  - `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit`
- Current app-level component project with broad reusable potential:
  - `C:\repositories\Zyphonote\src\App.Blazor\Components`
- Inventory counts found during bundle preparation:
  - `CanDoItAll.Components`: 38 Razor wrapper components
  - `Zyphonote.App.Components/Radzen/Blazor`: 39 Razor wrapper components
  - `CanDoItAll.ComponentKit/Components`: 69 components
  - `CanDoItAll.ComponentKit/Canvas`: 51 canvas contracts/services/models
  - `Zyphonote.App.Blazor/Components`: 112 components

## Required End State

- `CanDoItAll.Components.Common`
- `CanDoItAll.Components.BaseLib`
- `CanDoItAll.Components.CanvasLib`
- `CanDoItAll.Mcp.Components`
- `CanDoItAll.Components.Sandbox`
- `CanDoItAll.Components`
- `Zyphonote.Components`

## Recommended Execution Order

1. `subbundles/01-foundation-and-governance`
2. `subbundles/02-shared-wrapper-baselib-merge`
3. `subbundles/03-canvaslib-extraction-and-hardening`
4. `subbundles/04-tailwind-and-asset-pipeline`
5. `subbundles/05-sandbox-catalog`
6. `subbundles/06-mcp-documentation-server`
7. `subbundles/07-candoitall-components-split-and-adoption`
8. `subbundles/08-zyphonote-components-split-and-adoption`
9. `subbundles/09-cross-app-validation-and-proof`

## Bundle Map

- `inputs`
  - saved original prompt
  - structured restatement and assumptions
- `architecture`
  - target library boundaries
  - phased migration plan
- `inventories`
  - wrapper diffs
  - component classification
  - CSS/JS/assets inventory
  - cross-repo dependency map
- `subbundles`
  - implementation-ready phase packs with source refs, checklists, proof rules, and prompts
- `templates`
  - shared-library change request template
  - governance and future skill outline
- `reviews`
  - self-review from QA, architect, and manager perspectives

## Non-Negotiables

- Do not edit shared libraries from the Zyphonote side once the split exists. Shared library changes must originate in CanDoItAll.
- Do not copy `zyphonote-compat.css` wholesale into `BaseLib`.
- Do not keep demo/preview/tuning components inside runtime libraries.
- Do not weaken the current canvas stack by replacing CanDoItAll canvas contracts with Zyphonote page-local canvas code.
- Do not preserve CDN icon dependencies in the final shared component story.
