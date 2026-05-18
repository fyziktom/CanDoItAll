# 20 Claim Evidence Belief Ledger

## Purpose

Prevent canonical summaries from hiding contradictions, scope differences, or weak evidence.

The existing memory item model is useful, but enterprise cognitive memory needs a lower-level belief model:

```text
source anchor -> evidence ref -> atomic claim -> belief state -> memory item/chunk/procedure summary
```

## Why Atomic Claims Are Needed

A memory item may contain several claims:

```text
Docker Compose is suitable for local plugin development.
Docker Compose is suitable for production deployment.
Docker Swarm is supported by the current deployment strategy.
Bind mount behavior differs on Windows/WSL/Linux.
```

These claims have different evidence, risk, scope, and validity. If they are stored as one summary, the system can answer incorrectly while appearing source-backed.

## Claim Model

A claim should include:

- claim id,
- project id,
- normalized claim text,
- optional subject/predicate/object shape,
- claim type,
- scope/context frame ids,
- temporal validity window,
- confidence score vector and derived display confidence,
- validation state,
- stability state,
- source/evidence refs,
- support/attack relation ids,
- revision lineage,
- owner algorithm/version,
- created/updated timestamps.

## Evidence Anchor

Evidence must be anchorable.

Recommended anchor fields:

- source manifest id,
- source item id,
- source system,
- storage locator,
- structured path,
- text span start/end,
- quote hash,
- source trust level,
- redaction state,
- observed timestamp,
- source version/hash.

## Evidence Direction

Evidence can:

- support claim,
- attack claim,
- qualify claim,
- supersede claim,
- narrow scope,
- broaden scope,
- provide example,
- provide counterexample.
- provide self-regulation assessment context,
- provide professor-review challenge or critique,
- provide calibration evidence after outcome validation.

Self-regulation, professor review, and calibration evidence are evidence directions only. They do not directly create belief state or source truth.

## Belief State

Belief state is derived from claim evidence, validation, context, and time.

Suggested states:

- `Unexamined`,
- `Supported`,
- `WeaklySupported`,
- `Contested`,
- `Contradicted`,
- `ScopeLimited`,
- `Stale`,
- `Superseded`,
- `Rejected`,
- `Validated`.

Do not treat belief state as universal truth. It is always relative to scope, source trust, and time.

Belief state must use the shared `BeliefState` score space. Support evidence, attack evidence, source quality, context fit, temporal validity, review state, contradiction pressure, and staleness are score dimensions. Do not calculate belief as a simple support-minus-attack scalar.

## Relation To Existing `MemoryItem`

Do not remove `MemoryItem`. Use it as a chunk/container that composes claims.

| Existing object | New relationship |
|---|---|
| `MemoryItem` | Contains or summarizes one or more claims. |
| `MemoryRelation` | May link items or claims. |
| `MemorySourceRef` | Supplements or points to evidence anchors. |
| `ContradictionCandidate` | Should identify conflicting claims, not only items. |
| `RecallCandidate` | Should expose selected claim ids where available. |
| `ProbeFinding` | Can point to claim ids and evidence anchors. |

## Claim Revision

Claims should be updated through explicit operations:

- propose claim,
- support claim,
- attack claim,
- narrow scope,
- broaden scope,
- supersede claim,
- reject claim,
- validate claim,
- retire claim.

These operations go through `IMemoryMutationAuthority`.

## Mutation Authority

All authoritative memory changes must pass through a command boundary:

- deterministic mutation id,
- idempotency key,
- actor id,
- source/evidence refs,
- affected claims/items/relations,
- precondition/version token,
- policy decision,
- review requirement,
- audit event,
- projection invalidation.

Low-level stores can remain internal repositories, but they should not be the public write API.

## Recall With Claims

Recall should be able to answer from claims, not only item summaries.

Context pack sections should distinguish:

- source-backed claims,
- generated summaries,
- contested claims,
- stale claims,
- scope-limited claims,
- claims excluded by access policy.

## Probe Corrections

A user correction should create one or more candidate claim operations:

- attack existing claim,
- propose replacement claim,
- narrow scope,
- mark answer too broad,
- request source anchor.

High-risk changes require review before belief state changes.

## Self-Regulation And Professor Review

Self-Regulation may submit claim mutation candidates through `IMemoryMutationAuthority` when an assessment, humility trigger, calibration outcome, or professor review identifies a weak claim, contradiction, missing source, wrong scope, or stale procedure.

The ledger must preserve the difference between:

- source evidence,
- generated synthesis,
- professor critique,
- self-regulation warning,
- calibration outcome,
- human review decision.

Only governed mutation commands can change authoritative claim or belief state. Professor review and self-regulation output are challenge/review inputs until accepted by policy.

## Tests

Required tests:

- two claims inside one memory item can have different belief states,
- contradiction between two claims is visible even if item summary is fluent,
- unsupported generated summary cannot be promoted without source anchors,
- high-confidence recall with attacked claim produces warning/answer gate action,
- scalar-only belief calculation is rejected by contract/model tests,
- user correction creates claim mutation candidate, not direct truth update,
- self-regulation and professor review cannot directly mutate claim truth,
- projection rebuild includes claim ids and belief state payload.
