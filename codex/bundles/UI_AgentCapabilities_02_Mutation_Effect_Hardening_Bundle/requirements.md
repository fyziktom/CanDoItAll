# Requirements and traceability

The preserved raw request covers three workstreams; this child owns only its next-child mutation/effect topics. Earlier provider/catalog and Capabilities-01 implementations remain accepted predecessors.

| ID | Input / required behavior | Owner / planned proof |
|---|---|---|
| C02-01 | Immutable assignment before any await; no mutation of authoritative presentation before a known commit | SB01; copy/independence and rejected assignment |
| C02-02 | Failed-save rollback; preserve unrelated/later editor changes and local filters | SB01; rejected state/later edits |
| C02-03 | ExpectedUpdatedAtUtc conflict; known rejection/commit/warning versus unknown | SB00 trace, SB01 real persistence |
| C02-04 | Stable target/attempt; unresolved assignment cannot become replayable by target change | SB01; unknown/reentry verification |
| C02-05 | Assignment/verification generations, owner cancellation and authoritative refresh | SB01/SB02; delayed owner/canonical tests |
| C02-06 | Capture typed preview draft, cancel/supersede; no stale errors or busy clear | SB02; preview lifetime |
| C02-07 | Details/setup direct and nested dialog ownership; preserve unrelated overlays | SB02; real DialogHost/removal |
| C02-08 | Exact curator identity/authorization and launch cancellation; no false rollback of a created chat | SB02; launcher contract/late result |
| C02-09 | Known commit plus failed refresh offers reconciliation without effect replay | SB01/SB02; commit and zero-additional-write tests |
| C02-10 | Future extraction/list/CSS/assets and representative sandbox scenarios | SB03 inventory only; no move/sandbox |
| C02-11 | Preserve one requested page owner, one accepted session, one host and one service-free surface | all phases; owning 59-case selection and browser |
| C02-12 | Direct builds, failing-first, exact discovery, canonical fixtures, portability/secrets, live siblings and honest retained evidence | each phase/SB03 |

No blanket InvalidOperationException -> safe rejection. No automatic replay of a possibly performed diagnostic or non-idempotent write. No global CloseAll, outbox, universal effect controller, provider-registry redesign or new first-create identity system: assignment edits an existing stable agent ID. Preserve previous provider/publication semantics.

## Current execution request supersedes preparation shorthand

The complete [execution request](inputs/02-execution-request.txt) is governing. All 20 adjudications and 49 required behavioral topics are retained. C02-02 means authoritative-before presentation, not a mutate-and-rollback implementation. C02-05 separates assignment from diagnostics and proof persistence. C02-07/08 are panel scoped; preview is target scoped; unresolved write/proof and launch attempts require circuit retention. Known commit wins over cancellation. No blind write or diagnostic replay. C02-10 now authorizes preparation of Capabilities-03 only after this child closes.
