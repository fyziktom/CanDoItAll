---
name: candoitall-subbundle-validator
description: "Validate a CanDoItAll work unit before implementation and after proof, using its actual bundle shape and declared Standard, Behavioral, or Governed proof tier. Use to protect prerequisites, dependency trust, progression decisions, and reopen behavior without imposing unrelated evidence."
---

# CanDoItAll Subbundle Validator

Gate the current work unit against current repo evidence. Return `Pass`, `Fail`, or `Blocked` with the exact determining issue and downstream impact.

## Entry Gate

Confirm:

- the subbundle still owns the intended raw inputs and requirements;
- its outcome, non-goals, acceptance criteria, and proof tier are explicit;
- prerequisites are complete and their proof remains trusted;
- source references/discovery instructions still identify the correct surfaces;
- dependency order and parallel work do not create unsafe overlap;
- applicable architecture, UI, host, security, migration, or production overlays are ready.
- applicable UI work records its primary surface, supporting content, stats treatment, list/editor composition, textarea/dialog sizing, first-viewport target, and scroll owner using `candoitall-components-mcp/references/compact-ui-composition.md`.

Stop and repair/reopen when any prerequisite is stale, weak, or contradicted.

## Closure Gate

Confirm:

- acceptance criteria and required affected-scope validation are complete;
- the selected proof tier is satisfied;
- actual code and tests support the recorded behavior;
- normal-state and relevant open-overlay screenshots were inspected, not only captured;
- CanDoItAll app UI was proven at the target large-screen desktop viewport, with small/medium checks required only for reusable basic BaseLib or explicit scope;
- the implemented UI still matches its recorded compact composition, with first-viewport, scroll-owner, sizing, and open-overlay findings in the execution report;
- host-visible behavior has host proof;
- raw-input closure and progression state were updated while evidence was fresh;
- a critical foundation has the dependent-flow check needed to lend trust downstream.

## Proof Tiers

- `Standard`: exact affected checks and results.
- `Behavioral`: Standard plus realistic positive and meaningful negative/boundary/regression proof.
- `Governed`: Behavioral plus manifest, semantic invariant contract, hashes, transcripts, source assertions, anti-stub evidence, and applicable browser/host/downstream/red-team artifacts.

Do not require Governed proof from a lower tier. Do not accept lower-tier evidence when the subbundle declared Governed.

## Reopen Rule

Reopen immediately when later observations contradict ownership, behavior, dependency direction, or proof. State which downstream phases must be revalidated.

## C# Architecture Checks

When relevant, require planned dependency direction, target owner, testability seam, partial-class policy, composition impact, and architecture review/CodeAnalytics evidence appropriate to the proof tier. Stop on cyclic references, unplanned project references, fake separation, or tests that still require the original god object.

## References

- Read [references/prerequisite-and-closure-gates.md](references/prerequisite-and-closure-gates.md).
- For UI gates, read `candoitall-components-mcp/references/compact-ui-composition.md`.
- Read [../candoitall-bundle-execution/references/semantic-adequacy-proof.md](../candoitall-bundle-execution/references/semantic-adequacy-proof.md) for Behavioral/Governed proof.
- Read [../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md](../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md) only for Governed proof.

## Exit Condition

Pass only when downstream work can rely on the current phase without borrowing trust from intent or excessive ceremony.
