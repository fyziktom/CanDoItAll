# SB04 governed proof manifest

State: `PASS`.

## Baseline and owned scope

SB04 was executed on branch `providers-shared` from and still at commit
`e46f81d5ee33627dccb548732725e1c37e980ab5`. The uncommitted worktree already contained SB00-SB03;
`semantic-changed-files.md` is the SB04 semantic delta, while `changed-files.md` is the cumulative
worktree inventory. No commit, staging, discard, push, or unrelated-file overwrite occurred.

SB04 owns exactly three inference POST surfaces; publication-namespaced current-state routing;
strict Chat/Responses/Images normalization; five typed production adapter rows; connector-owned
URI/auth; bounded buffered/SSE transport; client-executed function-tool relay; Base64-only images;
metadata-only operation-aware invocation usage; and hosted recovery of stale `InProgress` rows.
Source networking/sync, imported runtime, multi-instance proof, UI, and the broad aggregate remain
downstream.

## Architecture evidence

The before snapshot is `snap-20260825012213-a17e36ed`. The force-refreshed post-repair snapshot is
`snap-20260825051057-300644c7`: 14 product projects, 752 documents, 35 modules, 5,158 dependency
facts, 34 direct product references, zero project cycles, unchanged two module plus one nested-type
cycle, and zero error findings. The sole product-reference delta is the authorized
`CanDoItAll.Modules.AgentFramework -> CanDoItAll.SharedProviders.Abstractions` edge. Web and
Workspace do not reference Http; Http depends only on Abstractions; Abstractions remains inward;
outer Composition owns concrete registration.

Public-surface review records additive `ImageCount` on relay usage, invocation completion/record,
usage contribution, and usage totals. Existing primary constructor/deconstruction arities are
preserved. The deterministic recovery schedule is internal and exposed to Integration tests only
through friend assembly access. No partial class, competing ledger, open-proxy seam, or duplicate
image persistence path was introduced.

## Frozen semantic contract

- Chat tools and named choice are nested under `function`; Responses uses flattened name fields.
- Named choices must match a declared tool; Chat `parallel_tool_calls`, even `false`, requires the
  advertised capability.
- Chat structured output is nested `response_format.json_schema`; Responses is `text.format`.
- Responses omission of `store` canonicalizes to JSON `false`; explicit JSON `false` is accepted;
  `true`, `null`, and every non-Boolean value reject before dispatch. The upstream request never
  depends on the provider's retention default.
- Chat text/image parts differ from Responses `input_text`/`input_image`; cross-surface shapes fail.
- Chat vision is user-role only. Data URI bytes must be nonempty, whitespace-free valid Base64 and
  PNG/JPEG/WebP; Chat detail is auto/low/high. The five production rows advertise no vision.
- Absent Images `n` defaults to 1; present string/null/fraction/overflow/non-positive/out-of-range
  values fail closed. Responses are Base64-only.
- Unavailable usage has no counts; partial token usage exactly one count; complete Chat/Responses
  both token counts; complete Images positive ImageCount only. Missing usage is never zero.
- EF configuration, migration, designer, and snapshot enforce operation-specific ImageCount 1–16;
  projection and aggregation reject inconsistent, non-positive, or mixed token/image observations.

The complete executable mapping is in `semantic-invariants.md`, `behavior/supported-denied-fields.md`,
and `behavior/relay-streaming-images.md`.

## Real PostgreSQL proof boundary

The 22-case compatibility lane uses the production Web/Workspace application services, current
catalog/routing state, secret resolver, PostgreSQL invocation persistence, hosted recovery worker,
and image target resolver. Its neutral fake dispatcher makes deterministic provider results; this
is not a live OpenAI/Ollama/ComfyUI network call.

`PersistedWorkspaceRelay_ResolvesRouteSecretAndFinalizesMetadataOnlyAudit` proves Chat,
Responses, and Images routing/secret/model/timeout resolution; metadata-only finalization; token
usage 2/3 and 4/5; ImageCount 1; aggregation; and unavailable missing usage.
`InterruptedInvocationRecovery_FinalizesOnlyStaleInProgressRecords` proves the actual hosted
worker and PostgreSQL stale-only recovery. `ImageExecutionTargetResolver_RequiresExactCurrentEligiblePublication`
proves current publication/profile/model/secret/capability eligibility in the Workspace resolver.

The recovery stale threshold is maximum relay timeout plus five minutes. Defaults are a 10-second
startup delay, one-minute interval, batch 100, and hard maximum 1000. The test schedule is 100 ms,
with stale/fresh ages of maximum timeout plus six/three minutes and a 20-second poll bound. Bounded
finalizer retry is source-reviewed; no forced save-failure/retry behavior is claimed.

## Honest failing-first and review chronology

The recorded first policy build genuinely fails because the neutral relay contracts and policy do
not yet exist. It predates the later semantic repairs and is not presented as their red source.
Historical host-fixture assertion corrections and superseded runs remain preserved. Independent
review then found material transport/image/recovery/ownership blockers, followed by real-Workspace,
wire-shape, ImageCount/ABI, policy, usage, recovery-schedule, and explicit malformed-`n` blockers.
Every blocker was repaired and the final read-only audit reports PASS with no remaining code/test
blocker. `architecture/semantic-reopen-review.md` and `architecture/independent-review.md` record
the chronology.

The August 25 wire-contract reopen passed its entry validator, then preserved two honest local
compile failures: the first Unit build reported four missing `JsonDocument`/`JsonValueKind`
symbols, and the first Integration build reported one missing `ResponsesModelId` fixture symbol.
After those narrow test repairs, Unit and Integration built cleanly; Web built cleanly on its first
reopen run. Existing Facts gained real-Web and recorded-upstream assertions for the Responses
`store` rule, so discovery remained exactly 24, 22, and 12.

## Authoritative commands and evidence

| Gate | Result | Artifact |
| --- | --- | --- |
| Web Release build | 0 warnings/errors; 23.389 s | `transcripts/sb04-build-web-release-closure-final.txt` |
| Unit Release build | 0 warnings/errors; 46.320 s | `transcripts/sb04-build-unit-release-after-image-count-fix.txt` |
| Integration Release build | 0 warnings/errors; 26.196 s | `transcripts/sb04-build-integration-release-closure-final.txt` |
| relay policy list/run | 24 discovered; 24 passed | `transcripts/sb04-list-relay-policy-release-after-image-count-fix.txt`; `transcripts/sb04-run-relay-policy-release-after-image-count-fix.txt` |
| compatibility list/run | 22 discovered; 22 passed | `transcripts/sb04-list-openai-compatibility-release-closure-final.txt`; `transcripts/sb04-run-openai-compatibility-release-closure-final.txt` |
| streaming list/run | 12 discovered; 12 passed | `transcripts/sb04-list-streaming-release-closure-final.txt`; `transcripts/sb04-run-streaming-release-closure-final.txt` |
| supporting usage list/run | 7 discovered; 7 passed | `transcripts/sb04-list-provider-usage-aggregation-release-semantic-final.txt`; `transcripts/sb04-run-provider-usage-aggregation-release-semantic-final.txt` |
| expanded anti-stub audit | 69 selected files pass | `transcripts/sb04-anti-stub-audit-semantic-final.txt` |
| expanded secret/content scan | pass; ImageCount identified as permitted metadata | `transcripts/sb04-secret-content-scan-semantic-final.txt` |
| ProjectReference refresh | exit 0; authorized one-edge delta | `transcripts/sb04-project-references-after-semantic-final.txt` |
| CodeAnalytics refresh | snapshot above; no project cycle/error finding | `architecture/codeanalytics-after.md` |
| SB02 restored trust | state 18/18, PostgreSQL 14/14, approved deletion 6/6, EF no drift | `bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/sb04-downstream-invalidation-revalidation.md` |

No broad, browser, paid/live-network, source-sync, UI, or multi-instance lane ran. The single broad
aggregate remains SB12-owned.

## August 25 wire-contract reopen commands and evidence

| Gate | Result | Artifact |
| --- | --- | --- |
| entry validator | pass | `transcripts/sb04-reopen-entry-validator.txt` |
| Unit Release build, first | fail: four `CS0103` JSON symbol errors | `transcripts/sb04-reopen-build-unit-release.txt` |
| Unit Release build, final | 0 warnings/errors | `transcripts/sb04-reopen-build-unit-release-final.txt` |
| Web Release build | 0 warnings/errors | `transcripts/sb04-reopen-build-web-release.txt` |
| Integration Release build, first | fail: one `CS0117` fixture-symbol error | `transcripts/sb04-reopen-build-integration-release.txt` |
| Integration Release build, final | 0 warnings/errors | `transcripts/sb04-reopen-build-integration-release-final.txt` |
| relay policy list/run | 24 discovered; 24 passed | `transcripts/sb04-reopen-list-relay-policy-release.txt`; `transcripts/sb04-reopen-run-relay-policy-release.txt` |
| compatibility list/run | 22 discovered; 22 passed | `transcripts/sb04-reopen-list-openai-compatibility-release.txt`; `transcripts/sb04-reopen-run-openai-compatibility-release.txt` |
| streaming list/run | 12 discovered; 12 passed | `transcripts/sb04-reopen-list-streaming-release.txt`; `transcripts/sb04-reopen-run-streaming-release.txt` |

## Migration assumption

Amending `20260824224847_AddSharedProviderPersistence` is valid only if it has never been applied to
a durable/non-disposable database. If it has, a new migration is required because an already-
recorded migration ID will not execute again. This is an explicit deployment assumption, not a
residual proof gap for a disposable development database.

## Progression decision

The complete evidence supports progression to SB05 alone. Inventories, proof hashes, closure
validator, status, handoff, review, and traceability agree; no later subbundle is unlocked.
