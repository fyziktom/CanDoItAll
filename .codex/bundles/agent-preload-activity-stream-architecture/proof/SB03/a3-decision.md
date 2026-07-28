# SB03 A3 Revisioned Preparation Decision

## Decision

`PASS with two A3 P2 follow-ups`

Date: `2026-07-27`

SB04 was authorized and its downstream A4/A5/A6 validation did not contradict the
preparation foundation.

## Determining evidence

- `AgentExecutionPreparationCache` is a bounded, typed, per-agent single-flight map.
  Completed entries may be evicted; a full set of in-flight entries is rejected
  explicitly rather than growing without bound.
- `AgentExecutionPreparationBlueprint` defensively copies its agent, provider,
  capability, permission, tag, secret-reference, and memory data. Live agents,
  provider clients, tools, MCP sessions, chat sessions, credentials, authorization
  results, approvals, context-contributor output, and `DbContext` instances are not
  members of the blueprint.
- The version fence combines catalog data revision, database-profile generation, and
  selected-provider configuration fingerprint. Invalidation cancels stale shared work,
  and a superseded completion cannot publish over the current entry.
- Shared factory work is service-owned. A caller waits with its own cancellation token,
  so cancelling the first or a later waiter does not cancel the shared load for other
  callers.
- Reference-data snapshots use the same service-owned cancellation boundary, are
  defensively copied, invalidate across scopes through the shared hub, and never overlap
  operations on the same workspace service.
- Provider state is an immutable publication with explicit `NotReady`/`Faulted`
  behavior. A profile switch advances the publication fence; provider deletion,
  revision-probe failure, and mapping failure fail explicitly without a catalog
  fallback.
- Existing SB03 transcripts preserve 13/13 focused preparation/provider results, 4/4
  startup scenarios, the owned production compile, and the anti-stub scan. The current
  parent validation handoff also reports the focused architecture unit suite at
  140/140. The original command stream for that 140/140 handoff was not retained, so
  this record does not invent a transcript.
- SB05 operation-count proof records one immutable catalog snapshot read, one provider
  acquisition, zero provider registry gets, zero session gets, and zero run-summary
  lists in each final scenario. Its provider SQL proof records one scalar revision
  command for unchanged warm, zero for synthetic, and three for changed provider state;
  no zero-database-read claim is made for a non-synthetic warm acquisition. A5 returned
  `GO with three P2 follow-ups`.
- Final CodeAnalytics snapshot `snap-20260728014834-63e19a8b` reports an acyclic
  affected project graph.

## Acceptance disposition

| Acceptance | Evidence | Result |
| --- | --- | --- |
| Defensive immutability | blueprint construction plus `Blueprint_is_a_deep_data_snapshot_without_live_runtime_resources` | Pass |
| Stale in-flight fencing | `Invalidation_during_load_fences_stale_completion`, `Revision_change_during_cold_load_discards_old_blueprint_and_retries`, and `Superseded_profile_rebuild_cannot_publish_old_data` | Pass |
| Independent waiter cancellation | preparation-cache cancellation test plus reference-data first/later waiter cancellation tests | Pass |
| Warm work reduction | preparation reuse tests and SB05 deterministic operation counts | Pass |
| Per-run live resources | dispatch credential-scope tests, runtime factory construction, and A5 architecture review | Pass |
| Use-time identity validation | catalog/profile/provider change and deletion tests return typed stale results and reprepare/fail closed | Pass |
| Truthful phase source | typed `Reused`/`Refreshed` disposition and startup cache counters | Pass |

## Scope exceptions

- Skill parsing, tool creation, credential resolution, runtime-agent/session creation,
  current authorization, and context contribution stay per dispatch.
- No claim is made that provider validation and external provider use are distributed-
  transaction atomic across hosts.
- Physical WAL/directory durability is outside A3 and remains an SB05 P2.

## Residual P2 follow-ups

1. `DatabaseSwitchNotificationService.Publish` invokes subscribers synchronously, so
   a blocked subscriber can delay the switching thread.
2. Another host can commit a provider revision after the final scalar probe and before
   external provider use. A distributed lease/version boundary is required if stronger
   multi-host consistency becomes necessary.

SB05 separately retains the WAL physical-flush P2. None of these findings permits
serving a known-stale blueprint, retaining secrets/live runtime objects, silently
falling back to catalog state, or sharing a `DbContext`.

## Evidence provenance limitation

SB01 preserves the explicit pre-change red contracts in
`bundle://proof/SB01/deferred-characterization-contracts.md`, but no raw failing-first
console transcript was retained for the final A3 cache tests. Existing direct SB03
transcripts and the current source/tests are cited as such; this decision does not
reconstruct missing console output or claim a test count beyond the retained 13/13,
4/4, and parent-confirmed 140/140 results.

## Progression and reopen rule

SB04 progression is authorized. Reopen A3 and all downstream gates if a blueprint
retains a live/secret/request-specific resource, a stale completion publishes, one
waiter poisons another, a warm dispatch reintroduces duplicate canonical reads, or
provider/profile validation silently falls back.
