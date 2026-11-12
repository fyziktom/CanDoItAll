# P02b protected HTTP boundary

Status: implementation card; no execution evidence yet.

The Web adapter owns HTTP authentication, bounded JSON and concrete response cases.
Projects continues to own normalization, transactions, receipts and current target
availability. No Web, principal, token or transport type enters Projects.Contracts.

The protected write context and lookup require current Projects read permission.
Writes additionally require Projects write permission because their exact result is
also a read surface. Enabled mode resolves the independently verified issuer and
subject through the existing principal resolver. Credential ID, expiry and changing
authorization stamp are checked for current access but excluded from durable caller
identity. Failed authentication never becomes OpenApi. Disabled mode explicitly
selects OpenApi; native LocalWorkspace remains a separate caller partition.

The definition read retains its existing snapshot fields and adds writeContext.
Read context before and after the coherent definition read; require equal namespace
and caller binding. Epoch rotation alone is allowed. A draft must retain this scope:
fetching a new namespace at save time cannot make a pre-restore draft current.

POST /api/projects/definitions, PUT /api/projects/{id}/definition and its /scalars
variant accept the existing strongly typed operation envelopes. Route/body targets
must agree. Read the raw body with a hard schema-one byte ceiling, then scan JSON
depth, strings and collection counts before constructing immutable commands.
Historical exact retries use schema-one transport limits; smaller current policy
limits are enforced by the owner only after checking an existing receipt.

Transport replies use concrete closed cases, rather than serializing a base record
and accidentally omitting derived fields. Accepted carries the exact owner receipt
and replay flag; rejected carries operation and stable errors; unconfirmed is 503
with the original operation. Lookup distinguishes found full/scalars, not observed,
closed absent, expired, changed dataset/caller and unavailable detail. Error status
alone is never evidence that an earlier dispatch rolled back.

The old GUID-only save and tokenless Project Structure writes will explicitly refuse
once the native and governed callsites have moved. Do not disable test seeding by
silently inventing operation identities in a legacy facade. Audit remaining runtime
callers and update fixture construction explicitly at final cutover.

Proof: real routed JSON create/replace/scalar/lookup, stale and changed-payload
refusal, exact historical replay, malformed/missing/contradictory identity, chunked
body ceiling and array/string limits, enabled issuer/subject identity across token
renewal, read/write revocation and disabled/secure caller partition changes. Existing
definition read compatibility is separately checked. Run discovery before execution.
