# 03 · Terminology, identity and invariants

## Distinct identities, not a universal Version field

| Concept | Meaning | Lifetime / reset rule | Not equivalent to |
|---|---|---|---|
| Canonical database profile identity | Which installed dataset and owner binding is addressed | Existing canonical profile contract | An actor grant |
| Dataset restore/activation epoch | Disambiguates destructive restore or replacement where public IDs and old tokens may recur | Reuse a proven existing epoch if available; otherwise design under maintenance authority | A per-request number or an ordinary process restart |
| Object ID and lifetime | Public identity plus the specific live incarnation | Recreate/import allocates a new local lifetime | Content revision |
| Content stamp | Exact expected editable state or explicit owner-defined subset | Change when covered data changes, atomically with children | UpdatedAt timestamp or total ordering |
| Definition version | Immutable definition used for a workflow/process/model configuration | Old versions can remain valid for already admitted runs | Live editor token or current worker claim |
| Operation ID | Same intentional mutation through retries and duplicate deliveries | Stable for technical retry; new for a corrected or materially changed proposal | HTTP request ID or a creation reservation |
| Proposal / decision-basis ID | The effect being proposed and the relevant input observations | Replace after material reconsideration | Proof that the model reasoned correctly |
| Execution/claim generation | Which runtime attempt currently owns publication authority | Changes with supersession, takeover or reassignment | Content freshness |
| UI request/editor generation | Which page/editor/read completion is still relevant | Local host/editor lifetime | Durable operation or dataset epoch |
| Projection cursor | Ordering/progress of a particular derived view | Owner-specific; preserve source/lifetime association | A source of business truth |

Normal host restart must not invalidate every durable operation, receipt or approved immutable execution merely because a transient process generation changed. Destructive restore is different: restoring an old token or receipt namespace must not accidentally admit work originating from the overwritten future dataset. P00 first establishes which existing control-plane identity can express that difference. Do not add another epoch without reconciling existing profile-generation rules (SRC-05, SRC-11, SRC-15).

## Invariant catalogue

[The machine-readable invariant registry](../registers/invariants.json) is authoritative for stable IDs. The main contract is:

**A writer cannot silently replace changes outside the state it observed; a governed proposal cannot gain validity merely by refreshing technical preconditions; an ambiguous acknowledgement cannot trigger a blind second effect.**

The checks are different on different command kinds. An append to a conversation need not reject because a message arrived after the UI opened. It still needs correct conversation identity, authorization, request deduplication and any owner-defined ordering. A rename typically needs an edit stamp. A structural move needs identity and relational invariants. A planning proposal may need several source witnesses. Requiring the same giant envelope for all of them would defeat the purpose.

## Levels of validity for source inputs

**Pinned input:** the operation intentionally uses an immutable version. A newer live version does not invalidate computation; publishing into a live target still has its own conditions.

**Current at admission:** a source is checked when work is admitted. This is useful provenance but does not promise freshness after a long computation.

**Current at publication:** the source predicates remain protected through the publication transaction by a proven owner protocol. This is the target for material planning decisions where an intervening edit must cause rejection or ordering before that edit.

**External observation:** a remote or filesystem input is only as conditional/immutable as its provider contract. A timestamp or hash checked earlier is not a database lock on that external reality.

Each tool/command declares which level applies. The word "validated" without a level is insufficient.

## No-op, value equality and ABA

A request that sets a field to its current normalized value can be reported as a no-op only according to the command's policy. A stale protected snapshot must not automatically become acceptable merely because one field happens to match again. A value may change A → B → A while a long-running decision remains obsolete; the version or dependency generation detects that history when the policy requires it.

Some domains intentionally use value-based preconditions rather than generation history. Preserve that choice explicitly, especially in existing task contracts. Do not replace these checks with a weaker token or silently strengthen them into widespread false conflicts without impact analysis. Text trimming, UTC normalization, virtual option rows and ordered collections all affect equality.

## Guarantee boundary

The system can prove the captured input identities, enforce required dependencies and prevent unauthorized publication. It cannot prove an agent understood every fact, cannot keep the entire world fixed throughout reasoning, and cannot guarantee exactly-once effects in an external system lacking an adequate protocol. These are explicit design boundaries, not reasons to weaken the guarantees inside the owner transaction.
