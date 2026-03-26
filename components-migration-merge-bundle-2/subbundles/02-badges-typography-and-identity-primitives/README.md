# 02 Badges Typography And Identity Primitives

## Objective

Finish the high-frequency small primitives that are still stranded in Zyphonote and give `BaseLib` a complete reusable badge, text, divider, and identity surface.

## Component Set

- badge and chip family:
  - `Badge`
  - `BadgesGroup`
  - `StatusChip`
  - `Pill`
  - `PillList`
  - `Chip`
  - `ChipRow`
  - `ProfileTagChip`
  - `ProfileTagChipRow`
  - `BadgeTone`
  - `PillTone`
- typography and headings:
  - `Eyebrow`
  - `SmallText`
  - `MonoText`
  - `MutedInline`
  - `FooterText`
  - `SectionHead`
  - `SectionHeading`
  - `Divider`
- identity:
  - `Avatar`
  - `CreatorAvatar`
  - `CreatorLine`
  - `CreatorSocialLink`

## Exact Source References

- `C:\repositories\Zyphonote\src\App.Blazor\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\StatusBadge.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\TextBlock.razor`
- `..\..\inventories\02-sharedization-matrix.md`
- `..\..\inventories\03-tailwind-and-style-generalization-map.md`

## Implementation Steps

1. Create the shared badge family in `BaseLib`.
2. Use typed enums for tones and appearances.
3. Expand `TextBlock` or add very thin shared wrappers for the Zyphonote text aliases.
4. Promote a shared `Avatar` primitive and retire duplicate avatar wrappers.
5. Replace Zyphonote-local consumers with the shared primitives or compatibility wrappers that point at them.

## Hard Rules

- do not keep separate shared badge, pill, chip, and status dialects if one family can model them
- do not add new stringly-typed appearance values
- do not import seller-profile or sheet CSS names into `BaseLib`

## Acceptance Checklist

- high-use badge and typography wrappers are no longer stranded in Zyphonote
- text variants are represented cleanly in `BaseLib`
- avatar and identity primitives are shared or intentionally retired
- Zyphonote pages still render badge and text-heavy surfaces correctly

## Proof Required

- build proof for both repos
- screenshots from marketplace, my scores, and seller profile pages
- diff showing badge and text family ownership moved into `BaseLib`

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Move the remaining reusable badge, chip, typography, divider, and identity primitives from Zyphonote into BaseLib. Prefer stronger shared families over one-to-one wrapper copying, and keep the shared API typed and neutral.
```
