# Normalized Requirements

Statuses below describe planned implementation. Only R012 is fulfilled by this
preparation turn after its readiness gate passes. Product requirements remain unimplemented.

| ID | Inputs | Required behavior | Acceptance / disproof | Owner phases |
|---|---|---|---|---|
| R001 | N001 | Shared relay records retain execution-time price evidence with provider-reported/calculated/free/partial/unavailable provenance and currency. | Buffered and terminal-stream use are priced when supported; missing/overflow/unsupported evidence stays explicit. Current tariffs never overwrite old unknowns. | SB02, SB04, SB08 |
| R002 | N002 | Record verified issuer, subject and managed credential ID independently for inbound requests; safe local/legacy/unavailable kinds remain distinct. | Two active managed keys for one subject are distinguishable; forged context cannot grant access or alter identity. Bearer/token/secret material is absent from storage/logs/UI. | SB01, SB04, SB06, SB08 |
| R003 | N003 | A provider History tab appears next to Sharing and is locked to that local provider scope. | Reuses the shared panel; Search/Enter causes zero provider saves; no nested EditForm; provider scope cannot be widened by a query DTO. | SB06, SB07 |
| R004 | N004 | Search starts only on explicit request with a bounded UTC interval and filters; no history/totals/details/historical facets load on mount, tab change or draft edit. | Service/network spies show zero reads until Search. Draft/applied filters are distinct; cancel/stale replies do not overwrite newer results. Query is scalar, server-filtered and bounded. | SB06, SB07, SB08 |
| R005 | N005 | An Agents History tab searches all authorized providers in the current instance/database/security partition. | Both local/imported profiles use the same pipeline; no remote log federation, dashboard aggregate load or unauthorized rows/counts. | SB06, SB07, SB08 |
| R006 | N006 | Reuse canonical agent, simple-chat, workflow/process and relay evidence; index compact metadata/owner references only. | One shared observation with several owners has one charge/attempt. A pending/late owner never causes copied canonical prompts. Deleted owners and stale replay reconcile correctly. | SB01, SB03–SB06, SB08 |
| R007 | N007 | Provider/model identify search dimensions; actual logical operation/attempt/source IDs identify calls. Capture untracked calls across all identified production paths. | MAF, buffered/streaming chat, batch, relay, image, voice and operational/diagnostic paths have explicit coverage. Retry creates distinct observed attempts; recovered completed batch results send none. Legacy aggregates are not split into invented attempts. | SB01, SB04, SB05, SB08 |
| R008 | N008 | General per-partition policy controls Light/Detailed, direct/relay retention, detail retention/byte quota and bounded cleanup. Canonical projections follow original owner lifetime. | Validated concurrency-aware settings; expiry applies at read time before GC; no active-attempt deletion, expiry extension on replay or silent oldest-row eviction. Old retained canonical evidence remains searchable. | SB03, SB05, SB07, SB08 |
| R009 | N009 | Default Light contains metadata only; optional Detailed captures protected bounded current-turn input and per-attempt response with completeness/omission reasons. | No snippets in Light. Unsupported arbitrary relay shape, missing keys, quota or truncation are explicit. No binary media, provider configuration, headers, system/tool/RAG expansions or raw exception payloads are copied. | SB03, SB04, SB06, SB07 |
| R010 | N010 | Do not duplicate complete prior conversations or a retry's identical logical input. | Canonical content remains at owner; standalone input is captured once per operation/input revision and reused by retry links. Long-conversation/stream fixtures stay bounded independently of transcript length. | SB03–SB05, SB08 |
| R011 | N011 | Maintain neutral contracts, clear owner/adaptor boundaries, small cohesive files and predictable performance. | New dependency graph is acyclic; forbidden edges/public signatures fail guards. No new runtime partial/manager/reflection classifier. SQL uses keyset/index/Take; file work is incremental outside interactive queries. | SB01–SB09 |
| R012 | N012 | Prepare a detailed analyzed implementation bundle using named architecture/performance skills; do not implement now. | Source inventories, two-pass performance report, contracts, phases, traceability and preparation validation exist. No production edit, build/test/inference/migration/deployment/settings mutation in this turn. | Preparation, SB09 audit |
| R013 | N002, N004–N009 | Separate metadata, content and policy-management authorization, with trusted context and per-owner detail checks. | Invoke/catalog grant does not grant history. Missing local authority denies. Permission/profile change while awaiting invalidates results before publication; details cannot bypass source-resource permission. | SB01, SB06–SB08 |
| R014 | N006–N011 | Capture/replay/retention operate durably across cancellation, crash, concurrent hosts and profile transfer, without repeating inference. | Begin persists before dispatch; same-context EF outbox and file journal recover every handoff; terminal errors never retry the provider. Additive migration and rollback/restore pass on disposable data. | SB03–SB06, SB08 |

## Fixed Limits And Semantics

- Default draft range: last 24 hours; page 50, hard maximum 200; selected interval at most
  31 days but any retained age; query deadline 10 seconds.
- Live keyset ordering: immutable SortAtUtc + EntryId. Legacy TimeBasis is explicit;
  actual StartedAtUtc/AttemptId may be absent. No insertion snapshot promise or automatic totals.
- Light default; direct/relay metadata 30 days; optional history-owned detail 7 days,
  no longer than metadata, 32 KiB UTF-8 per field (hard policy maximum 128 KiB).
- Detail quota 256 MiB per partition; maintenance batch 500, hard maximum 1,000.
  These are proposed configurable product defaults, not measurements or compliance claims.
- Stable partition identity survives restart/switch-back; transient profile generation
  fences current operations, cursors and result publication.
- Legacy source identity excludes mutable source version. Version controls monotonic
  updates, not uniqueness. Late canonical evidence after orphan expiry remains independently
  indexable under its owner's lifetime; expired detail and deleted sources never revive.

## Scope Exclusions

No exact-person IDM/EGCP mapping, billing reconciliation, global cross-instance charge
deduplication, full-text body search, export, wire replay, content-addressed prompt store,
sibling RAG instrumentation, mobile redesign, shared component changes or unrelated
large-file refactoring. Existing shared API compatibility, retries, tools/approvals,
provider secrets and transcript lifecycles remain intact.

## Required Outcome Evidence

Each product requirement needs a real source/producer path, focused positive and negative
tests, and the relevant runtime/SQL/UI artifacts. A populated screen, green unit test,
successfully submitted command or existing old-bundle status alone is not closure proof.
See [validation strategy](../plan/02-validation-strategy.md) and
[traceability](../traceability/01-requirement-traceability.md).
