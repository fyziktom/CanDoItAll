# Exact-head merge gate and bookkeeping

## Goal

Produce a truthful decision for merging into development under the current post-merge macOS policy.

## Entry

Read the root execution prompt, findings, requirements, invariants and
validation strategy. Reconfirm the exact repository anchor before editing.

## Tasks

1. Freeze the exact repaired commit and source fingerprint.
2. Run the final C2 gate from validation-strategy.md.
3. Reconcile the focused tests, runtime catalog and Docker smoke.
4. Record any pre-existing stable-suite residuals without re-labeling them as passes.
5. Update prior M10 wording so macOS absence is a post-merge deferral, not an automatic NO-GO.
6. Keep enterprise vault and Keychain limitations explicit.
7. Do not merge or push without operator instruction.

## Rules

- Preserve unrelated changes.
- Use focused failing-first tests.
- Keep source comments in English.
- Do not push or merge.
- Do not weaken a validator to make evidence pass.
