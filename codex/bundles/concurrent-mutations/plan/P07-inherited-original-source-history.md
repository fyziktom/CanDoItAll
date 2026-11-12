# Original inherited Process source for historical reads

The existing selected dispatch query intentionally joins current assignment and requires
the original policy/readiness. It cannot supply stable historical evidence after an
assignment repair. Recovering a receipt must resolve the original accepted admission and
must never grant a new dispatch. This slice precedes product inherited receipt activation.

Extend the existing `IProcessOriginalReceiptAuthorityReader` with an explicit selected
decision method. Its existing adapter delegates to the existing Persistence query owner
and checks the canonical database profile. No new interface, registration, project edge
or runtime partial is required. Ordinary receipt behavior remains on its existing method.

Resolve the exact runtime and supplied original claim without joining current assignment;
require the accepted prepared launch and its original assignment. Check original Agent,
step, plan/hash, Project lifetime and execution creation within the original claim interval.
Then validate the exact retained child/admission/grant and material scope/stamp. A missing
accepted admission fails explicitly; current assignment cannot substitute for immutable
source evidence. Current cancellation/completion, claim expiry/release, child closure or
generation supersession do not erase original evidence. Return `ObservedCurrentDispatch`
false in every case. Current read authorization and manager authority are separate owners.

Reuse accepted-launch validation and source snapshot construction inside the existing
query. Preserve exact JSON property names/order, digest domains and ordinary wire bytes.
Extract the original child/policy/material validator from the existing dispatch binding;
live dispatch additionally checks unchanged current policy/readiness and current claim.
Four bounded indexed reads for prepared launch history, without material payload or
lineage/sibling scans. No transaction, lock, write, provider invocation or retry is needed.

Prove persisted original source survives assignment executor/readiness/policy repair,
claim expiry/release, child closure and supersession without dispatch. Reject substituted
evidence, parent/profile/namespace/contracts/material and invalid claim creation times.
Compare original live and historical source fingerprints and verify the fingerprint
expression against the preceding committed source after only its local variable rename.
Preserve PostgreSQL timestamp wire precision when reconstructing the original assignment:
Npgsql truncates ticks relative to its 2000 epoch, including pre-epoch values. Cover both
sides with actual SQL round trips. See [Npgsql's encoder](https://github.com/npgsql/npgsql/blob/v10.0.0/src/Npgsql/Internal/Converters/Temporal/PgTimestamp.cs)
and [timestamp mapping](https://www.npgsql.org/doc/types/datetime.html).
Assert query bounds with sibling history and inspect index plans.
Repeat selected dispatch and ordinary historical source regressions. Build affected
production projects and Integration in standard Release; exact discovery, full portability,
architecture comparison, frozen source/runtime, observed execution and resource closure
remain required. Core schema 7 historical reads, product provider/producer and manager
activation are subsequent integrations and are not widened by this query-only slice.
