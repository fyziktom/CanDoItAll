# Exact inherited Agent execution admission

Implemented with [73 distinct passing selected behaviors](../validation/2026-10-19-exact-inherited-admission.md)
across an exact test-only bridge. The admission foundation is ready for the concrete
Process/Workbench integration below; it does not activate those producers.

Continuation of the inherited journal checkpoint. The next owner must turn one already
enrolled Process child into one durable Agent execution. It must not use the legacy
same-source reservation scan: that store enumerates lineage and requires recoverable
generic-tool journals, while inherited planning is an effects-only journal with a stable
child occurrence.

## Owner and boundaries

Add a cohesive Core admission owner using the existing execution-run reader/writer and
cross-instance dispatch lease interfaces. The business adapter supplies the deterministic
execution ID, original-parent validator and deferred preparation callback. The owner
acquires only that execution's lease, performs exact ID lookup, and returns the original
run before invoking preparation when it exists. No history enumeration, shadow index,
new store interface or runtime partial file is needed.

The existing FileSandboxWorkspaceStore remains the transaction owner for initial
run/usage/index publication and recovery. Reuse its generic new-run commit journal,
including interrupted initial commit recovery on exact lookup. Do not add a second
filesystem transaction protocol.

Core validates the inherited schema, session/run identity, new-run state and retained
runtime contract. The required business validator checks the original parent enrollment,
profile/namespace, material, allocation, target and request against its canonical owner.
It runs on both new and retained records. Returning a retained record does not authorize
dispatch or manufacture a new claim; the live owner still admits current source and
original work state. Uncertain/open work is returned unchanged.

Preparation includes the selected provider runtime contract and actual workspace scope.
Add an explicit inherited variant to AgentPlanningRunMetadata before constructing the
background journal, so its existing execution fingerprint binds the metadata. Preserve
interactive v2 bytes and governance validation. The inherited variant never fabricates
interactive authority. Read validates the matching journal origin and source/run identity;
actual current provider options are still compared by MAF against the saved contract.

## Ordering and failure

1. Validate the exact nonempty execution ID and required callbacks.
2. Acquire its existing cross-instance dispatch lease.
3. Exact lookup, including the file owner's pending-commit recovery.
4. If retained, validate the original record and return it without preparing input,
   origin, grant, journal or provider parameters.
5. Otherwise call deferred preparation, validate the new candidate, bind its inherited
   runtime metadata, create the source-authorized inherited journal, validate against
   the parent, and publish through the existing canonical store.
6. Release the lease. A save or acknowledgement failure is propagated; retry resolves
   the same ID. Preserve a primary failure if lease release also fails.

No provider call happens within admission. The lease does not cover another child.
No SQL root lock is held while preparing input or writing the Agent file journal.
Parent source/generation checks before dispatch and the eventual enlisted effect fence
remain necessary because SQL and file publication are not one atomic transaction.

## Proof and progression

Use real file stores and independent owner instances: concurrent same-ID admission must
invoke preparation once; an unrelated child must progress; every initial commit boundary
must recover the original decision/material/grant/runtime contract after acknowledgement
loss; denial or mismatched input must leave the original unchanged. A retained open work
interval must never become a new allowance. Instrument physical JSON reads to show exact
lookup without unrelated execution-history reads. Test cancelled lease acquisition and
explicit schema/runtime-contract substitution, plus unchanged interactive metadata.

Then connect this owner to the actual Process adapter and Workbench inherited participant.
This admission primitive does not lift executor guards or complete manager/Workflow
publication. Existing Agent snapshot after snap-20261020000039-926561d8 is the architecture
baseline; check dependency direction and changed owner responsibilities again at closure.
