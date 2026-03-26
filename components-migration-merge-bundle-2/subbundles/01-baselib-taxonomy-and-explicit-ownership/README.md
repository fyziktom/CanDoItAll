# 01 BaseLib Taxonomy And Explicit Ownership

## Objective

Put `CanDoItAll.Components.BaseLib` on a professional footing before adding more migrated components, and stop the current wildcard ownership model in `Zyphonote.Components`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib`
- `C:\repositories\Zyphonote\src\Zyphonote.Components\Zyphonote.Components.csproj`
- `C:\repositories\Zyphonote\src\App.Blazor\Components`
- `C:\repositories\Zyphonote\src\App.Components`
- `..\..\architecture\02-baselib-subfolder-organization.md`
- `..\..\inventories\01-bundle-1-gap-audit.md`
- `..\..\inventories\04-zyphonote-components-end-state.md`

## Implementation Steps

1. Reorganize `BaseLib` into explicit family subfolders without breaking the root namespace.
2. Split `BaseLibPrimitives.cs` into family-local enums and support files.
3. Add parity work items for the existing shared primitives that are known gaps:
   - `PageHeader`
   - `EmptyState`
   - `Dialog`
   - `Notification`
4. Replace wildcard ownership in `Zyphonote.Components.csproj` with explicit includes.
5. Stop linking `UiButton`, `UiCard`, `UiField`, and `UiSection` as if they were long-term library assets.

## Hard Rules

- do not move files into subfolders and accidentally change namespaces
- do not keep `BaseLibPrimitives.cs` as a junk-drawer after this phase
- do not keep wildcard-linked ownership in `Zyphonote.Components`
- do not start copying Zyphonote CSS during the foldering phase

## Acceptance Checklist

- `BaseLib` folder structure matches the taxonomy document
- shared support types are colocated by family
- `Zyphonote.Components` owns an explicit file list
- known shared primitive gaps are identified and staged for the next subbundles

## Proof Required

- tree diff of `BaseLib`
- project file diff of `Zyphonote.Components.csproj`
- list of remaining compatibility shims, if any

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Reorganize CanDoItAll.Components.BaseLib into explicit component-family folders without breaking the public namespace, split junk-drawer support files by ownership, and remove wildcard ownership from Zyphonote.Components. Do not start broad consumer rewiring yet.
```
