# Recover predecessor-format runs without inventing authority

## Why a fresh PC3 run is not a legacy test

New direct native launches may lack optional `ProjectAdmission`, but now have a captured
`ExecutableDefinitions` closure. Existing PC3 compatibility tests cover this new shape.
An actually stored pre-PC3 plan can lack the closure as well. Current scope matching rejects
such a project candidate and the outer lookup returns null. Some subprocess callers interpret
null as permission to create a new child. A general migration byte-preservation test does
not prove that an old run remains discoverable or safely recoverable.

## Fixture and provenance

Construct or retain an authentic predecessor serialization through the predecessor serializer/
fixture and schema where practical. Record source SHA, fixture generation and typed hashes.
At minimum prove the payload really omits the new field instead of writing a newly constructed
PC3 closure and setting one convenient flag to null. Use isolated populated-upgrade fixtures;
do not edit ordinary retained records or falsify old plan hashes to match the current model.

Cover both no-admission/no-closure and old runs that do carry reliable existing admission.
A bare public ProjectId or launch variable is not proof of captured project lifetime.
Inventory available authoritative evidence: retained preparation, immutable parent plan,
creation/admission records and actual project lifetime history, with explicit trust boundaries.
Do not assume the model has historical information it never stored.

## Required result semantics

Distinguish: (a) authoritative same-origin retained run, (b) demonstrably another origin,
(c) no matching candidate, and (d) matching historical candidate whose origin cannot be proven.
Use the smallest suitable existing result/error seam; a new public discriminated union is
not mandatory. However, (d) must not flow through a null-coalescing 'launch a replacement'
path or disappear as if no effects ever happened.

For (a), preserve the run/plan/assignment identities and exact stored content; use the
retained plan rather than resolving today's template or publication to display execution
meaning. For (b), do not expose/rebind another project's run. For (c), normal new-launch
policy applies. For (d), fail safely with actionable bounded reconciliation and no automatic
new child, while preserving the historical record. Where evidence exists, do not globally
block all legacy runs simply because checking evidence is harder.

If no authoritative lifetime evidence exists, document that limitation precisely and retain
an explicit safe operator recovery path using existing authority. Do not fabricate it from
current project capture. No blanket backfill to today's lifetime, history deletion, rewrite
of accepted plan hashes, removal of new scope fences or default-template substitution.

## Full-path tests

Run through `FindExistingLaunchAsync` and the actual Workbench/subprocess caller, not only
a predicate test. A controlled lookup can show a filtered candidate, but a duplicate claim
requires the complete caller path and proof that no other existing guard prevents it.

Test active and completed children; blocked/failed children retain their existing stopped-
child behavior. Include repeated parent re-entry, interrupted responses, old preparations,
a restarted host, project deletion/recreation under the same public ID, another project,
another database profile, global versus project and malformed/ambiguous origin evidence.
For safe reuse assert **same run ID, plan ID/hash, assignments and child linkage**, plus no
new database effects. For refusal assert explicit status and no new child, not merely null.
Maintain accepted-run recovery versus preacceptance launch checks; archive is not a reason
to erase an already accepted result.

Watch definitions and profile aliases at the boundary: historical matching must use the
canonical stable identity consistent with the old plan, not a newly case-sensitive shortcut.
Audit adjacent stopped-child and artifact-bridge paths so they do not undo the result.
