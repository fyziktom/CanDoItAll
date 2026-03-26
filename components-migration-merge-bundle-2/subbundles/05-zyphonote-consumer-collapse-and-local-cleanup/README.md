# 05 Zyphonote Consumer Collapse And Local Cleanup

## Objective

Shrink `Zyphonote.Components` after the shared primitives exist, rewrite consumers onto `BaseLib`, and move non-shared workflow wrappers out of the library.

## Exact Source References

- `C:\repositories\Zyphonote\src\Zyphonote.Components`
- `C:\repositories\Zyphonote\src\App.Components`
- `C:\repositories\Zyphonote\src\App.Blazor\Components`
- `C:\repositories\Zyphonote\src\App.Blazor\Pages`
- `..\..\inventories\04-zyphonote-components-end-state.md`
- `..\..\inventories\05-validation-surface-map.md`

## Implementation Steps

1. Replace `UiButton`, `UiCard`, `UiField`, and `UiSection` consumers with `BaseLib` primitives.
2. Replace thin Zyphonote wrappers with direct shared primitive usage where the wrapper adds no stable API value.
3. Move workflow-local components out of `Zyphonote.Components` and next to their owning features.
4. Keep only the explicitly local domain surfaces inside `Zyphonote.Components`.
5. Delete compatibility wrappers once the calling pages are updated.

## Hard Rules

- do not keep a wrapper just because pages already reference it
- do not leave score-workbench layout wrappers in `Zyphonote.Components`
- do not keep `App.Components` alive as a second shared UI abstraction layer
- do not pull shared source ownership back into Zyphonote

## Acceptance Checklist

- `Zyphonote.Components` is materially smaller and explicitly owned
- `App.Components` wrappers are gone or clearly temporary
- score-workbench wrappers are feature-local if they still exist
- domain-specific components still compile and render on top of the new shared primitives

## Proof Required

- diff of remaining files in `Zyphonote.Components`
- diff of components moved feature-local
- screenshot proof for the key validation pages
- note listing any temporary wrappers left behind and why

## Suggested Agent Prompt

```text
Implement subbundle 05 only.

Now that the shared primitives exist, shrink Zyphonote.Components aggressively. Replace wrapper debt with direct BaseLib usage, move workflow-local components next to their features, and keep only truly domain-specific reusable Zyphonote UI in the project.
```
