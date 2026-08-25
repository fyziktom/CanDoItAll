# SB04 session handoff

State: `DONE`

## Outcome

The SB04 implementation, exact focused lanes, downstream SB02 revalidation, dependency audit,
proof inventories/hashes, closure validation, and final independent code review pass. SB04 is
`DONE`; SB05 alone is `READY`.

The three authenticated OpenAI-compatible POST surfaces resolve only current persisted
publications, dispatch through five typed production connector/purpose rows, stream incrementally
with bounded failures, and record metadata-only operation-aware usage. The compatibility lane
uses the real Web, Workspace, PostgreSQL, catalog, secret, invocation, hosted-recovery, and image-
target services with a deterministic neutral dispatcher; it does not claim a live provider call.

## Repository state

- branch: `providers-shared`;
- commit before/after: `e46f81d5ee33627dccb548732725e1c37e980ab5`;
- no commit, staging, discard, push, or unrelated-file overwrite was performed;
- the worktree remains the cumulative uncommitted SB00-SB04 implementation and governed proof.

## Semantic reopen chronology

The original failing-first transcript is a genuine pre-contract compile failure. It is not
represented as the red source for every later semantic repair. Independent audits subsequently
found and drove fixes for:

- exact Chat/Responses image-part allowlists, typed SSE transport failures, independent
  connect/overall/idle timeouts, durable stale-invocation recovery, and Workspace ownership of
  exact image target resolution;
- real PostgreSQL Workspace route/secret/audit/recovery/image proof, exact nested/flattened wire
  shapes, image-only usage persistence/projection/aggregation, and constructor/deconstruction ABI;
- surface-specific tools, named choice, structured output, text/image discriminators, declared-
  tool matching, Chat user-role vision, valid Base64/detail, bounded description/Boolean strict,
  and capability gating whenever `parallel_tool_calls` is present, including `false`;
- explicit malformed image count handling: only absent `n` defaults to 1; present string, null,
  fraction, Int32 overflow, non-positive, or out-of-range values fail closed.
- the August 25 Responses retention contract: omission is canonicalized to JSON `false`, explicit
  JSON `false` is accepted, and `true`, `null`, or every non-Boolean value fails before dispatch.
  The policy and real-Web compatibility assertions verify the canonical request observed by the
  upstream boundary without adding or removing a Fact.

Final read-only audit reports no remaining SB04 code/test blocker.

The August 25 reopen passed the SB04 entry validator. Its first Unit build failed honestly with
four missing `JsonDocument`/`JsonValueKind` symbols and its first Integration build failed honestly
with one missing `ResponsesModelId` fixture symbol. The imports/fixture reference were corrected;
the final Unit and Integration builds are clean. The intervening Web build was clean on its first
reopen run. All failing and passing transcripts remain preserved.

## Architecture and public surface

- CodeAnalytics before: `snap-20260825012213-a17e36ed`;
- force-refreshed after: `snap-20260825051057-300644c7`;
- after metrics: 14 product projects, 752 documents, 35 modules, 5,158 dependency facts, 34
  direct references, zero project cycles, unchanged two module plus one nested-type cycle, and
  zero error findings;
- the only product-reference delta is the authorized
  `CanDoItAll.Modules.AgentFramework -> CanDoItAll.SharedProviders.Abstractions` edge;
- Web owns HTTP, Workspace owns current routing/secret/audit/recovery/image target policy, Http
  owns protocol/transport, AgentFramework owns only existing image and usage bridges, and outer
  Composition owns registration;
- additive `ImageCount` properties exist on relay usage, invocation completion/record, usage
  contribution, and usage totals. Existing primary constructor/deconstruction arities remain
  unchanged; the recovery schedule is internal with friend access only for Integration tests.

## Frozen wire and usage contract

Chat uses nested function tools/named choice and nested `response_format.json_schema`; Responses
uses flattened function tools/named choice and `text.format`. Chat uses `text`/nested `image_url`
parts and Responses uses `input_text`/flattened `input_image`. Named choices must match a declared
tool. Vision data is user-role Chat only, PNG/JPEG/WebP, nonempty whitespace-free valid Base64,
with Chat detail limited to auto/low/high. The five production rows advertise no vision.

Unavailable usage has no counts; partial text usage has exactly one token count; complete
Chat/Responses has both token counts; complete Images has a positive ImageCount only. EF
configuration, migration, designer, and snapshot enforce operation-specific rules with ImageCount
bounded 1–16. Projection and aggregation reject inconsistent, mixed, or non-positive values and
never fabricate missing usage as zero.

## Recovery behavior

The stale threshold is maximum relay timeout plus five minutes. Defaults are a 10-second startup
delay, one-minute interval, batch 100, and hard maximum 1000. The deterministic test schedule is
100 ms startup/interval; its stale row is maximum timeout plus six minutes, fresh row maximum
timeout plus three minutes, and poll bound 20 seconds. The hosted worker proves stale-only
terminalization and preservation of fresh/terminal rows. The bounded finalizer retry loop is
source-reviewed; no forced finalizer-save failure/retry behavior is claimed.

## Final build and test evidence

| Gate | Result | Artifact |
| --- | --- | --- |
| Web Release build | 0 warnings, 0 errors | `proof/transcripts/sb04-build-web-release-closure-final.txt` |
| Unit Release build | 0 warnings, 0 errors | `proof/transcripts/sb04-build-unit-release-after-image-count-fix.txt` |
| Integration Release build | 0 warnings, 0 errors | `proof/transcripts/sb04-build-integration-release-closure-final.txt` |
| relay policy | 24 discovered; 24/24 pass | `proof/transcripts/sb04-run-relay-policy-release-after-image-count-fix.txt` |
| compatibility | 22 discovered; 22/22 pass | `proof/transcripts/sb04-run-openai-compatibility-release-closure-final.txt` |
| streaming | 12 discovered; 12/12 pass | `proof/transcripts/sb04-run-streaming-release-closure-final.txt` |
| supporting usage | 7 discovered; 7/7 pass | `proof/transcripts/sb04-run-provider-usage-aggregation-release-semantic-final.txt` |

No broad, browser, multi-instance, source-sync, paid-provider, live-network, or UI lane ran.

## August 25 wire-contract reopen evidence

| Gate | Result | Artifact |
| --- | --- | --- |
| entry validator | pass | `proof/transcripts/sb04-reopen-entry-validator.txt` |
| Unit Release build, first | fail: 4 `CS0103` missing JSON symbols | `proof/transcripts/sb04-reopen-build-unit-release.txt` |
| Unit Release build, final | pass: 0 warnings, 0 errors | `proof/transcripts/sb04-reopen-build-unit-release-final.txt` |
| Web Release build | pass: 0 warnings, 0 errors | `proof/transcripts/sb04-reopen-build-web-release.txt` |
| Integration Release build, first | fail: 1 `CS0117` missing fixture symbol | `proof/transcripts/sb04-reopen-build-integration-release.txt` |
| Integration Release build, final | pass: 0 warnings, 0 errors | `proof/transcripts/sb04-reopen-build-integration-release-final.txt` |
| relay policy list/run | 24 discovered; 24/24 pass | `proof/transcripts/sb04-reopen-list-relay-policy-release.txt`; `proof/transcripts/sb04-reopen-run-relay-policy-release.txt` |
| compatibility list/run | 22 discovered; 22/22 pass | `proof/transcripts/sb04-reopen-list-openai-compatibility-release.txt`; `proof/transcripts/sb04-reopen-run-openai-compatibility-release.txt` |
| streaming list/run | 12 discovered; 12/12 pass | `proof/transcripts/sb04-reopen-list-streaming-release.txt`; `proof/transcripts/sb04-reopen-run-streaming-release.txt` |

The exact test counts are unchanged. Existing Unit and Integration Facts now assert omission-to-
false normalization, explicit-false preservation, invalid-value rejection before dispatch, and
the canonical body observed across the real Web/upstream boundary.

## SB02 downstream invalidation and restored trust

SB04 changed the SB02-owned invocation entity/configuration/migration/snapshot and public usage
surfaces. The original SB02 PASS remains historical. A durable additive overlay links fresh state
18/18, real PostgreSQL persistence 14/14, approved deletion/reference 6/6, and EF no-pending-model
proof. The initial deletion rerun failed only because sandboxed test bootstrap could not access the
user AppData package lock; the approved identical rerun passed 6/6.

Amending `20260824224847_AddSharedProviderPersistence` is valid only if that migration has never
been applied to a durable/non-disposable database. If it has, a new migration must be generated;
an already-recorded migration ID will not rerun.

## Progression decision

- result: `PASS`;
- next: SB05 only; proof inventories, hashes, validator, status, traceability, and review agree;
- downstream ownership remains unchanged: SB05 source networking/sync/reconciliation, SB06
  imported runtime/no-fallback, SB07 multi-instance proof, SB08-SB09 UI, SB10-SB11 docs/export,
  and SB12 the single broad aggregate and final running-stack closure.
