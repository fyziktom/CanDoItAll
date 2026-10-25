# P02d — Protected JSON transport

Status: source-reviewed proposal, 2026-10-16. Implementation requires the parent
review. This review read the installed SDK and existing generated source; it ran
no tests, builds, installs, generation, server or database actions. The SPA's
committed preparation and 18-case pending adapter remain unchanged. This card
does not activate either Vue writer or claim a generated protected-client proof.

## Placement and actual public seam

Add one hand-owned module at
`packages/api-client/transport/protected-json-http.ts`, outside generated `src`.
The API package already directly depends on `@typespec/ts-http-runtime` **0.2.1**;
the app does not. Use only that package's public root exports: `HttpClient`,
`PipelineRequest`, `PipelineResponse`, `ClientOptions` and `createHttpHeaders`.
Do not import runtime internals or add a transitive app import. No dependency or
lockfile change is necessary.

Add an explicit `./protected-json-http` package export pointing to the new module
and preserve `.` as `./src/index.ts` for types and default import. Current consumers
use the root entry; inspect consumers again before closing this manifest change.
The existing cleanup helper restricts generation to physical `api-client/src`, so
it cannot erase the hand-owned transport. Do not modify generated `index.ts`.

The installed source establishes these boundaries:

| Source under `packages/api-client` | Observed behavior |
| --- | --- |
| `src/api/canDoItAllClientContext.ts` | Generated options extend public `ClientOptions`; creation forwards them to `getClient`. |
| `src/canDoItAllClient.ts:753` | Public constructor accepts those options. The option type is not itself re-exported; public `ClientOptions` is sufficient structurally, or later code can derive `ConstructorParameters<typeof CanDoItAllClient>[0]`. |
| `node_modules/@typespec/ts-http-runtime/dist/browser/index.d.ts` | Public exports include the transport interfaces, header factory, `getClient`, request factory and policy interfaces. Internal pipeline factories are not exported. |
| `dist/browser/client/getClient.js` | Custom `httpClient` is passed beneath the normal SDK pipeline. Each awaited operation builds a request; it does not require replacing authentication or header policies. |
| `dist/browser/client/clientHelpers.js` and `policies/auth/*` | Configured credentials add SDK authentication policies. Built-in policies call `next` once; bearer/OAuth getters receive the request abort signal. No automatic 401 replay is implemented here. |
| `dist/browser/client/sendRequest.js` | JSON object serialization precedes the pipeline. It parses `bodyAsText` before runtime `onResponse`. Normal JSON requests set `enableBrowserStreams: true`, so that flag alone cannot be rejected. |
| `src/api/canDoItAllClientOperations.ts:3549` and `src/helpers/interfaces.ts` | Existing generated `onResponse` receives `PathUncheckedResponse` after SDK JSON parsing and before generated model conversion. It is not a raw-body hook. Existing generated options do not forward cancellation to the runtime. |
| `dist/browser/policies/retryPolicy.js`, `defaultRetryPolicy.js`, `retryStrategies/*` | Defaults permit three retries. `retryOptions.maxRetries: 0` is a separate required configuration; the transport alone does not change other clients. |
| `dist/browser/createPipelineFromOptions.js`, `fetchHttpClient.js` | SDK redirect policy is Node-only; browser Fetch has no explicit redirect mode. A browser transport must set `redirect: "error"`. |

These references are installed-version evidence, not promises about a future SDK.
The prior retry review's suggestion that generated `onResponse` could preserve
raw JSON is superseded by this card. It can preserve string-valued timestamps
before Date conversion, but cannot recover original whitespace or JSON text.

## Minimal API and lifetime

Export `createProtectedJsonAttempt(options): ProtectedJsonAttempt` with these
small readonly contracts in the same module:

- `ProtectedJsonLimits`: positive safe-integer `maxRequestBytes`,
  `maxResponseBytes`, `timeoutMs` (at most 2,147,483,647); all are required. This slice supplies no default
  claiming to cover every Projects response.
- `ProtectedJsonAttemptOptions`: trusted absolute `origin`, limits, optional
  caller `AbortSignal`, injected Fetch delegate, and selected SDK options for
  `credential`, `authSchemes`, `additionalPolicies`, `apiVersion`,
  `loggingOptions`, `telemetryOptions`, `userAgentOptions` and
  `allowInsecureConnection`. Do not expose transport/retry/redirect overrides or
  Node agent, TLS and proxy configuration through this browser factory.
- `ProtectedJsonResponse`: numeric status, frozen lower-case response-header
  entries, exact decoded `bodyText`, and measured UTF-8 byte count. Headers and
  body are response evidence, not an accepted owner result.
- `ProtectedJsonObservation`: `dispatchStarted`, nullable response head
  (status and frozen headers) and nullable complete response.
  Return a new frozen value; never expose the mutable PipelineRequest, credentials
  or a request-header capture through this observation.
- `ProtectedJsonAttempt`: readonly `clientOptions`, an `observation` getter and
  idempotent `dispose()`. Its public SDK options contain the owned HttpClient,
  endpoint bound to the origin, `retryOptions: { maxRetries: 0 }` and
  `redirectOptions: { maxRetries: 0 }`. The latter prevents the Node policy from
  following mocked redirects in pipeline tests; it does not replace Fetch's
  browser setting.
- `ProtectedJsonTransportError`: closed kind (`invalidConfiguration`,
  `invalidRequest`, `requestTooLarge`, `attemptConsumed`, `disposed`, `aborted`,
  `timedOut`, `network`, `responseTooLarge`, `invalidResponse`) and a fixed safe
  message. Do not put URL query, headers, body, token or arbitrary Fetch error
  text in the message. Outcome classification uses the observation, not the
  exception wording.

Create a fresh attempt and dedicated generated client for **one explicit SDK
operation**. Dispose in the caller's `finally`; read the retained observation in
both success and failure handling. Disposal cancels transport work and forbids a
later send, but keeps an already captured response available for reconciliation.
It does not clear browser pending evidence or generate a new operation ID.

The HttpClient consumes its single send entry synchronously, before any await.
A second call fails even if the first was rejected locally or is still running.
Only the transition immediately before invoking Fetch sets `dispatchStarted`.
This prevents custom policies or accidental SDK re-entry from producing a second
application Fetch call through this instance. A separately constructed attempt
is not authorized to retry merely because it exists: the later pending session
must require the original frozen command and an applied durable dispatch claim.

Preserve configured SDK credentials and header policies by keeping the normal
pipeline. Do not translate authentication to cookies, persist credentials, mutate
the global `apiClient` or patch global Fetch. Review any additional policy or
credential getter for its own network effects; this transport cannot police
requests made outside its HttpClient. No automatic token-refresh-and-write loop
is introduced.

## Request and response rules

Validate the origin as an HTTP(S) origin without credentials, path other than
`/`, query or fragment. Validate the final PipelineRequest URL again immediately
before Fetch: absolute HTTP(S), same normalized origin, no user info or fragment.
HTTP requires explicit `allowInsecureConnection`; cross-origin requests fail
before dispatch. The allowed methods are GET, HEAD, POST, PUT, PATCH and DELETE.

Accept an absent body, or a serialized JSON string with JSON content type and
UTF-8 encoding. GET/HEAD cannot have a body. Check UTF-16 length before bounded
TextEncoder allocation, enforce actual UTF-8 bytes, then validate JSON syntax
without rewriting its text. Reject lone UTF-16 surrogates rather than permitting
TextEncoder/Fetch to replace them silently. Reject functions, Blob, buffers/views, streams,
FormData, multipart/form fields, requested stream-response status codes,
progress callbacks and explicit Node transport settings. Do not invoke a body
factory to discover its contents. Accept the normal `enableBrowserStreams: true`
flag when no streaming response is requested.

The SDK already allocated its serialized request before reaching this boundary.
This cap prevents further unbounded transport growth; it does not replace the
later typed command's collection/string checks **before** SDK serialization.
Passing a frozen JSON string as SDK `body` with `contentType: application/json`
would JSON.stringify that string a second time in 0.2.1. Do not solve this by
changing media type or substituting an unrelated request in the transport.
The later generated API cutover must prove how original frozen command bytes
reach this seam unchanged on explicit retry; this card alone does not prove it.

Pass the final SDK header values to Fetch and use fixed
`credentials: "same-origin"`, `mode: "same-origin"`, `redirect: "error"`,
`cache: "no-store"` and the owned abort signal. Do not set keepalive or duplex.
Do not follow Location, Retry-After or authentication challenges in adapter code.
Fetch can still involve browser-owned authentication, proxy, service-worker or
connection behavior. The claim is **at most one application Fetch invocation per
attempt**, not one physical HTTP transmission. Owner operation identity and
receipts remain the duplicate-effect boundary.

Consume the response body with a reader. Check every chunk's byte length against
remaining capacity before decoding/retaining it; never call unbounded `.text()`,
`.json()` or `.arrayBuffer()`. Count decompressed bytes actually delivered by
Fetch. Content-Length is only an optional early-rejection hint, never proof that
the stream is within bounds. Use fatal streaming UTF-8 decoding, including final
flush; reject malformed or incomplete UTF-8. Preserve valid text, including
whitespace, timestamp precision and string epochs. Do not silently discard a BOM;
reject a JSON BOM explicitly. No timestamp conversion or receipt extraction occurs.

Record status and headers when available. Once the bounded body is fully decoded,
retain the immutable raw response **before** returning the SDK PipelineResponse
with its identical `bodyAsText`. An incomplete/overflow/invalid-UTF-8 body is not a
complete captured response. Empty bodies remain empty; the later operation layer
decides whether its contract permits them. For a nonempty body require
`application/json` or an `application/*+json` type with absent/UTF-8 charset;
retain a complete bounded non-JSON error response before rejecting its media
type, so diagnostics still have status without granting owner-outcome authority.

Do not JSON-parse responses twice or add a substring JSON parser. The SDK performs
the ordinary parse after the capture. Its 0.2.1 malformed-JSON RestError embeds
body text in the message; the later API adapter must replace that error with a
safe code/message and must never default-log the SDK exception. A generated
callback/model conversion failure cannot erase the attempt's captured response.
Known acceptance may be established from it only after full semantic validation;
store that knowledge before persistence or presentation callbacks.

## Cancellation and outcome mapping

Link caller and PipelineRequest abort signals; reject a pre-aborted or disposed
attempt before Fetch. Use the smaller positive request timeout and configured
transport timeout, or the configured timeout when SDK timeout is zero. A deadline
covers Fetch plus body consumption, not just response headers. On cancel, timeout,
overflow or invalid encoding, abort Fetch and cancel any acquired reader; remove
listeners, clear the timer and release the reader lock in finally. Cleanup errors
must not replace the primary failure or erase a complete response.

Race pending Fetch/read promises with the owned abort rejection as well as
signaling their cancellation; do not rely on an injected delegate honoring abort
to release the adapter's promise. Observe late fulfillment/rejection, cancel a
late response body, and fence late observation updates. No unhandled late promise
or awaited uncooperative cancellation may defeat the deadline. Validate the SDK
request timeout as finite/nonnegative before choosing its effective bound.

This transport deadline begins when its send starts. It cannot terminate an
arbitrary upstream credential getter. The current generated wrappers also omit
AbortSignal forwarding, although SDK bearer/OAuth policies support it. Until the
later deterministic generator fix, the separate attempt signal blocks/cancels the
transport but does not claim cancellation of an already waiting auth getter.
Editor retirement fences late completion and must dispose its attempt. A late
credential completion then reaches a retired transport and cannot send.

Return HTTP status/headers/text unchanged to the SDK for all status families;
there is no generic success/rejection/absence classification in this transport.
After dispatch, network, timeout, abort, oversized response, malformed JSON,
unknown status or presentation failure cannot prove rollback. Before dispatch,
local refusal is evidence only for this attempt and cannot clear an older sticky
uncertain result. Retry-After may be exposed as a header; it never schedules work.
An HTTP 200 without a valid exact typed acceptance is not known acceptance.

## Independent wire-size and receipt gates

Owner defaults/maxima are raw request 2,097,152 bytes, canonical command 1,048,576
bytes and encoded accepted receipt 2,097,152 bytes, with 1,024 phase/option rows
each. These are **different representations**. The HTTP full acknowledgement is
flattened, includes wrapper/status/replayed data and string epoch/version/expectation
shapes; do not set its wire cap by equating it with canonical receipt bytes.

Keep the transport response cap required/configurable for this dormant slice.
Before activation, derive a conservative finite wire cap using the actual emitted
contracts and HTTP serializer, then prove maximal supported create/full/scalar
write and lookup forms. Existing coherent GET can return larger legacy definitions;
a bounded client must report that refusal and preserve its current draft, never
truncate. The pending adapter's independent 2 MiB opaque payload cap remains
unchanged; HTTP envelope capture does not automatically fit its retained wrapper.

The later API layer selects a reviewed, versioned stable Receipt representation
excluding per-attempt `replayed`. JSON.parse preserves timestamp **strings** and
string epochs but does not retain exact original JSON text. Do not persist a whole
write envelope or assume write and lookup stringify identically. Actual write vs
lookup equality through the selected path, including sub-millisecond timestamp
ticks and maximum Int64 epoch text, is a required gate. If it fails, request an
engine-owned stable wire representation; do not fork the owner digest, relax
pending-store equality or normalize timestamps silently.

## Exact implementation set and planned proof

Proposed SPA changes are only the transport module, the API package's additive
exports, `packages/api-client/test/protected-json-http.node.mjs`, and
`docs/concurrent-mutations-protected-transport.md`. No Vue/main/router/generated
source, existing API singleton, pending JS/adapter or existing 18/20 test input is
changed. Tests import public runtime exports from the package which owns them.

Plan **20 explicit top-level Node cases**, not a claim of authored/discovered
tests. Table-driven assertions inside a case do not create extra claimed rows:

| Cases | Arrangement and meaningful assertion |
| --- | --- |
| 1 | Final SDK headers and exact serialized JSON reach one controlled Fetch with the fixed origin/credential/redirect/cache settings. |
| 2 | Cross-origin, user-info, fragments, unsupported protocols/methods and insecure-policy refusal produce zero Fetch calls. |
| 3 | Unsupported bodies/forms/streaming/progress and malformed request JSON fail before Fetch; normal enableBrowserStreams succeeds. |
| 4 | Exact and one-byte-too-small UTF-8 request bounds, including escaped/non-ASCII text; no body normalization. |
| 5 | Exact chunked response limit succeeds; one excess byte cancels its reader and retains no complete response. |
| 6 | Missing/lying Content-Length cannot bypass measured stream bounds; a valid oversized hint refuses early. |
| 7 | Split multibyte UTF-8 succeeds, invalid/truncated UTF-8 and BOM fail without replacement/normalization. |
| 8 | Raw whitespace, seven-digit timestamp fraction and maximum decimal epoch text survive capture byte-for-byte before SDK parsing. |
| 9 | Empty response and complete non-JSON error body retain their distinct status/body evidence; media rejection cannot imply rollback. |
| 10 | Pre-abort/dispose cause no Fetch and no new listeners/timers. |
| 11 | Abort during Fetch and during a stalled body ends transport and cleans listeners/reader; observed dispatch remains true. |
| 12 | Controlled timer expiry during body read cancels work; normal completion clears the timer; no sleep-based race. |
| 13 | Concurrent and later second send calls fail without second Fetch or replacement of the first response. |
| 14 | Disposal during send prevents late publication; disposal after complete capture preserves evidence and stays idempotent. |
| 15 | Actual installed public getClient + counting HttpClient: retryable 408/429/503/500 and recognized network error with maxRetries zero enter transport once. |
| 16 | Separate bounded positive control using the same installed retry pipeline and maxRetries one records two calls; it does not use or weaken the single-use adapter. |
| 17 | Actual SDK credential and additional header policy reach the injected HttpClient unchanged; 401 does not create adapter refresh/replay. |
| 18 | Actual SDK malformed response JSON and throwing onResponse preserve the captured raw response despite a later throw. No body-rich default logging. |
| 19 | Existing generated CanDoItAllClient constructor accepts the custom transport/options, serializes a synthetic legacy save body and calls transport once. This proves plumbing only, not new protected DTOs. |
| 20 | Independent attempt instances cannot cross-publish raw bodies or share disposal; no global Fetch mutation. |

Use controlled Fetch/ReadableStream, explicit deferred completion and Node mock
timers; never a live endpoint. Runtime pipeline cases use the installed public
`getClient`/constructor HttpClient seam, not copied retry/auth implementations or
private runtime imports. Node uses its actual Node pipeline and a network-free
counting transport; do not select the browser conditional export under Node and
pretend this is browser proof (its environment check still detects Node). The
adapter's controlled-Fetch cases use public createPipelineRequest. The generated
constructor case may use a counting transport so Node-only proxy policies do not
masquerade as browser settings. Actual browser 307/308 rejection, SDK browser
composition and all generated protected-wire roundtrips remain later proof gates.

For the composed capture-after-SDK-failure case, remove only Node's proxy policy
from the test client through public `client.pipeline.removePolicy` before using
the real adapter. This prevents ambient machine proxy settings from introducing
a Node agent that this browser transport correctly refuses; retry, authentication,
serialization and parsing remain the installed SDK code. Do not mutate process
environment, import a private runtime factory or apply that removal in production.

After approval and source freeze, run only the new source-counted tests, inspected
direct formatter/linter, targeted TypeScript check and app build in the isolated
SPA. No package installation or package-manager wrapper is needed. Keep exact
command/input/output evidence and the inactive blocked `.vite-hooks/_` untouched.
The root retains responsibility for backend schema/runtime and final closure.
