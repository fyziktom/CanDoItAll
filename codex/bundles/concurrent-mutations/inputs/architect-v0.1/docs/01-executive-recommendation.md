# 01 · Executive recommendation

## The architectural target

Define a governed mutation as a domain intention applied by the authoritative owner under explicitly named validity conditions, with an outcome that remains understandable after cancellation, conflict or a lost response. That is a narrower and more useful goal than simultaneous keystroke collaboration.

There are three independent questions. **Target validity:** is the object still the one, and in the content state, the editor read? **Decision validity:** are the relevant facts behind an agent's proposed change still acceptable? **Authority validity:** is this actor/attempt still allowed to publish this effect? A successful check in one category never implies the other two.

The approach is incremental. Preserve the modular monolith, owner contexts, typed bridges and existing runtime journals. Repair a fully closed Projects path first, then use actual conflict and performance measurements to choose subsequent boundaries. Do not begin with a platform-wide version column, command bus, global receipt service or wholesale replacement of transaction utilities.

## Why this is not just a UI problem

The project editor currently carries a nullable lifetime precondition, while the owner replaces scalar data and phase/option collections. The agent path delegates to the same owner with source authority and a mutation lease. These mechanisms protect different invariants; none establishes the editor's expected content revision in the inspected save contract. See SRC-03, SRC-06 and SRC-08 in [the source ledger](20-source-ledger.md).

A stale update can therefore arise even with a single human operator: an agent and an open editor are two independent writers. Adding a realtime channel is optional; making writes conditional is not optional for the protected update contract. Notifications improve awareness, but missed notifications must not make a stale write valid.

## Recommended investment order

| Investment | Immediate value | Cost boundary |
|---|---|---|
| Evidence inventory and cost baseline | Prevents fixing only one frontend while another writer bypasses the rule | Read-only, targeted discovery; no product framework |
| Minimal foundation proof/repairs | Establishes reliable commit classification, cleanup and lock participation | Existing utilities and owner ports; no wholesale isolation-level change |
| Projects vertical pilot | Stops lost project edits, preserves drafts and links safe retry to real owner commits | One owner and all of its enabled callers, including tools and import paths |
| Narrow intentions and selective merge | Reduces incidental conflicts and whole-list churn | Opt-in domain commands and allowed merge rules |
| One governed planning pilot | Rejects stale objectives, stale approvals and superseded attempts | A bounded read basis and existing authority mechanisms |
| Risk-driven expansion | Covers other editors, graph operations, storage and process publication | Reuse proven semantics; largest runtime integration after pilots |

## Decisions that should remain small

Use the existing application-managed token mechanism for the initial Projects edit boundary, subject to proving that every relevant writer participates. Preserve other owners' numeric versions, expected-state checks and content revisions where appropriate. A universal physical token type would create conversion work without increasing correctness.

Introduce an owner-local durable operation result for the new Projects retry contract only after reconciling creation reservations and existing tool journals. Do not pretend that the existing runtime creation receipt already provides durable deduplication. The Projects documentation explicitly distinguishes these concepts (SRC-15).

For the pilot, prefer making the small same-database search projection participate in the existing transaction **if measurement confirms bounded cost**. Otherwise persist a durable rebuild intention with an equally explicit ordering rule before declaring that part complete. Best-effort activity may remain best effort if it is not advertised as the audit record.

## Up-front consequences

Protected existing-object updates without their original expected stamp must stop succeeding. All active callers of that owner must be upgraded together, or the unconverted entry point must be disabled explicitly. A compatibility adapter that reads a fresh stamp and attaches it to an old body would reintroduce the original defect.

Editors need a conflict state and a submitted-state record, not only another hidden input. Tools need structured conflict/basis results and a bounded replanning policy. Migrations and imports need explicit local-token lifecycle rules. Tests that relied on unconditional updates must declare whether they are setup/maintenance or ordinary user mutations; they must not regain a secret bypass.

## Non-goals

No shared cursors, distributed locks across every module, CRDT/OT for all content, mandatory event sourcing, universal undo snapshots, per-keystroke server persistence, forced authentication of the existing local SSR surface, or automatic migration to microservices. This is also not a vehicle for the unrelated provider runtime, scheduler misfire or hard worker-timeout repairs from the earlier general audit. Those may interact with authorities or effects, but their independent fixes need separate scope.

## Review outcome requested

Approve or revise the invariants, pilot boundary, prerequisite evidence, compatibility policy and phase exit gates. Do not approve an unbounded rewrite. Unknown call paths remain an explicit P00 deliverable. An implementation slice is ready only when the selected owner's **complete enabled write perimeter** is evidenced and its tests/performance budgets are agreed.
