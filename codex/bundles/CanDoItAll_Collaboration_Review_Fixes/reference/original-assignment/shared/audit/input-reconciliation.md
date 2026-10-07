# Reconciliation of the three supplied bundles

This edition is one shared reference **for UI/component decoupling**. Implementers no longer need to compose three historical instruction sets for a UI slice. It does not repeal the broader Architecture Foundation for unrelated backend/domain work.

| Input | Retained here | Revised or retired from the UI shared baseline |
|---|---|---|
| `CanDoItAll_Architecture_Foundation`, v1.1, 2026-09-08 | Modular monolith; owner-directed typed operations; distinct technical/domain facts; context/admission; honest commit and recovery outcomes; projection provenance; cross-module journey preservation | Its then-current “finish bounded Agents first” is history, not a new blocker. The 76 semantic contracts, runtime-adapter roadmap and all-domain receipts/outbox/migration program are not automatic requirements of every UI extraction. Readiness is checked for the chosen owner boundary. |
| `UI_Component_Seams_Shared_Architecture_Bundle`, `CDA-UI-SEAMS-BASE-v2`, revised 2026-09-05 | Render/effect separation, explicit state and target lifetimes, no facade hiding dependencies, read lanes, safe mutation reconciliation, real-child/overlay/asset proof, measured sandboxability | Fold the accumulated reviews into topical rules. Replace Agents-first/current-child sequencing and stale file inventories with current-source discovery. Accept the existing workspace view approach and renderer-held EditContext. Remove dependency on missing historical sibling bundles. |
| `UI_Refactoring_Integration_Bundle`, prepared 2026-09-01, execution report 2026-09-02 | Source-mode dependencies, sibling asset/package discipline, clean production wiring, FileTools boundary, signed/authorized operations and honest gate provenance | Do not repeat its nine integration subbundles, old SHA denylist, old branch merges, 0.3.0 candidate selection, pending-signing status or blanket three-repo validation. Existing source targets and current CI replace old setup assumptions. Historical proof is not new execution proof. |

## Important corrections to interpretation

The Foundation already said not to split every DbContext before resuming a stable UI seam; this edition preserves that restraint rather than attributing a blanket backend-first rewrite to the original. It also preserves domain ownership when the selected seam truly needs a contract repair.

The old integration bundle's ban on `ui-refactoring-v2` protected that specific integration lineage. It is not a timeless prohibition on the owner's later API/UI work or a command to undo already integrated changes. The new bundle forbids unauthorized branch operations, not branch names.

Version `0.3.0` is present in the reviewed application's fallback properties [S21]. That observation does not establish present package-feed availability and does not authorize a version bump or publication. No package-feed check was needed or performed for this documentation review.

The UI shared input referred to child bundles not included in this upload. Their linked logs were not independently verified. Relevant durable lessons are restated here with current code/doc anchors; missing history is neither invented nor made an execution prerequisite.

## Replacement map

- Placement/contracts and legacy controller guidance → [boundaries](../architecture/01-boundaries.md).
- State, targeting, mutation, acknowledgement, revision, liveness and independent-lane reviews → [state and effects](../architecture/02-state-and-effects.md).
- Rendered-closure, source assets, observed styles and measured extraction reviews → [sandbox and dev loop](../architecture/03-sandboxes-and-dev-loop.md).
- Foundation authority, projection, lifecycle, runtime and safe contribution rules relevant to UI → [domain safeguards](../architecture/04-domain-safeguards.md).
- Historical phase and proof procedures → [validation](../VALIDATION.md) plus the two compact templates.

The archive contains none of the old TRX, screenshots, package binaries or fonts. Its source pointers and input ZIP hash preserve provenance without making historical execution artifacts part of every future prompt.

## Maintaining one durable authority

Keep `docs/architecture/ui-component-seams.md` as the maintained product rulebook, module completion/boundary documents as module records, and local sandbox READMEs as runnable entry guidance. Preserve old bundle IDs only in migration/history references where consumers still use them. Retire old copies only after active consumers migrate; do not delete historical evidence by executing this shared brief.

At the next appropriate documentation checkpoint, fold any accepted new general clarification from this review into the canonical UI document. Do not copy the whole package into `AGENTS.md` or maintain three mutually diverging versions of the same rules. Move reusable agent-skill conventions to the family-owned location only when that repository is actually in the authorized scope.
