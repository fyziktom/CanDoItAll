# SB04 semantic reopen review

Final disposition: `PASS`. No remaining SB04 code or test blocker.

This record preserves why an initially green structural result was reopened. Passing builds and
fixed test counts were necessary but did not by themselves prove that the accepted JSON shapes,
usage model, persistence rules, and recovery evidence matched the governed semantics.

## Failing-first and initial independent blockers

The initial 24-case relay-policy selection failed to compile because the neutral runtime and
policy contracts did not yet exist. After the first implementation became green, independent
review found four material blockers:

1. Chat `image_url` and Responses `input_image` accepted their principal field without an exact
   outer allowlist, permitting unknown siblings.
2. SSE `IOException`/`HttpRequestException` could escape without typed terminal completion,
   truthful audit classification, and deterministic disposal.
3. Terminalization lacked a durable stale-`InProgress` recovery path; final persistence behavior
   could not be represented as closed merely because a process-local finalizer ran.
4. The ComfyUI bridge put current Workspace persistence lookup in AgentFramework and did not
   re-resolve exact current publication/profile/model/secret eligibility at execution.

The repairs added exact part shapes, typed transport completion, Workspace-owned image resolution,
and bounded optimistic-concurrency stale-row recovery through a hosted worker.

## First semantic reopen: exact request-policy behavior

The next audit found that broad allowlists were still semantically wrong even though the frozen
policy count passed. The repaired requirements are:

- Chat tools are nested under `function`; Responses tools are flattened.
- Chat named tool choice is nested; Responses named choice is flattened; either must name a
  function declared in the same request.
- Chat structured schema is nested under `response_format.json_schema`; Responses schema fields
  are flattened inside `text.format`.
- Optional tool/schema descriptions are bounded strings, parameters/schema are bounded objects,
  and optional strict flags are booleans.
- The presence of `parallel_tool_calls` requires advertised support even when its value is false.
- Chat messages have role-specific exact fields. Assistant content may be omitted/null only when
  valid function `tool_calls` are present. Chat image input is user-only.
- Chat data images require exact outer/inner objects and `detail` in `auto|low|high`; Responses
  requires its exact `input_image`/string `image_url` shape. Data must be non-empty valid bounded
  base64; remote URLs, unknown siblings, and cross-surface shapes fail.

All five Production descriptors explicitly remain without vision input, so the valid syntactic
image-input forms remain denied for production rows.

## Second semantic reopen: malformed image count

Review found an ambiguity between absent and malformed Images `n`. The final policy distinguishes
them: absent defaults to 1; present null/string/boolean/object/array/fraction/overflow/zero/
negative/above-adapter-limit values reject. A malformed present value never falls through to the
default. Post-fix Unit build is 0 warnings/0 errors and exact relay-policy discovery/run remains
24 and 24/24.

## Third semantic reopen: image usage coherence and compatibility

Initial image usage stopped at extraction/projection and could mix operations or break existing
record constructors. The final design carries `ImageCount` across five public/additive surfaces:

1. `SharedProviderRelayUsage`;
2. `SharedProviderInvocationCompletion`;
3. `SharedProviderInvocationRecord`;
4. `ProviderUsageContribution`;
5. `ProviderUsageTotals`.

The generic relay value accepts unavailable, token-partial, token-complete, or image-complete
shapes. Invocation state narrows this by operation: Chat/Responses never carry image counts;
Images never carry tokens and accept only unavailable or complete positive image counts. The
1..16 bound and the same operation matrix appear in C# transitions and the PostgreSQL constraint
payload in EF configuration, migration, designer, and model snapshot.

Projection fails explicitly for inconsistent stored rows, maps partial/unrepresentable token
usage to unavailable, and maps complete image usage to observed with empty token totals.
Aggregation rejects non-positive/token-mixed image contributions and checked-sums image totals.
The existing primary constructor/deconstruct arities remain 8 for
`SharedProviderInvocationCompletion`, 16 for `ProviderUsageContribution`, and 10 for
`ProviderUsageTotals`; init-only additions preserve that ABI.

The affected 7-case usage aggregation lane passes 7/7. Because SB04 changed SB02-owned state and
persistence semantics, SB02 was explicitly invalidated and revalidated at 18/18 state, 14/14
persistence, 6/6 deletion/reference, and no pending EF model changes. The first deletion rerun
failed only on a sandbox AppData lock; its approved rerun passed 6/6 and both transcripts are
preserved chronologically.

## Fourth semantic reopen: proof realism and recovery timing

The first compatibility lane was insufficiently clear about mocked boundaries. Its first three
Facts now use real Web routing, Workspace services, PostgreSQL state, current secret resolution,
audit persistence, usage projection, hosted recovery, and Workspace image resolution. Only the
external dispatcher is replaced by a neutral recording fake. This proves the central runtime and
durability boundary without claiming network or paid-provider behavior.

The recovery worker gained an internal immutable schedule: production remains 10-second startup/
one-minute interval; the integration fixture replaces it with 100 ms/100 ms and polls up to 20
seconds. The hosted Fact proves stale-row recovery while preserving fresh and terminal records.
The pure unit Fact proves transition idempotency. Source inspection confirms bounded finalizer
retry, but no test injects a finalizer persistence failure; no such claim is made.

## Fifth semantic reopen: Responses retention-safe canonicalization

SB07 exercised the real upstream boundary and invalidated the earlier SB04 Responses wire
contract. Merely allowing omission leaves storage behavior dependent on the provider's default;
blanket rejection also prevents an explicitly safe request. The final contract is exact:

- omitted `store` is written into the canonical request as JSON `false`;
- explicit JSON `false` is accepted and preserved;
- JSON `true`, `null`, and every non-Boolean value reject before dispatch.

Existing tests were extended instead of adding parallel Facts. The policy Fact covers both
canonical inputs and all invalid value classes. The compatibility Fact traverses the real Web
surface and checks the body observed by the recording upstream boundary. Discovery therefore
remains exactly 24 relay-policy, 22 compatibility, and 12 streaming Facts.

The reopen chronology remains honest. The entry validator passed. The first Unit build failed
with four `CS0103` errors because the strengthened assertions lacked the `System.Text.Json`
symbols; the final Unit build passed with zero warnings/errors. Web built with zero
warnings/errors. The first Integration build failed with one `CS0117` error because an assertion
used a nonexistent `ResponsesModelId` fixture constant; the final Integration build passed with
zero warnings/errors. The final exact runs passed 24/24, 22/22, and 12/12. Their authoritative
artifacts are the `proof/transcripts/sb04-reopen-*` files.

## Final evidence and disposition

- Release builds: Web, Unit, Integration — 0 warnings, 0 errors.
- Governed SB04 selections: 24/24 relay policy, 22/22 compatibility, 12/12 streaming.
- Supporting affected lane: 7/7 usage aggregation.
- Downstream SB02 revalidation: 18/18 state, 14/14 persistence, 6/6 deletion/reference, no pending
  EF model changes.
- EF operation-aware constraint text aligns across configuration, migration, designer, snapshot.
- Existing constructor/deconstruct ABI is preserved.
- Final independent audit: `PASS`; no remaining SB04 code or test blocker.
- August 25 retention reopen: entry validator pass; honest Unit and Integration compile failures
  repaired; final Unit/Web/Integration builds clean; unchanged exact selections pass 24/24,
  22/22, and 12/12 with real-Web/upstream assertions.

Live-provider, multi-host, source-sync, UI, exported-OpenAPI, and aggregate-regression proof remains
in its designated downstream subbundle and is not claimed here.
