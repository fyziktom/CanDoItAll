# SB04 relay containment and security proof

State: `PASS`.

## Threat-to-control evidence

| Threat | Production control | Focused proof |
| --- | --- | --- |
| arbitrary/open proxy | caller supplies only a publication-namespaced routing ID; Workspace re-resolves current publication/profile/model/secret and Http owns connector URI/header construction | duplicate/malformed route, current-state, stored-target, base-path, caller URI/header/model override, unknown/unpublished, and mismatch cases in 24/22 |
| stale/bypassed image publication | Workspace image resolver matches exact public publication ID, profile ID, model, published/current state, secret existence, eligibility, Production descriptor, operation, base64 support, and count immediately before the existing capability | real PostgreSQL-backed image-resolver Fact plus image compatibility negatives |
| credential disclosure | Workspace resolves through the current secret resolver and passes one typed credential to the selected adapter; Http has no secret-store/EF dependency | caller override/error-redaction Facts and secret/context scan |
| authorization/context confusion | the three POST surfaces require invoke or umbrella `api`; access context and subject remain opaque audit metadata and do not enter the Http request shape | real-Web authorization/access-context Facts in 22/22 |
| feature/tool smuggling | per-surface exact allowlists, distinct Chat/Responses nested shapes, declared-name tool choice, capability intersection, and function-only tools | malformed/duplicate/bounds, tool, role/content, structured, parallel, cross-surface, and unknown-sibling Facts in 24/22 |
| image-input/output smuggling | Chat image is user-only with exact nested `image_url`, allowlisted detail, and valid bounded base64; Responses has its own exact `input_image` shape; output is bounded base64 only | vision/data-URI/detail/cross-surface/remote URL and image bounds/URL-output cases |
| private response/error/header reflection | typed failure mapper emits bounded fixed messages; response headers are reconstructed from a bounded allowlist | safe-header/raw-error/Set-Cookie/Location/auth-challenge/upstream-Server/private-URI assertions |
| content retention or metric conflation | audit schema stores metadata only; operation-aware checks keep token usage disjoint from positive image counts | metadata audit/state/SQL/projection/aggregation tests plus secret/content scan |
| upstream Responses retention default | policy emits canonical JSON `store: false` when omitted, preserves explicit JSON `false`, and rejects `true`, `null`, or non-Boolean values before dispatch | reopened policy Fact and real-Web recorded-upstream assertions; exact 24/24 and 22/22 passes |
| unbounded memory/work | bounded request/message/tool/schema/image/output limits, buffered-response maximum, bounded SSE line/frame state, linked cancellation, and three distinct timeout phases | exact policy bounds and streaming cancellation/timeout/malformed cases |

## No-open-proxy boundary

The public request contract contains no caller base URI, path, arbitrary header collection,
credential, secret reference, or internal provider profile. Workspace accepts the public routing
ID, rechecks current persisted state, resolves any current required secret for the governed
consumer, intersects operation/capabilities, and creates the detached target. Http selects an
exact connector/purpose/operation adapter and constructs only its owned endpoint:

- OpenAI configured root plus owned Chat Completions, Responses, or Images Generations route;
- Ollama configured local/remote root/base path plus `/v1/chat/completions`;
- ComfyUI through the existing in-process image capability, not caller-controlled HTTP.

The registry has exactly five Production rows and fails on missing or duplicate keys. Synthetic,
scenario, imported, default-enum, or test rows cannot become a production fallback.

## Exact request, tool, schema, and image containment

Chat and Responses do not share a loose "OpenAI-like" object. Chat tools/tool choices are nested;
Responses forms are flattened. Chat schema output uses `response_format.json_schema`; Responses
uses the flattened `text.format` schema. Named choices must match a declared tool. Descriptions,
parameters, schemas, and strict flags are typed and bounded. `parallel_tool_calls` itself requires
parallel capability even when false. Assistant null/omitted content is accepted only with valid
tool calls. Every cross-surface form and unknown nested sibling fails.

Chat data-image input is user-only and has exact outer/inner properties, a `detail` of
`auto|low|high`, an allowlisted image media type, and non-empty whitespace-free valid bounded
base64. Responses accepts only its exact `input_image` with string `image_url` data URI. Remote
URLs and file IDs fail. All five current Production descriptors advertise no vision, so both
otherwise valid input shapes remain denied in production.

Images `n` defaults to 1 only when absent. Null, string, boolean, object, array, fraction,
overflow, zero, negative, and above-limit present values fail explicitly. Image output is bounded
by count, per-image bytes, total bytes, and exact content type and is serialized as `b64_json`;
private paths and artifact URLs never enter the response.

Function tools are returned for client execution; central does not instantiate or execute them.
Hosted web/file/MCP/computer/code/shell/retrieval tools, provider storage/background state,
conversation IDs, caller identity, unsupported modalities, remote files/images, and audio fail.

Responses storage is deliberately narrower than a blanket property denial. Omission does not
remove the member: canonicalization writes JSON `store: false` so the upstream request cannot
inherit a retention-enabling provider default. Explicit JSON `false` is accepted and preserved.
JSON `true`, `null`, strings, numbers, objects, and arrays reject before the dispatcher is called.
The August 25 reopen proves this through both the policy and real-Web recorded-upstream boundaries
without changing the frozen Fact counts.

## Upstream and downstream metadata containment

Caller Authorization is consumed by central authorization and is not copied. Access context,
authenticated subject, trace/correlation values, cookies, caller host, arbitrary organization/
project headers, and caller credentials are absent from the Http integration request shape. Only
adapter-owned Authorization and explicit content/accept behavior are applied upstream.

The response policy retains only typed safe metadata and bounded retry delay. It strips upstream
Set-Cookie, Location, authentication challenges, private Server/header/URI values, and raw bodies.
Exception messages, stack traces, endpoints, and credentials become bounded public failures. The
compatibility proof correctly distinguishes Kestrel's own host `Server` header from an upstream
sentinel; the latter is proven absent.

The final secret/context scan (`sb04-secret-content-scan-semantic-final.txt`) passes. It is source/
content evidence: it looks for credential-shaped material and for access-context, subject, or
correlation metadata in Http. It is not a packet capture and does not claim a live multi-host
boundary; SB07 owns that proof.

## Streaming, audit, and usage containment

Streaming uses `ResponseHeadersRead`, bounded parser state, per-read idle cancellation, downstream
cancellation, typed completion, and async disposal. Malformed, oversized, timed-out, cancelled,
I/O-failed, or transport-failed streams never expose raw private text or synthesize `[DONE]`.

Audit contains identifiers, operation, timing, outcome, failure category, observed usage, and
pricing metadata only. It contains no prompt, response, image bytes, attachment, tool arguments,
request body, response body, secret, or upstream endpoint. The five public/additive image-count
surfaces do not widen that content boundary:

- `SharedProviderRelayUsage.ImageCount` carries only a positive bounded count;
- `SharedProviderInvocationCompletion.ImageCount` carries terminal count metadata;
- `SharedProviderInvocationRecord.ImageCount` persists only that count;
- `ProviderUsageContribution.ImageCount` projects only that count;
- `ProviderUsageTotals.ImageCount` aggregates only that count.

Chat/Responses persist tokens without an image count; Images persist a positive complete image
count without tokens; unavailable contains neither. EF configuration, migration, designer, and
snapshot carry the same PostgreSQL check. Inconsistent rows fail projection rather than being
silently coerced. The existing usage direction remains the only ledger.

## Honest recovery proof boundary

Finalization is once-only and its source contains three bounded attempts with a bounded token.
SB04 does not inject a finalizer `SaveChanges` failure, so it does not claim a tested transient
finalizer-persistence-failure path. Durable evidence instead comes from the real hosted recovery
worker against PostgreSQL: with a test-only 100 ms/100 ms internal schedule, it recovers only a
stale `InProgress` row and leaves fresh/terminal rows intact. Production defaults remain 10
seconds at startup and one minute between scans.

The final 69-file anti-stub selection passes in `sb04-anti-stub-audit-semantic-final.txt`. Deliberate
`NotSupportedException` overrides required by read-only test `Stream` doubles were manually
classified rather than mistaken for product placeholders; the full streaming class passes 12/12.

The reopen evidence is chronological: entry validation passed; the first Unit build failed on
four missing JSON symbols and its final build passed; Web passed; the first Integration build
failed on one missing fixture symbol and its final build passed. Exact reopened selections passed
24/24 relay policy, 22/22 compatibility, and 12/12 streaming. All are preserved under
`proof/transcripts/sb04-reopen-*`.
