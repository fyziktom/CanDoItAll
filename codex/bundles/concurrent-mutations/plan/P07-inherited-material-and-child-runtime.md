# Inherited material and child runtime

Refinement after the [versioned execution contract](P07-process-execution-contract.md).
The first [pure material value contract](../validation/2026-10-19-process-inherited-material-value.md)
now passes 44 selected cases. Storage/runtime integration below remains prospective;
no activation is claimed. The original E2E scope
still includes both RunSnapshot and StepSnapshot for supported Process/Workflow work.

## Current code constraints

The Process generation owner currently retains a material ID, not a canonical material
payload or parent material scope. Its publication condition fences one decision. Local
budgets belong to a real run/step and cannot represent a shared run allowance by inventing
a synthetic step. A RunSnapshot spanning several decisions therefore still needs a real
parent owner; neither the material ID nor an inherited numeric ceiling supplies one.

Agent planning is currently interactive. Its admission requires the original chat
authority; its review and publication readers require the original latest chat run.
Refresh updates the actual chat session. The planning journal is schema 6 and its
decision schema 1 rejects basis timestamps before the Agent decision's start. Existing
background journals have their own source policy, retained input and execution fingerprint.
Those checks are deliberate and must remain intact for their existing protocols.

`AgentPlanningDecisionOwner.CaptureBasisAsync` stamps new local capture before invoking
the participant. This is conservative for a new local read, but it cannot represent an
inherited older basis. Routing inherited material through that callback would renew its
age; weakening the timestamp invariant globally would also weaken old interactive
journals. The inherited path needs a required versioned origin and the actual original
capture timestamp, with legacy validation unchanged.

Legacy background reservation calls `ListRunsAsync` under workspace publication
coordination and checks all retained runs in the source lineage. That is unacceptable as
the lookup algorithm for every new governed child. The Process owner already provides
a stable child occurrence ID and exact canonical child query. The new reservation path
must use that ID as the actual child execution ID after authoritative enrollment.
It cannot merely pass a fixed ID into `CreateForNewBackgroundRunAsync`, which correctly
rejects a pre-existing run before reservation. Recovery must inspect the original child
before preparing any new provider input or journal.

## Ownership decisions

| Owner | Responsibility |
| --- | --- |
| Process Runtime/Application | Original run/step material scope, current scope generation, child enrollment and policy/allowance inheritance; neutral immutable references and explicit orchestration. |
| Process Persistence | Canonical scope/material records, bounded exact queries, shared root fence and atomic generation/runtime/accounting updates. |
| Workbench | Project task material and its codec, declared covered scope, current permissions, source comparison and business publication receipt. |
| Agent Models/Core/Persistence | Versioned inherited origin, exact durable child input, provider work accounting and journal recovery; no Process or Workbench project reference. |
| Workflow runtime | Its original child occurrence and durable node continuation; all dependent nodes consume the same admitted parent reference. |
| Process/Workbench adapters | Translation between these contracts and composition of source, manager, material and generation checks at the actual effect. |

No cross-store transaction is invented. SQL enrollment precedes the child journal;
retries recover the exact ID. Unknown journal/provider/effect outcomes keep their work
allocation reserved. Parent reconciliation reads closed owner evidence outside SQL,
then compares the exact complete enrolled set under the root fence before advancing.

## Material semantics

StepSnapshot belongs to a logical Process decision, not each transport retry, worker
claim, Agent provider iteration or Workflow node. All dependent work in that decision
uses the same original material. A child cannot add fresh reads for that covered scope.
An explicitly supported additional scope requires separate declared admission; otherwise
the request is refused before provider work.

RunSnapshot belongs to a declared run scope and is captured before any dependent model
work. It can be shared by multiple step decisions and subprocess children within that
scope. The parent scope's generation is distinct from a step decision's generation:
completed decisions remain historical when unfinished work refreshes. A step-local head
cannot serve as the parent run scope, and current assignment count is not proof that
all affected children are closed.

The canonical material record must preserve its ID, scope owner, contract ID/version,
original capture time and bounded owner-encoded content. Persist one parent copy, not a
copy in every Process state/command receipt. Child input binds the material identity,
timestamp and semantic digest. Whether the Agent journal retains one bounded copy or an
owner reference must be decided against actual restart availability and measured journal
cost; it must not silently replace a missing payload with current source reads.

The Workbench source limit is currently 1 MiB encoded material; Agent provider input is
bounded at 2 MiB and the entire journal at 24 MiB. Maximum-size input copied into 64
children costs up to 64 MiB per parent generation before journal overhead, and 32 retained
generations can multiply that substantially. Do not call that capacity-qualified from
the per-record bounds. Retained material must not be removed while original child
journals or effect receipts need it for historical interpretation.

The first material value contract is deliberately independent of storage and Agent
journals. Its scope binds database profile, workspace namespace, root run, owning run
and optional logical step. A null step means a run scope; an empty step is invalid.
Its immutable reference binds scope ID, positive bounded generation and material ID.
The original executor contract, UTC capture timestamp and bounded strict UTF-8 JSON
payload are sealed together by a versioned binary SHA256 fingerprint. Each field has a
fixed-width or length-delimited encoding, so concatenation cannot alias another scope.
Freshness compares the supplied current UTC time with the original capture time; there
is no child-start timestamp or recapture fallback. A newer parent observation requires
an exact original-material comparison before continuation. This pure contract is not a
permission check, scope-membership proof, current-head read or publication fence.

## Refresh and budget authority

Under the existing root transaction lock, a shared scope first enters Reconciling.
Every covered publication path must check that parent scope in its actual effect
transaction. This immediately blocks later effects across all affected decisions,
even when an individual child still has an active local journal. An effect which already
holds the fence may commit first and its accepted receipt remains historical.

Gather the bounded affected child set and its actual owner closures outside the fence.
Reenter, compare the exact scope revision/enrollment set and append new material only
when every affected child has a sealed accepted or definitely-no-effect outcome.
Cancellation, claim expiry and an absent acknowledgement do not close work. Completed
children are not rerun merely because the material scope advanced. Manager retries
resolve their original transition receipt instead of today's head.

The shared parent budget needs a genuine run owner and atomic reservations to its child
step/Agent/Workflow allowances. The existing per-step ledgers remain useful local owners,
but independently initializing each with the parent's full ceiling would double-spend
that ceiling. Capture, orchestration, provider work and reconciliation are charged under
the original admission. Human waiting closes active work without resetting consumed
work. Parent refresh consumes the parent's replan allowance exactly once.

The parent owns Fail/Pause/Replan. A nested child reports changed/expired material and
its sealed outcome to that owner; it never independently refreshes a shared scope or
creates an automatic successor. Any local Agent execution-policy projection must be
explicit and retain the original parent policy for audit, rather than pretending an
Agent preference was the inherited policy. An Agent preference cannot override a parent
RunSnapshot or reset its capture timestamp, maximum age or remaining allowance.

## Implementation order and gates

1. Implement canonical material/scope ownership and root allowance ownership, preserving
   legacy absent data. Do not backfill synthetic material for old opaque IDs. New
   governed contracts must require actual canonical material; missing data is refusal.
2. Bind Process generations and their exact child observations to that parent scope.
   Extend the existing enlisted publication condition to cover parent scope through
   commit. Preserve the established source/Project/dataset-before-root lock order.
3. Add a versioned inherited Agent journal/admission with original capture time and
   parent-controlled continuation. Keep schema 6 and all interactive authority checks
   unchanged. Add exact-ID admission/recovery before selecting provider work; do not
   add another runtime partial or a shadow source-of-truth index.
4. Implement the actual Workbench Process adapter and current manager continuation.
   Compose original source/claim or manager authority, canonical material and publication
   fences. Use the existing business receipt for Process outcome translation.
5. Apply the same occurrence/material/allowance contract through the mapped Workflow
   launch and dependent node continuation, including nested Agent work. Then wire actual
   definition/assignment settings and capability negotiation. An envelope declaration
   alone remains insufficient to enable a setting.

## Required adversarial and performance proof

- Child startup after a long queue uses the parent's original capture time; expiry,
  clock reversal and a forged newer timestamp fail before provider/publication work.
- Sibling decisions/nodes consume identical material; claim rotation, transport retry
  and host restart do not recapture or create another child/provider dispatch.
- A scope refresh racing publication has one ordered winner; late siblings cannot
  publish after Reconciling. Unrelated roots progress while one root is blocked.
- Partial child acceptance followed by cancellation, lost acknowledgement or restart
  retains accepted history and keeps uncertain allocations reserved.
- Concurrent reservations cannot each spend the parent's remaining total. Durable
  settlements preserve overruns and cannot re-credit another child's allowance.
- Actual source revocation, Project/dataset replacement, changed manager authority and
  missing inherited material remain explicit refusals in both Agent and Workflow paths.
- Old writers reject the new persisted versions. Legacy exact bytes, migration,
  restoration and downgrade refusal are tested with retained data.
- Query plans show exact indexed child/material lookup; no workspace/run-event history
  enumeration occurs in governed admission or publication. Measure p50/p95/p99 and
  allocation/journal growth against matched legacy workloads before activation.
- Run real provider-adapter tests with controlled responses, real PostgreSQL/file-store
  restart and actual manager UI/API continuation, then the required static and final
  broader gates. Pure state/codec tests alone cannot close this feature.
