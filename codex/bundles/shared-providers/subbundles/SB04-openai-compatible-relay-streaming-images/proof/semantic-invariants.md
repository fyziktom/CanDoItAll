# SB04 semantic invariant contract

State: `COMPLETE`. The final independent audit reports `PASS` with no remaining SB04 code or test
blocker. Authoritative exact results are 24/24 relay policy, 22/22 OpenAI compatibility, and
12/12 streaming; the affected usage-aggregation support lane is 7/7.

## SB04-INV-01 — Dispatch is publication-namespaced and never an open proxy

- **Required behavior:** a caller supplies one opaque publication-namespaced routing ID. Workspace
  re-resolves exactly one currently published, eligible publication/profile/upstream model and
  current required secret. Only the stored target URI, model, timeout, typed credential, purpose,
  operation, and intersected capabilities reach dispatch.
- **Fail-closed boundary:** caller URI/path/host/header/secret/internal profile ID/upstream model,
  unknown or unpublished routes, duplicate model names, purpose/operation mismatch, stale state,
  missing/duplicate adapter registration, and unsupported capability combinations fail without an
  upstream call. There is no connector fallback.
- **Proof:** the exact 24/22 lanes cover malformed/duplicate routing, stored-target construction,
  caller override denial, current-state mismatch, and the exact five-row Production registry.
  The persisted compatibility Fact uses real Workspace/PostgreSQL/current-secret behavior and a
  neutral recording dispatcher; it proves central resolution, not a live external provider hop.
- **Downstream:** SB07 owns live three-host hop proof; SB05/SB06 reuse the stable publication and
  routing identity.

## SB04-INV-02 — Chat and Responses are distinct exact compatibility subsets

- **Required behavior:** every root and nested object has an exact allowlist. Unknown/duplicate
  members, malformed JSON/UTF-8, excessive depth, invalid types, and over-limit values fail before
  canonicalization or dispatch.
- **Chat shapes:** tools are nested under `function`; named choice is
  `{"type":"function","function":{"name":"<declared>"}}`; schema output is
  `response_format = {"type":"json_schema","json_schema":{...}}`. Role-specific message
  fields are exact. An assistant may omit/null content only with valid `tool_calls`. Chat images
  are user-only and use exact nested `image_url` with optional `detail` in `auto|low|high`.
- **Responses shapes:** tools are flattened to `type/name/description?/parameters?/strict?`;
  named choice is `{"type":"function","name":"<declared>"}`; schema output is flattened
  inside `text.format`. Input message/text/image and function-output shapes are Responses-owned.
- **Responses retention:** omission of `store` is canonicalized to the JSON Boolean `false`, and
  explicit JSON `false` is preserved. JSON `true`, `null`, and every non-Boolean value reject
  before dispatch. The canonical upstream body therefore never relies on a provider retention
  default.
- **Shared constraints:** a named tool choice must name a function declared in the same request;
  optional descriptions, parameters, schemas, and strict flags are typed/bounded. The presence of
  `parallel_tool_calls` requires advertised parallel support even when `false`. Data images must
  be non-empty, valid, whitespace-free bounded base64 with an allowlisted media type.
- **Fail-closed boundary:** Chat and Responses tool, choice, structured-output, text-part, and
  image-part shapes cannot be interchanged. Hosted/built-in tools, provider storage/background/
  conversation state, remote URLs, file IDs, caller identity, unsupported modalities, and all
  unknown siblings fail. All five Production rows advertise no vision input.
- **Proof:** final exact 24/24 and 22/22 transcripts. The August 25 reopen strengthened existing
  Unit and real-Web compatibility Facts with recorded-upstream assertions without changing either
  Fact count. The full implemented matrix is frozen in `behavior/supported-denied-fields.md`.

## SB04-INV-03 — Image request count and image output are bounded and base64-only

- **Required behavior:** Images Generations accepts a bounded prompt and optional exact size,
  quality, output format, and `b64_json`. Absent `n` defaults to 1; a present `n` must be an integer
  from 1 through the resolved adapter limit.
- **Fail-closed boundary:** null/string/boolean/object/array/fractional/overflow `n`, zero,
  negative, above-limit count, URL response request, unexpected content type, too many images,
  excessive bytes, private path, and artifact URL fail. A malformed present `n` is never silently
  normalized to 1.
- **Ownership:** OpenAI image dispatch uses the stored target. ComfyUI re-resolves exact current
  publication/profile/model/secret/eligibility in Workspace, then a narrow AgentFramework bridge
  invokes the existing image capability. Persistence queries do not leak into AgentFramework.
- **Proof:** exact policy and compatibility image Facts plus the real PostgreSQL-backed image
  resolver Fact. Its dispatcher is neutral/fake; Workspace current-state and secret checks are
  real. No audio route or image URL response exists.

## SB04-INV-04 — Streaming is incremental, bounded, cancellable, and truthfully terminal

- **Required behavior:** `ResponseHeadersRead` delivers the first SSE frame without buffering the
  full response; frames retain order and valid UTF-8; only a real terminal success emits `[DONE]`.
  Socket-connect, overall target, and per-read idle timeouts are separate controls.
- **Fail-closed boundary:** bounded line/frame state, malformed/oversized frames, split UTF-8,
  downstream cancellation, pre-header timeout, post-header idle timeout, `IOException`, and
  `HttpRequestException` produce typed sanitized completion and deterministic disposal. Failure
  cannot expose raw transport text or synthesize success.
- **Proof:** exact 12/12 streaming lane, including the two first-byte-before-completion gates. The
  historical TestServer `Content-Length` correction did not remove either incremental gate.

## SB04-INV-05 — Invocation usage is operation-aware, metadata-only, and coherent end to end

- **Generic relay shapes:** unavailable has no counts; partial has exactly one token count and no
  image count; complete text has both token counts and no image count; complete image has one
  positive bounded image count and no token counts.
- **Persisted operation rules:** Chat/Responses permit unavailable, partial-token, or complete-
  token usage only. Images permit unavailable or complete positive image usage only. C#
  transitions and the PostgreSQL check constraint in EF configuration, migration, designer, and
  snapshot enforce the same rules and the 1..16 image bound.
- **Five public/additive surfaces:** `SharedProviderRelayUsage.ImageCount`,
  `SharedProviderInvocationCompletion.ImageCount`, `SharedProviderInvocationRecord.ImageCount`,
  `ProviderUsageContribution.ImageCount`, and `ProviderUsageTotals.ImageCount`. Existing primary
  constructor/deconstruct arities remain 8 for invocation completion, 16 for contribution, and 10
  for totals; the additions are init-only where ABI preservation is required.
- **Projection/aggregation:** inconsistent stored shapes fail the usage source explicitly. Valid
  partial or non-representable oversized token usage projects unavailable; complete images project
  observed with empty token totals. Aggregation rejects non-positive or token-mixed image
  contributions and checked-sums image totals separately from token totals.
- **Content boundary:** audit stores identifiers, operation, timing, outcome, failure category,
  observed usage, and price metadata only. It stores no prompt, response, image bytes, tool
  arguments, request/response body, secret, or private upstream endpoint.
- **Proof:** 24/24 unit policy/state Fact, the real PostgreSQL persisted relay Fact, 7/7 usage
  aggregation, SB02 downstream state/persistence revalidation at 18/18 and 14/14, and no pending
  EF model changes. PostgreSQL rejects cross-operation and zero-image rows.

## SB04-INV-06 — Terminalization is once-only and stale interruption is durably recovered

- **Required behavior:** audit begin precedes dispatch. One cached finalization task maps buffered
  or streaming completion once and performs bounded retry with a bounded cancellation token.
  Recovery scans a bounded set of stale `InProgress` rows and uses optimistic concurrency.
- **Hosted timing:** production recovery uses an internal immutable schedule with a 10-second
  startup delay and one-minute interval. The integration fixture substitutes 100 ms/100 ms and
  polls up to 20 seconds. The hosted worker recovers a stale row and preserves fresh and already-
  terminal rows.
- **Proof boundary:** source inspection proves the bounded retry implementation; the exact unit
  Fact proves pure transition idempotency. The real PostgreSQL/hosted-worker Fact proves stale-row
  recovery. SB04 does **not** force a finalizer `SaveChanges` failure and therefore does not claim
  a tested transient-finalizer-failure path.

## SB04-INV-07 — Authorization, secrets, headers, errors, and context stay contained

- **Required behavior:** all three POST routes require invoke or umbrella `api` scope. Opaque
  access context and authenticated subject are audit metadata only, never upstream authentication.
  Secrets remain central and reach only the selected adapter as a typed credential.
- **Fail-closed boundary:** caller Authorization/context/subject/correlation/cookies/host/
  organization/project headers are not copied. Response metadata is reconstructed from a bounded
  allowlist; raw upstream body, exception, URI, credentials, Set-Cookie, Location, auth challenge,
  private Server/header/URI, and secret sentinels do not cross the boundary.
- **Proof:** authorization/access-context/error/header Facts in 22/22, header/retry/failure Facts
  in 24/24, streaming containment in 12/12, plus the secret/context content scan. Kestrel-owned
  headers are not mistaken for copied upstream headers.

## SB04-INV-08 — Dependency direction and the HTTP surface remain exact

- **Ownership:** Abstractions is SDK/EF/ASP.NET/HttpClient-neutral; Http owns protocol behavior;
  Workspace owns persisted state, secrets, audit, recovery, and image target resolution;
  AgentFramework is only the outer image/usage bridge; Composition registers Http; Web renders
  HTTP. There is no Workspace-to-Http or Http-to-Workspace/Web/EF reverse edge.
- **HTTP surface:** Web exposes exactly POST Chat Completions, Responses, and Images Generations
  under the shared-provider OpenAI prefix. There is no inference wildcard, audio route, or
  inference ETag contract. Web delegates to the neutral application port.
- **Adapter surface:** exactly five Production descriptors exist: OpenAI chat/image, Ollama local/
  remote chat, and ComfyUI image. Missing/duplicate registration fails rather than falling back.
- **Proof:** refreshed project-reference and CodeAnalytics after artifacts, exact registry/
  architecture/OpenAPI Facts, public-surface review, anti-stub audit, and final independent PASS.

## Governed evidence bindings

`SB04-INV-01` through `SB04-INV-08` bind to the honest failing-first transcript, the semantic-final
Release build and exact list/run transcripts, the affected 7-case usage lane, additive SB02
downstream revalidation, architecture after artifacts, `architecture/semantic-reopen-review.md`,
independent review, containment scan, anti-stub audit, and final closure validator. Per-file proof
integrity is centralized in `proof/hashes.sha256` after the narrative is complete.

The August 25 Responses wire-contract reopen additionally binds to
`transcripts/sb04-reopen-entry-validator.txt`; the honest first/final Unit and Integration build
transcripts; the clean Web build; and the reopen list/run transcripts proving unchanged discovery
and 24/24, 22/22, and 12/12 passes. The first Unit build failed with four missing JSON symbol
errors and the first Integration build failed with one missing fixture symbol; neither is
misrepresented as a passing gate.
