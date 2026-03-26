# Cross Repo Validation And Proof

Implementation agents must finish bundle-2 work with proof, not with assumptions.

## Required Build Proof

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `dotnet build C:\repositories\Zyphonote\Zyphonote.slnx`

## Required Ownership Proof

- diff of `CanDoItAll.Components.BaseLib` folder tree showing family subfolders
- diff of `Zyphonote.Components.csproj` showing removal of wildcard ownership
- inventory of remaining components in `Zyphonote.Components`
- inventory of feature-local components moved out of `Zyphonote.Components`

## Required Visual Proof

- screenshot or equivalent proof for the pages listed in `inventories/05-validation-surface-map.md`
- specific evidence that toolbar, card, list, badge, profile-form, and modal patterns still render correctly

## Required Debt Proof

- list of temporary compatibility wrappers added, if any
- removal plan for each compatibility wrapper
- confirmation that no shared library imported `zyphonote-compat.css`
