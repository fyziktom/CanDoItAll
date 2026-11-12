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

## Source-evidence continuation: pinned SPA transport and client cutover

Read-only review, 2026-10-16, of the SPA continuation at
`C:\repositories\CanDoItAll.SPA-concurrent-mutations`. Its generator/asset
preparation is now source-frozen as recorded in
[P02d-spa-preparation](../validation/P02d-spa-preparation.md); the older automatic
5032-fetch description above describes the preceding client, not that prepared
entry point. Generated DTOs and Vue callers remain unchanged. This review ran no
installation, generation, build, tests, application, database operation or scan.

### Exact runtime evidence and retry boundary

`packages/api-client/package.json` and `pnpm-lock.yaml` pin
`@typespec/ts-http-runtime` **0.2.1**, not a version range. The package was absent
from both checkouts' node_modules and the local pnpm package index/cache. The
[published version metadata](https://registry.npmjs.org/@typespec/ts-http-runtime/0.2.1)
identifies Microsoft's Azure/azure-sdk-for-js repository. The
[published 0.2.1 tarball](https://registry.npmjs.org/@typespec/ts-http-runtime/-/ts-http-runtime-0.2.1.tgz)
was read in memory, without installing/extracting it; its computed SHA-512 matched
the lock exactly:
`3zI/d10N4D0L0MQVNTLp9PhhY49Wtg9iYZcb+rWxA4Ii9eJigpamCXjGaH7SkrhQ4+5w++ad7TP64tQzUvRDQA==`.
The following references are paths inside that exact package, under
`dist/browser/`; they do not rely on the repository's moving main branch.

| Boundary | Observed 0.2.1 behavior and decision |
| --- | --- |
| Default policy | `constants.js:4`, `policies/defaultRetryPolicy.js:17` and `policies/retryPolicy.js:19` give three retries after the initial send, at most four sends. There is no HTTP-method/idempotency filter: POST/PUT participate. |
| Disable SDK retries | Construct a dedicated protected-definition `CanDoItAllClient` with `retryOptions: { maxRetries: 0 }`. Its generated context forwards constructor options unchanged to `getClient`; the retry loop exits after the initial response/error before evaluating strategies. Keep ordinary feature clients outside this narrow cutover. |
| Per-operation control | Runtime `client/common.d.ts` RequestParameters/OperationOptions have no retry override. The existing generated `helpers/interfaces.ts` is narrower still: only `operationOptions.onResponse`. Generated `saveProject` forwards neither cancellation nor other request settings. Do not invent a per-call `retryOptions`, cast it through, or mutate a shared pipeline around an await. |
| Status retries | `retryStrategies/exponentialRetryStrategy.js:43` retries 408 and statuses >=500, except 501/505. `throttlingRetryStrategy.js:26` handles 429/503 with valid `retry-after-ms`, `x-ms-retry-after-ms`, or `Retry-After` seconds/date. A 429 without a valid header is not retried; a 503 without one still gets exponential retry. Ordinary 400/401/403/404/409/410/413/415 are not SDK retries. |
| Network retries | `exponentialRetryStrategy.js:53` recognizes RestError codes ETIMEDOUT, ESOCKETTIMEDOUT, ECONNREFUSED, ECONNRESET, ENOENT and ENOTFOUND. Ordinary browser Fetch TypeError becomes REQUEST_SEND_ERROR in `fetchHttpClient.js:149` and is not retried by this version. AbortError/non-RestError also exits. Do not describe all browser network failures as retryable. |
| Redirects | `createPipelineFromOptions.js:38` adds its redirect policy only in Node. Browser `fetchHttpClient.js:48` does not set Fetch redirect mode. Therefore `redirectOptions: { maxRetries: 0 }` cannot disable browser redirect replay, even with SDK retries disabled. |

Use the supported constructor `httpClient` seam for a small protected JSON
transport which invokes Fetch once with `redirect: "error"`, same-origin endpoint
validation, the existing same-origin credentials behavior, and no refresh/retry
loop. Bound request/response bytes and reject unsupported body shapes; preserve
the immutable serialized body. This remains an adapter under the generated client,
not hand-written replacements for generated request DTOs. Require
`retryOptions.maxRetries = 0` as well, since the generated pipeline still wraps the
transport. A direct app dependency on the already-pinned runtime is appropriate
if the adapter imports its public HttpClient/createHttpHeaders types/functions;
do not depend on pnpm exposing a transitive package accidentally.

Fetch can otherwise preserve and resend a POST/PUT body on 307/308 (and PUT on
301/302). Redirect rejection stops that path. The browser also owns HTTP
authentication/proxy handling and the specified 421 new-connection retry. Neither
this runtime setting nor a one-Fetch adapter proves one physical HTTP attempt.
The durable original operation plus owner receipt is the duplicate-effect
boundary. [Fetch HTTP redirect and network algorithms](https://fetch.spec.whatwg.org/#http-redirect-fetch).

`client/sendRequest.js:80` serializes a JSON object once before entering the
pipeline. `retryPolicy.js:30` repeatedly passes that same PipelineRequest; it does
not create a fresh operation ID. Function/stream bodies are unsuitable for this
bounded definition transport. Response JSON parsing happens after the retry
pipeline, so malformed JSON does not trigger an SDK retry; after dispatch it still
requires retained uncertain evidence rather than a new identity.

`client/clientHelpers.js:18` adds authentication only when a credential is supplied.
Each policy in `policies/auth/` calls `next` once; bearer/OAuth2 first calls the
supplied credential getter. These built-ins do not implement a 401 challenge loop.
Additional policies, a custom credential getter or a custom transport can introduce
their own behavior and must be reviewed explicitly. Current `src/lib/api-client.ts`
supplies no credential/auth policy and no retry configuration. Browser transport
uses `same-origin` credentials by default (`fetchHttpClient.js:48`); this does not
mean cookies alone satisfy configured API bearer authorization. Preserve current
authentication semantics, use server-returned caller binding, never persist bearer
tokens, and do not add automatic login/token-refresh-plus-write replay. A later
credential identity change must not rebind a retained operation.

### Observed editor lifecycle and minimal ordered file changes

Both writers currently own their state directly in `<script setup>`; there is no
Projects draft/session composable to extend. `ProjectNewPage.vue:45` treats any
failure as editable again and navigates after an unfenced delayed success.
`ProjectOverviewPage.vue:88` captures projectId once, loads only on mount, and has
no retirement/route-update fence. Its save passes live child arrays, then `load`
overwrites all fields; attach/detach also calls this combined definition/hierarchy
load. Phase date inputs immediately convert raw text to Date. AppShell renders an
unkeyed RouterView without KeepAlive; `useAppTabs.ts` persists navigation metadata,
not drafts. It is not a pending-operation store or a sufficient lifetime hook.

After actual emitted schemas and generated serializers pass their gate, the
smallest coherent implementation set is:

1. Regenerate `packages/api-client/src/**` from reviewed input. Verify all six
   operations and their real emitted names/types before writing wrappers. If the
   new generator still omits AbortSignal forwarding, add that narrow deterministic
   generator repair plus fixture proof; never patch generated output by hand.
2. Use the hand-owned API-package transport in
   [the refined transport card](P02d-protected-json-transport.md), keeping its existing
   direct SDK dependency and dedicated no-retry client. Add
   `packages/app/src/lib/projects/project-definition-api.ts` for the six typed
   generated operations and validated retained response. Keep outcome codes and
   discriminators; do not route these through existing generic `unwrap` or add an
   app-owned second transport. Constructor dependencies are explicit for tests;
   no process-global Fetch patch or additional dependency is required by that card.
3. Add `project-pending-store.ts` beside them: typed, bounded transport around the
   verified `virtual:project-pending-store-url`, with exact CAS results and no
   IndexedDB algorithm fork. Keep the opaque HTTP pending representation versioned,
   including string epochs and lossless timestamp text. Native recovery's exact
   numeric-epoch frozen representation is different: do not claim cross-client
   body interchange. Native LocalWorkspace and HTTP OpenApi/authenticated caller
   scopes differ; old-binding metadata-only inventory/disposal must remain usable
   without decoding another client's body. Do not change storage keys to evade
   capacity or discard old evidence.
4. Add `packages/app/src/composables/useProjectDefinitionEditor.ts` containing the
   typed draft/submission/acceptance state transitions, route-generation fence and
   disposal. Use original context from open, immutable bounded capture, applied
   dispatch claims only, sticky uncertainty, explicit checks/retry and new
   current-based reconciliation. Preserve live raw date text and post-dispatch
   typing. Disposal retires UI effects, not durable evidence; no automatic resume.
5. Change the two existing pages to use that composable. A watched route identity
   must retire the old session before opening another; unmount must do the same.
   Split hierarchy/access refresh from definition replacement in Overview. Create
   needs an explicit accepted/reconcile/navigation outcome and must not navigate a
   retired session or erase later typing. Both pages need reachable pending and
   metadata-only disposal controls; use one small Projects renderer,
   `packages/app/src/pages/projects/components/ProjectDefinitionRecovery.vue`, with existing
   controls. Remove the now-unused legacy save/editor wrappers from `api.ts` only
   after both callers migrate; leave separate hierarchy/deletion semantics honest.

No router-wide draft framework, app-tab persistence expansion or new store engine
is needed. The generated HTTP status set must include accepted 200/201, typed
rejections, typed unconfirmed 503 and the separate lookup variants. An unmodeled
proxy/server/transport error after a claim remains uncertain; a bare HTTP code is
not an owner rollback proof. Restored browser acceptance is untrusted until fresh
authorized lookup, and accepted historical receipt is separate from a current
definition read.

### Concrete proof seams for the next source slice

- `packages/api-client/test/project-definition-pipeline.test.ts`: instantiate the
  real generated class using its public `httpClient` constructor option. Capture
  method, URL, headers and final serialized body; return controlled PipelineResponse
  values or RestError codes. For each protected write, assert one transport call
  for retryable status/header/network arrangements with maxRetries zero. Use one
  bounded positive control with retries enabled to prove the counter observes
  actual pipeline sends. A generated onResponse callback runs after the pipeline
  and is not an attempt counter.
- The same generated boundary must prove maximum epoch text, original expectation,
  complete versus absent arrays, nullable fields, persisted row IDs, full/scalar
  response distinction, callerBinding lookup query and lossless accepted receipt
  text. The installed SDK parses JSON before generated onResponse; that callback
  cannot capture original raw text. Use the API-package transport's retained raw
  response before parsing, then validate the reviewed stable Receipt representation.
  Callback/presentation failure must not erase already validated acceptance. No
  canonical owner digest is reimplemented.
- `packages/api-client/test/protected-json-http.node.mjs`: inject a Fetch
  delegate into the real adapter, assert at most one application invocation and
  redirect/error/credentials/body settings, pre-abort refusal and bounded parse
  failure. Separately exercise 307/308 against real isolated browser routes: the
  redirect destination must receive no request. Mock responses alone cannot prove
  browser redirect behavior.
- `project-pending-store.test.ts` and
  `packages/app/src/composables/useProjectDefinitionEditor.test.ts`: deferred owner
  responses, storage prepare/claim failure, accepted-before-store-failure,
  post-dispatch typing, route reuse/unmount, two callers racing one claim, unknown
  then rejected/NotObserved, closed/expired/draining distinctions and original
  binding change. Browser restart/multi-tab tests must use the unchanged packaged
  module, actual durable evidence and fresh receipt authorization.

These are proposed paths and arrangements, not authored/discovered/passing cases.
During this review the parent reported the existing 20 preparation cases passed
(20 passed, zero failed/skipped, 0.200 seconds wall time; TAP 131.567 ms), with
unchanged 21 source inputs and zero leftover temporary roots. Parent-owned
evidence is `validation/p02d-spa-preparation` under the main ignored artifact tree;
this reviewer did not execute that lane. The next slice's exact count will come
from its authored test rows. Actual OpenAPI generation, TypeScript roundtrip,
browser redirect and end-to-end SPA recovery proof are still open.

## Next API boundary: exact wire, dates and frozen submission

Source-only continuation, 2026-10-16, after the dormant transport review. The
transport's four SPA inputs, the existing 18/20 inputs, the index and signing
configuration remain frozen. This section changes only this managed-engine
readiness document. No client generation, install, server, database or API/Vue
source work was performed. The backend's actual emitted-schema proof remains a
prerequisite; the tables below describe inspected source, not a passing wire test.

### Inspected HTTP shape

Authoritative files are Web `Api/ProjectDefinitionApi.cs`,
`ProjectDefinitionHttpResponses.cs`, `ProjectDefinitionHttpBody.cs`,
`ProjectDefinitionOperationOpenApiSchemas.cs` and
`ProjectOperationEpochHttpContract.cs`, plus Projects.Contracts
`ProjectDefinitionOperations.cs`, `ProjectDefinitionContracts.cs`,
`ProjectDefinitionOutcomes.cs` and `ProjectEditorAcknowledgement.cs`.

All three write bodies are `{ operation, version, command }`. Operation has
`namespace` UUID string, canonical decimal-string `epoch`, UUID `id` and exact
`callerBinding` string. Request version is `{ schema, normalization }`, currently
`{ schema: 1, normalization: 1 }`; it is different from result version `{ value: 1 }`.

| Operation | Exact command/read boundary from source |
| --- | --- |
| GetProjectDefinitionWriteContext | Namespace, caller binding, epoch, admission/replay timestamps, supported request/result versions and six enforced limits. No mutation authority may be inferred merely from reading it. |
| GetProjectDefinition | `{ expectation, scalars, externalIdentity, phases, options, writeContext }`. Namespace/caller are compared before and after the coherent read. Expectation is `{ project: { databaseProfileId, projectId, lifetimeId }, revision }`. Stored child IDs are nonnull. |
| CreateProjectDefinition | Command `{ scalars, phases, options, externalIdentity? }`; complete explicit arrays are required, externalIdentity may be absent/null. First acceptance is 201; exact replay is 200. |
| ReplaceProjectDefinition | Command `{ expected, scalars, phases, options, assignExternalIdentity? }`; required original expectation and complete arrays. Route project ID must equal expected.project.projectId. |
| UpdateProjectDefinitionScalars | Command `{ expected, scalars }`; no child arrays or external-identity assignment. It preserves children. |
| GetProjectDefinitionOperation | Original namespace/epoch/operation ID in path and original callerBinding query. Fresh current read permission is required; it does not reacquire expired mutation authority. |

Scalars are name, description, objective, status, currentPhase and nullable
targetDateUtc. Phase inputs contain nullable ID, name, goal, status and nullable
startDateUtc/endDateUtc; options contain nullable ID, category, optionName and
notes. The current Projects enum values are numeric, while response `status`
discriminators are literal strings. Verify these in emitted schemas rather than
inferring an enum naming policy from unrelated API families.

Successful write body is `{ status: "accepted", receipt, replayed }`.
Receipt is `{ operation, version, kind, acceptedAtUtc, replayUntilUtc,
acknowledgement }`, where kind is Create=1, Replace=2 or UpdateScalars=3. Full
acknowledgement is **flattened**: definition expectation, scalar fields, phases,
options and externalIdentity. It has no serialized `project` shortcut (that C#
property is JsonIgnore). Each full child acknowledgement carries submittedIndex
and ID. Scalar acknowledgement instead has `{ definition, scalars,
externalIdentity }`. Never decode one as the other.

Known write rejection has status rejected, nullable operation and nonempty errors;
source maps it to 400/403/404/409/410/413/415 as applicable. Unconfirmed has original
operation and reason, with 503. Early current-principal denial can instead return
the common ApiErrorResponse at 403; the schema transformer explicitly advertises
that union. Do not manufacture an owner rejection/rollback proof from that generic
error, an unknown proxy response or a bare HTTP status.

Lookup statuses are foundFull/foundScalars with the same Receipt type; notObserved
with Open=1 or Draining=2; closedAbsent with closedAtUtc/observedAtUtc/retainedUntilUtc;
expired; datasetChanged; callerContextChanged; invalidContext with reason; and
detailUnavailable with reason. Found/notObserved/closedAbsent use 200; expired
410; dataset/caller/invalid context 409; detail unavailable 403/404/409 by reason.
Current access denial can again be a common 403 error. NotObserved, expiry,
changed binding and unavailable detail do not resolve previous uncertainty.

### Decision: protected timestamp fields remain strings

Existing generated `jsonProjectPhaseEditorModelToApplicationTransform` calls
dateDeserializer, and its transport counterpart calls dateRfc3339Serializer.
The same occurs for targetDateUtc. Converting through JavaScript Date can discard
sub-millisecond ticks **before** the command is frozen. Preserving receipt text
alone therefore does not protect an untouched phase during a full save.

Choose a narrow, deterministic **codegen-only** treatment of the protected
family's date-time schema nodes so its generated models and transforms use
strings. Do not change the backend contract or all client dates globally. After
actual schema proof, enumerate the protected operation graph's date-time fields:
context deadlines, definition/command/acknowledgement target and phase dates,
Receipt acceptance/replay dates and closed-absence dates. Preserve requiredness,
nullable branches and all other constraints; omit date-time format only in the
prepared generator document, retaining a marker identifying the original date
contract for validation/review. The reviewed original OpenAPI remains intact.

Before mutation, prove those selected nodes are not shared by an unrelated
operation. If sharing or an unexpected node is found, stop that generation and
review a targeted schema clone/name mapping; do not silently change unrelated
models or introduce a recursive global date rewrite. Require the emitted protected
models and serializers to contain string/null dates and no Date conversion for
the enumerated fields. Fixtures must include explicitly typed nullable branches.

The API layer validates those generated strings as owner-supported UTC timestamp
text, brands the validated text locally, and keeps it unchanged. A display Date
may be derived, but is never used as the source of a write. This avoids casting
strings into Date types or substituting dates after a submission is frozen.

The draft keeps, for each date field, the original validated wire value and the
editable date-only text. Phase values are associated with persisted child ID
(or a stable local row identity before creation), not current array position.
When displayed text still equals the original date-only display, submit the exact
original string, including its fractional precision. Explicit clearing submits
null. An actual new date is strictly calendar-validated and intentionally encoded
as midnight UTC; invalid raw input remains in the live draft and blocks capture.
Do not parse untouched text through new Date or normalize it with toISOString.

Required precision arrangement: inject a coherent-read wire fixture with phase
start/end timestamps such as `2026-10-16T12:34:56.1234567Z`, edit only project name,
then build/freeze/send a **full replacement**. Assert the untouched phase strings
are identical through generated read transform, draft, generated request transform,
retained request and final HttpClient body. Repeat with row reorder and null dates.
This proves the client path; a controlled seven-digit fixture is not a claim about
PostgreSQL's stored timestamp precision. The real HTTP fixture must separately
assert preservation of the exact timestamp text its coherent GET actually emits.
Post-dispatch typing, explicit date edits and date text reverted to the original
display need separate draft assertions.

### Decision: freeze generated request bytes and guard dispatch equality

Put the hand-owned typed API boundary in
`packages/api-client/projects/project-definition-api.ts`, with an additive package
subpath export. It can import the actual generated request types and their pure
transport transforms within the owning API package, and reuse
`transport/protected-json-http.ts` without adding a transitive app dependency.
This replaces the earlier provisional app-local HTTP/SDK wrapper path; the app
will consume the public Projects boundary from its editor composable.

After typed shape/bounds/UTC validation, call the actual generated **pure** request
transform and JSON.stringify its result once. Retain that exact HTTP string with
the original operation, supported request version, command kind and route target
in a versioned browser frozen-submission representation. Check the completed
representation against the pending store's independent 2 MiB bound, including
escaping/wrapper overhead, before prepare. No authentication getter, client
operation or network pipeline runs merely to discover serialization during freeze.

After an applied durable dispatch claim, decode the retained representation with
the same bounded supported-version validator and pass its typed command to the
corresponding generated method. Compose a small request-checking HttpClient above
the existing attempt's HttpClient: before delegation, require the expected method,
route target and **exact serialized body equality** with the retained string.
It must not rewrite the request or bypass the underlying one-send guard. The SDK
still owns authentication/headers and supplies the final serialized body. Local
mismatch causes no Fetch and does not erase older uncertain evidence.

Do not pass the frozen string directly as SDK JSON body: 0.2.1 JSON.stringify
would turn it into a JSON string literal. Do not compare only parsed semantic
objects, compute the owner's digest or restore a changed operation ID. If a later
generator version cannot reproduce a retained schema-one body's exact bytes,
disable its retry and allow authorized lookup; support that retained encoder
explicitly before re-enabling retry. Do not silently migrate pending payloads.

### Decision: versioned stable Receipt representation, not envelope text

Read the attempt's bounded complete raw text before using generated model objects.
Use ordinary JSON.parse, then explicit bounded typed validation of the top-level
case, receipt operation/version/kind and exact acknowledgement-to-submission
mapping. Validate full phase coverage, distinct/ranged submitted indices, persisted
ID matches, permitted omitted blank options and advancing revision. Construct the
validated Receipt value in a fixed field order with unchanged timestamp/epoch
strings. Serialize it inside a named client-format version-one wrapper. This is a
browser storage representation, not the owner canonical codec or a new digest.

Exclude status/replayed and other envelope-only data. Reordered envelope fields
must not affect it; unknown result versions or incompatible fields fail explicitly.
Write and found lookup both pass through the same validator/constructor. Prove
their resulting stored strings are exactly equal with the actual HTTP serializer,
including seven-digit fractions, full/scalar variants and historical acceptance
after a later current edit. No relaxed equality or timestamp normalization is
permitted. If actual wire proof cannot establish this, request an engine-owned
stable Receipt wire representation before activation.

Store known validated acceptance in session memory before browser persistence or
presentation. SDK parse/callback/model conversion failure must not hide an already
captured valid raw Receipt. A restored stored Receipt remains untrusted until fresh
authorized lookup. Coherent current GET and historical accepted Receipt remain
different values; never replace one with the other during recovery.

### Narrow cancellation repair and ordered source slice

The existing generated helper's nested operationOptions contains only onResponse;
that callback receives an already parsed PathUncheckedResponse. Add only an
optional abortSignal to that helper, preserving the callback type and behavior.
For the six protected generated operations, add
`abortSignal: options?.operationOptions?.abortSignal` to their actual request
options. Do not replace this nested type with runtime OperationOptions wholesale:
the runtime onResponse signature and invocation point differ. No arbitrary
headers, endpoints, retry options or body override are added to operation options.

Make this a deterministic generator repair with exact expected operation names
and one verified request-options site per method. Missing, duplicate or changed
emitter structures fail generation; already-repaired fixtures are idempotent.
Preserve nullable/optional syntax and unrelated methods. The existing generator
owns TypeScript, so a scoped AST inspection/text edit is available without a new
dependency; no broad source reformat or hand-edited generated output is needed.

Apply the same editor cancellation signal both here and to the attempt. This lets
SDK bearer/OAuth credential getters receive cancellation before reaching transport;
the transport still blocks a late send when a getter ignores the signal. Add an
actual installed SDK fixture whose credential getter observes the forwarded signal.
Cancellation after dispatch remains uncertain; it is not rollback evidence.

The next authorized source order is: actual backend schema gate and reviewed local
input; generator date/cancellation helper plus pure fixtures; deterministic client
generation; hand-owned typed Projects API boundary plus generated-pipeline tests;
only then Vue draft/session and native-style recovery surfaces. Proposed helper
path is `packages/api-client-generator/scripts/protected-project-client.mjs`,
called by the existing prepare/generate flow; actual generated identifiers must
come from the reviewed candidate, not this C# inventory. New API and generator
test counts must be derived from authored rows before execution. This section
does not claim that those sources or proofs already exist.
