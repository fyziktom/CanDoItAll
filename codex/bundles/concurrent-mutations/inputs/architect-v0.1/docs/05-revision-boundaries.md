# 05 · Revision boundaries and mutation semantics

## Initial Projects boundary

One edit stamp covers the current project's editable scalar definition plus phases and options. Include Name, Description, Objective, Status, CurrentPhase and TargetDateUtc, with Slug treated as accepted derived state. Include phase identity/content/status/dates/order and option identity/category/content. A write to any covered child must advance the root content stamp in the same transaction.

Do not bump that stamp for a chat message, agent progress, usage event, unrelated artifact, or a projection rebuild. Project hierarchy is a distinct relational boundary. Lifecycle admission remains a prerequisite even when a content stamp is valid. The final schema/DTO names are deferred to P01; the semantic boundary is not.

| Change | Initial edit stamp | Separate requirement |
|---|---|---|
| Rename or change objective | Advance | Validate normalization and original expected stamp |
| Add/update/delete phase | Advance even with unchanged project scalars | Validate child belongs to this project; return assigned ID |
| Change phase order | Advance | Explicit order semantics and complete intended permutation |
| Add/update/delete option | Advance | Clarify virtual default categories and blank-row behavior |
| Chat append / execution progress | No | Conversation/run owner and their own authority/ordering |
| Attach/reconnect subproject | No automatic definition bump | Graph invariant, source/target lifetimes and graph guard |
| Source projection refreshed | No | Projection source ordering and incarnation |
| Project deleted/recreated | New lifetime; new initial stamp | Retire exact old lifetime and invalidate old authority |
| Import to new profile | New local lifetime/stamp | Imported history remains nonauthorizing |

This is intentionally conservative: a renamed project can initially conflict with a phase addition. That is a usability cost, not data loss. Split the boundary only when observed conflict rates justify extra revision-maintenance complexity. A separate objective/planning-facts revision may later reduce false invalidation, but it requires complete writer coverage and is not a free cache key.

## Other owners: preserve, then extend

Task details already combine expected values and assignment revision (SRC-09). Preserve checks for execution state, scheduling, estimates and cost basis, not just title. Definitions and immutable published versions are distinct from run claims. Storage content revision protects bytes; a database metadata token does not replace it. Conversation messages are append-oriented and need duplicate suppression rather than whole-conversation replacement. Collaboration thread flags/status can have conditional transitions independently from reply append.

The [mutation inventory](../registers/mutation_inventory.json) gives proposed boundaries and coverage status. For owners only documented at this review, no exact schema change is prescribed until P00/P05 reads their current code.

## Whole snapshot versus explicit intention

The existing whole-editor save can remain during P02 if its full boundary is protected. P03 introduces selected operations such as rename, update objective, add phase, edit phase, remove phase and reorder phases. These are owner commands, not arbitrary JSON paths. A bulk replace remains an explicit and deliberately stronger command.

An add command should carry a stable operation ID and, where appropriate, a proposed client row identity or temporary-row correlation. A reply to a repeated add returns the same accepted row. An unknown supplied persisted child ID must not silently become a new item under the protected edit contract. Duplicate IDs, cross-project IDs, duplicate categories where forbidden and invalid ordering must reject deterministically before commit.

A narrow command does not automatically permit a stale write. It carries the precondition relevant to its effect, and the owner checks resulting invariants. A field-mask PATCH alone cannot establish safety when two fields jointly define a business rule.

## Token selection

Start with the existing application-managed GUID convention for the Projects edit stamp (SRC-07), subject to verifying that ProjectsDbContext, the complete model and every write path can maintain it. Do not compare GUIDs to determine temporal order. Existing numeric revisions and physical content hashes in other owners remain valid if they implement the required semantics.

Use a database-generated row version only when its physical row boundary and public lifecycle are appropriate. This proposal does not standardize on a PostgreSQL implementation detail as the public aggregate version. Timestamps are not suitable sole equality tokens for this contract. The choice is about exact expected state, not human-friendly sequential numbering.

## Aggregate stamping pitfalls

Automatic stamping of tracked modified entities does not notice a child-only root change. Bulk SQL does not automatically pass through the tracker. A late retry can carry already changed in-memory values. Imported DTOs can reintroduce source tokens. A root that stamps on every unrelated maintenance touch creates false conflicts. These are explicit tests, not reasons to scatter interceptor magic across modules. EF's tracked and bulk concurrency paths differ (EXT-01, EXT-05).

The owner must define a single atomic point for advancing the aggregate stamp, ensure it happens once per accepted command or logical batch, and return the accepted stamp from that commit. For repeated SaveChanges inside an owner transaction, specify whether intermediate token changes are private and ensure only the final committed stamp leaves the boundary.

## No-op policy

For the Projects pilot, normalize the intended editable values, compare under the original valid expected state, and define whether a fully unchanged accepted command advances the stamp. Recommended default: no content revision advance for a true no-op, but a durable operation result may still record acceptance. Reject stale whole-snapshot submissions before relying on this optimization. Preserve existing UpdatedAt semantics until a separately documented compatibility decision changes them.

A receipt must distinguish `CommittedChange` from `AcceptedNoChange` without teaching callers to replay either. Version advancement, activity emission and search work should be explicit for each case. Changing no-op behavior affects sorting, activity feeds and tests; it is not merely a performance optimization.
