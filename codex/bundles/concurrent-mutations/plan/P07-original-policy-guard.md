# Original Process policy must survive current assignment repair

Read-only adapter review found a concrete downgrade path before activation. In
EfProcessExecutionAuthorityQuery.ReadCoreAsync, original accepted metadata is already
loaded, but ordinary dispatch eligibility checks only the mutable assignment's
DecisionDataPolicy. Removing that field can mark an originally governed launch as an
ordinary current dispatch. The same ReadCoreAsync supplies the native effect guard,
so the correction belongs to this existing owner, not only the adapter's early check.

Capacity proof is retained and released. Require both original and current
DecisionDataPolicy to be absent before ordinary current dispatch can be true.
The selected inherited path still uses its exact original/current policy comparison.
Keep historical observation and fingerprints unchanged; deny reservation and native
mutation through the existing false-current outcome. No new query, type, interface,
reference, cache, schema or runtime partial is required.

Extend the existing Unsupported_decision_policy_preserves_history_but_blocks_reservation_and_owner_mutation
integration theory from four to six cases: policy present at admission, added later,
or removed later, each with/without a declared execution contract. Keep every existing
assertion: same historical identity/source/fingerprint, ordinary reservation refusal,
and refusal inside the enlisted native mutation transaction. Use a typed enum for the
three transitions. A removed original policy must never authorize a new execution.

Also extend Prepared_launch_retains_the_original_policy_Agent_and_readiness in
ProcessDecisionDispatchAuthorityTests with explicit policy removal: exact selected
authority must reject the changed assignment and ordinary authority must remain false.
The existing AdmissionChange enum gains RemovedPolicy; keep the other three cases.

Use the current five-project snapshot as baseline because the preceding capacity
changes are complete before this predicate changes. Build Persistence directly and
refresh Integration; predeclare/discover the exact affected classes, reuse the isolated
fixture, run full portability enforcement and record source/runtime/resource proof.
Capacity evidence is not invalidated: this predicate adds no work to its unrelated
serialization and root-reconciliation hot paths.

This closes the discovered authority hole. Actual adapter routing must still choose
the original accepted policy, never route solely from mutable assignment metadata.
Manager review, cancellation/refresh and Workflow activation remain separate work.
