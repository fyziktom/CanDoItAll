# SPA protected Projects cutover feasibility

Status: read-only source review, 2026-10-16. No sibling SPA source, generated
client, package, build or test was changed or run. This is a concrete remaining
client perimeter for the end-to-end initiative, not completed protection.

## Current callers and failure modes

| Source under `../CanDoItAll.SPA` | Observed behavior and required change |
| --- | --- |
| `packages/api-client/src/models/models.ts:557` | `ProjectEditorModel` has no original expectation, lifetime, operation or receipt. Generate explicit protected request/read/outcome types. |
| `packages/api-client/src/models/internal/serializers.ts:4229` | Both editor transforms explicitly enumerate fields and drop unknown expectation/operation values. Adding a property only to a Vue object will not send it. Regenerate and test nested round trips. |
| `packages/api-client/src/api/canDoItAllClientOperations.ts:3549` | `saveProject` uses legacy POST and returns a GUID; only 200/400/404 are modeled. New routes need full 201/200 accepted, typed rejection, unconfirmed and lookup result handling. |
| `packages/app/src/pages/projects/api.ts` | `unwrap` reduces named errors to `Error(message)`. Add a Projects-specific typed boundary retaining status, codes, original operation, historical receipt and all lookup variants. |
| `packages/app/src/pages/projects/ProjectOverviewPage.vue` | `load()` assigns every field; save passes live phase/option arrays, then reloads everything. Hierarchy attach/detach also reload the editor. Capture original bound definition, freeze a separate submitted value, reconcile exact accepted rows by submitted identity/index, and preserve later typing. Split hierarchy refresh from definition adoption. |
| `packages/app/src/pages/projects/ProjectNewPage.vue` | Create has no bound namespace/caller capture, frozen operation or uncertainty state; any failure re-enables Create. Acquire context when the draft opens, persist one operation before dispatch, and never replace its identity after an uncertain result. |
| `packages/app/src/lib/api-client.ts` | Same-origin generated client has no explicit retry configuration. Review the installed runtime pipeline and disable implicit protected-write retries; explicit retry must reuse the exact operation and frozen command. |

Only New/Overview call this `saveProject` wrapper in the inspected app sources.
Project Structure's API facade is a separate mutation perimeter; this review
does not certify all node/project lifecycle actions. The existing backend legacy
POST remains enabled with P02a expectation checks and no durable replay promise;
SPA updates presently lose that expectation, while creation can still use the
unprotected legacy contract. Do not mark the pilot's caller cutover complete or
keep that bypass indefinitely after migrating these two callers.

## Browser wire decision before generation

The newly added protected HTTP endpoints are unshipped in this checkout. The
parent selected a **canonical decimal string epoch** as the preferred browser
contract: positive `1..9223372036854775807`, no sign, whitespace, leading zeros,
fraction/exponent or numeric coercion. Namespace/operation IDs remain UUID
strings; caller binding remains the exact `pc1:` value returned by the server.
No JavaScript `Number` or `BigInt` conversion is needed for the key: validate
bounded decimal length/lexical range and preserve the string. This matches the
native IndexedDB key representation already in use.

Currently `ProjectOperationEpochJsonConverter` writes a JSON Int64 number.
Ordinary `JSON.parse` loses precision above `2^53-1`; a generated `number` cannot
silently be used as an operation address. Scope the browser transport change to
the protected HTTP seam and its documented schemas, including context/read,
request, accepted receipt and every lookup reference. Do not change schema-1
owner canonical command/receipt bytes, digests, database epoch values or native
C# invocation contracts as an incidental converter edit. Existing retained
receipts must continue to decode under their original version. The new HTTP
wire tests must assert exact max-Int64 strings and reject numeric/noncanonical
epochs; no released client migration is assumed without confirming deployment.

Generate from a reviewed OpenAPI snapshot of the isolated candidate host. The
generator currently fetches ordinary port 5032 in its default `spec:fetch`, and
`generate` deletes/recreates client output. Do not run that default against the
ordinary app or hand-patch generated models. Export the candidate spec to the
generator's explicit input, review custom-converter scalar shapes and required
collections, then regenerate reproducibly. Preserve the lookup `status`
discriminator and write outcome literal statuses through TypeSpec postprocessing.
The existing `fillEmptySchemas` fallback is not proof that strongly typed IDs
or the new epoch schema have the correct wire shape.

## Minimal state and persistence integration

Use a Projects-specific browser adapter/state module, leaving Vue components to
render typed values and intents. Keep four values distinct: original bound
definition, live draft, frozen submission and exact historical acceptance.
Capture namespace/caller when opening both existing and new drafts. A refreshed
epoch may be adopted before dispatch only within the same original binding.
Explicit current read never retokenizes the old payload; reconcile starts a new
current-based draft with the old draft available for comparison/copy.

Retain bounded frozen request text before first dispatch in a strict transactional
store. Only an applied revision claim may dispatch; no storage fallback, auto
resume, timer retry or new identity on transport failure. Preserve current
OpenApi/authenticated caller separation across login/logout, namespace restore,
tabs and browser restart. Reuse the existing store protocol if the deployed SPA
can consume the exact maintained module; do not silently copy/fork its CAS state
machine or assume Vite's `/api` proxy serves Web `/js` assets. Resolve asset
packaging/version ownership explicitly before implementation.

Keep HTTP receipt data immutable and independent of transport-only `replayed`.
Do not round-trip exact historical timestamps through generated JavaScript
`Date` values before retaining/reconciling the receipt; that loses sub-millisecond
precision. Retain raw protocol data with versioned typed validation, while using
derived display dates separately. The client does not reproduce the owner's
digest or infer acceptance from current state. Browser-stored acceptance still
requires a fresh authorized server lookup before restored body reconciliation.

Apply the existing native uncertainty rules: a later rejection or NotObserved
cannot erase prior uncertainty; ClosedAbsent is distinct from expiry, caller or
dataset change, and unavailable details. Quota failure blocks dispatch. Keep
post-dispatch typing live but state honestly what survives restart. Provide
explicit metadata-only old-binding evidence disposal without claiming rollback.

## Proof needed for the authorized implementation slice

- Generator/transport tests: expectation and child IDs retained; missing versus
  empty arrays; max-Int64 string epoch; all outcomes/status codes; callerBinding
  lookup query; no hidden retry; immutable receipt timestamps and `replayed`
  exclusion. Existing Projects app/API-generator test scripts allow no tests,
  so passing those commands alone would not prove these cases.
- Pure draft/session tests: delayed success preserves later typing, full row
  mapping/omitted blank option IDs, stale conflict without retokenization,
  separate hierarchy refresh, route-change/late-read fencing, no second creation
  after unknown result, binding change and closed/expired recovery distinctions.
- Browser proof against the isolated protected backend: two independent contexts
  conflict, lost acceptance then same-operation recovery after reload, actual
  strict storage quota/abort refusal, same-origin two-tab CAS and exact historical
  receipt versus a later current edit. Preserve ordinary ports/databases.

No source-derived test count is proposed until actual tests are written and
discovered. The shared owner/native tests remain useful regression proof but do
not substitute for the generated SPA serializer and browser client journey.

## HTTP epoch implementation decision

The backend transport slice uses a Web-owned `ProjectOperationEpochHttpJsonConverter`
registered only with ASP.NET HTTP JSON options. The same bounded canonical parser
handles the lookup path's string `epoch`. It accepts decimal strings from 1 through
Int64.MaxValue and rejects numeric JSON and noncanonical spelling. The lookup route
no longer uses a `long` route constraint; invalid epoch spelling returns explicit
400 rather than silently becoming another address. OpenAPI declares both the scalar
and lookup parameter as strings with the same canonical-range pattern. Contract
attributes, owner canonical codecs, persisted journals, SQL values and semantic
digests are unchanged. Focused transport tests will use the actual HTTP converter,
with separate proof that default owner serialization remains numeric. Builds,
discovery, execution and portability-static enforcement remain parent-owned.

The Web schema transformer also describes the two custom-converted operation and
request envelopes explicitly, so the nested epoch is visible to generators. The
operation envelope retains its original namespace, epoch, ID and caller binding;
the request envelope names its operation, interpretation version and existing
typed command. This is documentation of the actual HTTP serializer, not a second
command codec. The nullable rejected-operation member remains nullable.

Source proof now adds **26 cases**: `ProjectOperationEpochHttpContractTests` has
23 theory rows (three lossless/default-codec cases and twenty invalid JSON cases),
and `ProjectOperationEpochHttpIntegrationTests` has three Facts (large future
epoch write/lookup, canonical rejection/no dispatch, and actual generated OpenAPI).
The existing 15 `ProjectDefinitionHttpIntegrationTests` (including the subsequent
canonical-byte boundary regression) and 14
`ProjectDefinitionHttpBodyTests` use matching HTTP client options. The combined
focused filter is therefore **55 expected cases**, pending parent-owned discovery
and execution. The routed large-value proof uses unissued positive epochs and
asserts their exact address in known refusal responses; it does not alter owner
epoch rows or claim those unissued epochs admit an effect.

The full SPA generation gate remains open. In particular, the custom-converted
`ProjectDefinitionExpectation` still needs an explicit schema with original
`project.{databaseProfileId,projectId,lifetimeId}` and `revision`; standalone
`DatasetNamespaceId`, `ProjectCallerBinding` and `ProjectDefinitionRequestVersion`
properties in write context also need their actual scalar/object schemas verified.
The three command shapes must retain required complete replacement arrays and
their nullable scalar/child semantics in generated-client serializer tests. This
epoch slice does not claim that broader generator roundtrip or browser cutover.
