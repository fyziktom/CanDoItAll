# 09 · Agent decision bases, approval and changing authority

## Target CAS is necessary but not sufficient

Consider an agent preparing implementation tasks for a web application. A person changes the objective to an API-only delivery. The tasks do not exist yet, so there is no stale task row to reject. The obsolete fact is the input objective. The proposal must name that material basis, and the publishing owner must reject or re-evaluate it under the agreed freshness level.

A different problem is an automation that reads the new state but keeps applying an old standing mandate. Its content checks can be valid while its behavior is unwanted. This requires a clear intent/authority policy and an explicit human takeover mechanism, not another version field on a task.

## Bounded decision basis

A proposed planning mutation should bind the original invocation/mandate, proposal identity, proposed effect, relevant source observations, target conditions and any approval. For each input include logical owner, scope, object/lifetime identity, revision or immutable version, and the required validity level. Set-valued inputs also need a membership/predicate witness.

Mandatory basis inputs are defined by the tool/owner operation and trusted runtime context. A model may identify additional useful inputs, but it cannot remove the objective dependency from a "plan from objective" command. Prefer the runtime retaining a reference to the captured basis rather than asking the model to reconstruct or hand-copy a technical version vector.

The runtime must distinguish facts actually supplied to the proposing call from unrelated reads performed elsewhere in a long session. Attaching every observed fact creates an enormous and mostly irrelevant basis. Attaching only model-declared facts permits omission. Start with a single well-defined planning operation whose required inputs are known.

## Proposed first planning pilot

1. A governed planning read returns the project definition snapshot and a bounded set of relevant canonical task facts, including the set-membership witness.
2. The runtime creates a proposal against that basis outside any database transaction. The proposal includes exact intended add/update/remove operations, not a replacement of every task.
3. A person may approve that exact effect and its material basis when policy requires it.
4. At publication, the adapter passes the original authority and basis to the owning Workbench operation. Projects facts are validated through a typed Projects-owned port on the protected publication transaction.
5. A changed objective, lifetime, membership witness, permission ceiling or attempt generation rejects publication with a structured reason. The rejection has no partial task effects for the declared atomic batch.
6. A new read and reconsidered proposal produce a new operation/proposal identity. Existing approval is reused only if an explicit policy proves its approved scope still covers the exact new proposal; default is reapproval after material change.

The first pilot should use a bounded batch and existing project gates rather than building a universal distributed dependency service. Its content stamp may initially overinvalidate on harmless project edits. Measure the rate before splitting source revision scopes.

## Source access, decision basis and approval are separate

A valid source lease proves access/authority under its contract, not that the content remained unchanged. A basis stamp proves an input state, not permission to modify the target. An approval proves a permitted reviewed proposal, not continued eligibility of the executing worker. Keep all three as distinct fields/records and reuse the current source authority mechanism (SRC-06/SRC-08).

Do not replace current claim checks with a cached approval flag. Do not allow an agent to create a new proposal that claims an old approval ID without a matching semantic fingerprint and trusted origin. Approval views must show the actual intended change, important dependencies and the actor/automation scope; otherwise a hash merely binds an opaque object the person did not understand.

## Current-at-publication and multiple owners

The validated source and target effect must have the protection described in document 06. Independent source reads followed by an unrelated write transaction are only preflight. Multiple owner contexts can participate on the existing physical transaction, but every conflicting source writer must honor the same scope protocol. Validation uses bounded batch reads, not full Workbench/CRM dashboards. Required inputs are never silently omitted to fit a query limit.

External sources have explicit weaker contracts. A remote page or mutable file read earlier can be recorded by immutable content hash for provenance, but without provider support it cannot be guaranteed to stay current at local SQL commit. A tool must state whether it plans from that pinned observation or needs an external conditional recheck at delivery. Unknown freshness is not a fabricated successful validation.

## Human edits and standing automations

Do not hard-code "human always wins" or "automation always recomputes". Identify fields owned by an active planning policy and expose a clear operation: edit while keeping automation, pin/override a value, suspend a rule, or take over the relevant run. The product policy determines whether a manual change invalidates a proposal, changes the mandate or creates a durable override.

A policy/override may itself have a revision. Preserve only the needed scope: pausing schedule automation must not stop unrelated chat replies. Treat override expiry, resume and ownership transfer as explicit commands with their own admission and expected state. UI warnings should say what future automation remains active, not merely that the current value was saved.

## Late workers and fencing

A cancellation token is cooperative. The data owner must reject publication from a retired attempt even if the worker ignores cancellation or its result arrives after restart. Reuse existing process/agent claim generations where they already supply this fence; do not create an independent shadow generation without reconciling the owner protocol. The generation check is inside the actual write boundary.

For an external effect already delivered, a later cancellation cannot undo reality. Report the committed/external outcome and use an explicit compensation or follow-up. This proposal does not promise hard worker termination or repair unrelated timeout isolation.

## Checkpoints, resume and versioned definitions

A checkpoint may intentionally reference an immutable old definition. That is valid for continuing its computation. Publishing into today's project still validates target state, relevant material inputs and current authority. Old records lacking a new mandatory basis cannot be auto-upgraded by inventing provenance: either remain readable history, require explicit re-admission/replanning, or follow a narrowly proven compatibility path.

Ensure a saved denial does not become an executing mutation on replay. Preserve existing tool journal/effect-evidence semantics described in historical coverage, then verify the actual current implementations before extending them (SRC-14). State-machine compatibility is a separate P07 gate.

## Replanning cost and livelock

Limit semantic replans per operation family and total work budget. Repeated conflicts can consume more LLM time than database checking. Record basis age, invalidating owner/fact category, proposal cost and replan count without private prompt contents. After the agreed limit, preserve the proposal and ask for an operator decision rather than chasing a constantly moving project forever.

A fresh call with new inputs is observable evidence of re-admission, not proof of sound reasoning. The system should claim enforced source/authority conditions, not a formal proof of model understanding.
