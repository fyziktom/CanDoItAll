# Structured Input

## Objectives

- prepare `components-migration-merge-bundle-2` only
- analyze `components-migration-merge-bundle-1` and keep a professional bundle structure
- identify all meaningful missing component transfers from Zyphonote into CanDoItAll shared libraries
- add explicit organization guidance for `CanDoItAll.Components.BaseLib` subfolders
- give implementation agents enough detail to finish the migration without rediscovery

## Constraints

- do not implement the migration
- do not edit runtime component libraries as part of this task
- focus on components still left behind after the first migration bundle
- transfer as much as is reasonable, but do not force domain-specific or canvas-specific Zyphonote UI into `BaseLib`

## Assumptions

- `C:\repositories\Zyphonote` is available locally and is the authoritative source for the missing components
- the current `CanDoItAll.Components.BaseLib` project state is the post-bundle-1 baseline
- the final shared owner for reusable primitives is still `CanDoItAll.Components.BaseLib`
- `Zyphonote.Components` should become an explicit owner project, not a wildcard projection of `App.Blazor\Components`

## Risks To Control

- over-sharing product-specific music or marketplace components
- under-sharing thin wrappers that are obviously generic
- copying Zyphonote CSS naming and technical debt into `BaseLib`
- keeping weak ownership boundaries by leaving wildcard includes in place
- adding shared components as flat files with no family structure inside `BaseLib`

## Bundle-2 Decision Rule

Each candidate component falls into exactly one action type:

| Action | Meaning |
| --- | --- |
| `ExpandExistingBaseLib` | the concept already exists in `BaseLib`, but parity is incomplete |
| `PromoteNewBaseLib` | create a new shared component or support type in `BaseLib` |
| `MergeIntoExistingBaseLib` | do not copy the wrapper 1:1; extend or parameterize an existing shared primitive |
| `RetireWrapper` | delete the Zyphonote wrapper and use shared primitives directly in consumers |
| `KeepLocal` | keep inside Zyphonote because the behavior is domain-specific |
| `MoveFeatureLocal` | do not keep it in `Zyphonote.Components`; place it next to the owning workflow/page |
