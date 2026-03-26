# 06 Cross Repo Validation And Proof

## Objective

Close the migration wave with explicit build, ownership, and visual proof across both repositories.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\Zyphonote\Zyphonote.slnx`
- `..\..\proof\06-cross-repo-validation-and-proof\README.md`
- `..\..\proof\06-cross-repo-validation-and-proof\validation-checklist.md`
- `..\..\inventories\05-validation-surface-map.md`

## Implementation Steps

1. Build both repositories.
2. Audit `BaseLib` foldering and `Zyphonote.Components` ownership boundaries.
3. Capture visual proof for the named validation pages.
4. Record any temporary compatibility shims and their removal conditions.
5. Reject the wave if shared library ownership regressed or if wildcard linkage still exists.

## Hard Rules

- do not accept build-only proof
- do not accept screenshot-only proof
- do not accept the wave if shared CSS ownership is muddy
- do not accept lingering wildcard includes in `Zyphonote.Components`

## Acceptance Checklist

- both repos build
- shared ownership boundaries are explicit
- validation surfaces render correctly
- any temporary debt is documented with removal conditions

## Suggested Agent Prompt

```text
Implement subbundle 06 only.

Run the final validation pass for the bundle-2 migration work. Prove builds, prove ownership boundaries, prove visual parity on the named Zyphonote pages, and record any temporary compatibility debt that remains.
```
