# Components Migration Merge Bundle 2

This bundle is a planning and coordination package only. It does not implement the migration. All content in this folder is meant to guide future implementation agents working step by step.

## Mission

Finish the missing reusable-component transfer from Zyphonote into `CanDoItAll.Components.BaseLib`, tighten ownership boundaries, and leave `Zyphonote.Components` with only the domain-specific UI that truly belongs there.

Bundle 1 created the initial split. Bundle 2 finishes the work that was left behind:

- it audits the real post-bundle-1 state instead of trusting the original plan
- it treats one-class wrappers as migration debt, not permanent architecture
- it gives `CanDoItAll.Components.BaseLib` a real subfolder taxonomy
- it stops the current wildcard ownership pattern in `Zyphonote.Components`

## Discovery Summary

- `C:\repositories\Zyphonote\src\App.Blazor\Components` currently contains 112 Razor components.
- `C:\repositories\Zyphonote\src\Zyphonote.Components\Zyphonote.Components.csproj` currently exposes 101 of those components through a wildcard include plus linked `App.Components` wrappers.
- Bundle 1 marked 32 Zyphonote app-level components as wave-1 `BaseLib` candidates.
- Current `CanDoItAll.Components.BaseLib` contains only 2 exact-name transfers from that wave-1 list:
  - `EmptyState`
  - `PageHeader`
- Most of the remaining Zyphonote wrappers are still structurally generic. Many should move to `BaseLib`; many others should disappear in favor of stronger `BaseLib` primitives instead of surviving as one-off wrappers.

## Required End State

- `CanDoItAll.Components.BaseLib` owns the reusable badge, text, card, list, toolbar, modal, form, and workspace surface story.
- `CanDoItAll.Components.BaseLib` uses explicit subfolders such as `Buttons`, `Forms`, `Modals`, `Cards`, `Badges`, `Typography`, `Lists`, `Navigation`, and `Layout`.
- `CanDoItAll.Components.BaseLib` keeps the root namespace stable even after foldering.
- `Zyphonote.Components` stops linking `..\App.Blazor\Components\**\*.razor` as a wildcard ownership model.
- `Zyphonote.Components` keeps only domain-specific or app-specific reusable components.
- page-local workflow wrappers in Zyphonote move closer to their feature instead of pretending to be shared library assets
- shared Tailwind and shared component styling remain owned by CanDoItAll

## Recommended Execution Order

1. `subbundles/01-baselib-taxonomy-and-explicit-ownership`
2. `subbundles/02-badges-typography-and-identity-primitives`
3. `subbundles/03-cards-lists-shells-and-workspace-primitives`
4. `subbundles/04-forms-toolbars-modals-and-interactive-primitives`
5. `subbundles/05-zyphonote-consumer-collapse-and-local-cleanup`
6. `subbundles/06-cross-repo-validation-and-proof`

## Bundle Map

- `inputs`
  - saved user request
  - structured restatement and assumptions
- `architecture`
  - bundle-2 scope rules
  - `BaseLib` subfolder taxonomy and namespace rules
- `inventories`
  - bundle-1 gap audit against the real filesystem
  - sharedization matrix for every relevant Zyphonote component family
  - Tailwind and CSS extraction guidance
  - target end state for `Zyphonote.Components`
  - validation surface map
- `subbundles`
  - implementation-ready work packs grouped by real migration families
- `proof`
  - validation checklist and required artifacts
- `templates`
  - proof ledger template
  - compatibility shim retirement template
- `reviews`
  - self-review and requirement traceability

## Non-Negotiables

- Do not copy `zyphonote-compat.css`, `brand.css`, or other app-global Zyphonote CSS into `BaseLib`.
- Do not keep wildcard-linked ownership in `Zyphonote.Components`.
- Do not preserve dozens of one-class Zyphonote wrappers once an equivalent `BaseLib` primitive exists.
- Do not promote music, notation, marketplace workflow, or app-canvas components into `BaseLib`.
- Do not keep adding new `Sheet*`, `Profile*`, `ScoreWorkbench*`, or `Zy*` names to shared libraries.
- Do not use stringly-typed tones or looks in newly promoted shared APIs when a focused enum is more appropriate.
