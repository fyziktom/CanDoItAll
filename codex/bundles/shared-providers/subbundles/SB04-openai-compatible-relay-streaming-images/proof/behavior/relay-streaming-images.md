# SB04 relay, streaming, tools, images, and usage behavior proof

State: `PASS`.

## Final focused validation

| Selection | Discovery | Result | Authoritative evidence |
| --- | ---: | ---: | --- |
| `SharedProviderRelayPolicyTests` | 24 | 24 passed, 0 failed, 0 skipped | `../transcripts/sb04-list-relay-policy-release-semantic-final.txt`; `../transcripts/sb04-run-relay-policy-release-semantic-final.txt` |
| `SharedProviderOpenAiCompatibilityIntegrationTests` | 22 | 22 passed, 0 failed, 0 skipped | `../transcripts/sb04-list-openai-compatibility-release-semantic-final.txt`; `../transcripts/sb04-run-openai-compatibility-release-semantic-final.txt` |
| `SharedProviderStreamingIntegrationTests` | 12 | 12 passed, 0 failed, 0 skipped | `../transcripts/sb04-list-streaming-release-semantic-final.txt`; `../transcripts/sb04-run-streaming-release-semantic-final.txt` |
| `ProviderUsageAggregationTests` (supporting affected lane) | 7 | 7 passed, 0 failed, 0 skipped | `../transcripts/sb04-list-provider-usage-aggregation-release-semantic-final.txt`; `../transcripts/sb04-run-provider-usage-aggregation-release-semantic-final.txt` |

The final Web, Unit, and Integration Release builds complete with zero warnings and zero errors.
The exact 24/22/12 selection remains frozen; the 7-case usage lane is additive downstream proof,
not a change to an SB04 governed count. No unfiltered solution test, live provider, paid service,
external network, browser, or multi-instance lane ran.

## Exact request, routing, and adapter behavior

The policy uses distinct Chat and Responses shapes. Chat function tools and named choices are
nested under `function`; Responses definitions and named choices are flattened. Chat schema output
uses `response_format.json_schema`; Responses uses `text.format` with the schema fields on the
format object. Named choices must reference a declared function. Optional descriptions,
parameters, schema, and strict flags are type- and size-checked. Merely supplying
`parallel_tool_calls` requires parallel support, including `false`. Chat image input is user-only,
uses an exact nested object and `auto|low|high` detail, and accepts only non-empty valid bounded
base64 data URIs. Responses uses its exact `input_image`/string `image_url` shape. Cross-surface
forms and unknown siblings fail.

Images `n` defaults to 1 only when absent. A present non-integer, null, fraction, overflow, zero,
negative, or above-limit value is rejected rather than normalized. The caller supplies only a
publication-namespaced routing ID; Workspace resolves current publication/profile/model/secret
state and Http rewrites the canonical request to the stored upstream model.

| Production descriptor | Proven behavior |
| --- | --- |
| OpenAI chat | Chat Completions and Responses use the stored root, owned routes, bearer credential, persisted target timeout, and upstream model; public model is restored in buffered output |
| OpenAI image | Images Generations accepts the bounded base64-only subset and returns `b64_json` without a private URL/path |
| Ollama local chat | uses the owned OpenAI-compatible `/v1/chat/completions` route |
| Ollama remote chat | preserves the configured base path and appends the owned compatibility route |
| ComfyUI image | re-resolves exact current publication/profile/model/secret/eligibility in Workspace, invokes the existing image capability, bounds bytes/type/count, and emits base64 only |

These are the only five Production rows. All five advertise `SupportsVisionInput == false`.
Function definitions and returned calls round-trip for client execution; central does not execute
tools. Hosted/built-in tools, unsupported advanced fields, remote image URLs, audio, and fallback
connector selection fail closed.

## Real Workspace/PostgreSQL proof boundary

The first three compatibility Facts use the real Web endpoint mapping, Workspace application
service, current PostgreSQL state, current secret resolver, invocation audit rows, existing usage
projection, hosted recovery worker, and Workspace image-target resolver. The external relay
dispatcher is deliberately replaced by a neutral recording fake. Consequently these Facts prove
central routing/secret/audit/usage/recovery/image ownership without network or paid-provider
dependence; they do not claim a live OpenAI/Ollama/ComfyUI hop.

The persisted relay Fact executes Chat, Responses, and Images, verifies resolved publication,
profile, connector, URI, upstream model, timeout, and credential, then reads metadata-only audit
rows from PostgreSQL. It proves complete token usage for Chat/Responses and complete positive
image usage with null token columns for Images. It also reads the existing usage projection and
aggregate, where image observations have empty token totals and a positive image count.

The hosted-recovery Fact seeds stale, fresh, and already-terminal rows. Production defaults are a
10-second startup delay and one-minute interval; the integration fixture replaces only the
internal immutable schedule with 100 ms/100 ms and polls up to 20 seconds. The real hosted worker
recovers only the stale `InProgress` row and leaves fresh/terminal rows intact. This is not a
forced finalizer-save-failure test. Bounded finalizer retries are source-inspected; the tested
durability claim is stale-row recovery by the hosted worker.

## Streaming lifecycle and containment

Chat and Responses first-byte tests keep upstream open and prove the first SSE chunk reaches the
real Web client before completion. Frames remain ordered, split UTF-8 is reassembled, observed
terminal usage is extracted, and a real terminal success alone yields `[DONE]`. Missing usage is
unavailable rather than fabricated as zero.

Downstream cancellation cancels and disposes upstream work. A pre-header timeout maps to a typed
gateway timeout; a post-header idle timeout retains the already-sent SSE status and fails stream
completion. Malformed/oversized frames and midstream `IOException`/`HttpRequestException` are
bounded, sanitized, disposed, and never receive a synthetic success terminator. Socket-connect,
overall target, and per-read idle timeouts are separate production controls.

The early `Content-Length` assertion failure was a TestServer artifact after a deterministic
stream completed. Removing that host-specific assertion did not weaken the two gated
first-byte-before-completion tests.

## Operation-disjoint usage and audit

Relay usage has only these coherent shapes:

- unavailable: no input tokens, output tokens, or image count;
- partial: exactly one token count and no image count;
- complete text: both token counts and no image count;
- complete image: a positive bounded image count and no token counts.

Persistence narrows that generic contract by operation: Chat/Responses allow unavailable,
partial-token, or complete-token usage only; Images allow unavailable or complete positive image
usage only. C# transitions and the PostgreSQL check constraint in EF configuration, migration,
designer, and snapshot enforce the same matrix and the 1..16 image bound. Projection fails the
source explicitly for inconsistent stored rows; valid partial or unrepresentable oversized token
usage projects as unavailable, while complete Images project as observed with empty token totals.
Aggregation validates that observed image contributions never mix token counts, sums image counts
in a checked block, and exposes them separately from tokens.

The five public/additive `ImageCount` surfaces are `SharedProviderRelayUsage`,
`SharedProviderInvocationCompletion`, `SharedProviderInvocationRecord`,
`ProviderUsageContribution`, and `ProviderUsageTotals`. The pre-existing primary constructor and
deconstruction arities of invocation completion (8), usage contribution (16), and usage totals
(10) remain unchanged through init-only additive properties.

## Honest red, review, reopen, and final green

The failing-first transcript records the expected missing-contract compile red. Initial review
then found exact outer image-shape, typed SSE transport completion, durable stale-audit recovery,
and Workspace image-ownership blockers. A later semantic audit reopened the apparently green
phase for Chat/Responses shape separation, declared tool choice, role/content, parallel-capability,
structured-output, data-image/detail/base64, malformed `n`, operation-aware image usage, SQL/
projection/aggregation coherence, ABI preservation, real PostgreSQL proof boundaries, and hosted
recovery timing. Every item was repaired without changing 24/22/12. The final independent audit
reports PASS with no remaining SB04 code or test blocker; detailed chronology is in
`../architecture/semantic-reopen-review.md`.
