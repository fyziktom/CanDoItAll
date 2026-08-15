# SB09 — SSE/Profile Lifecycle And Architecture Checkpoint

## Status

- `Ready`

## Objective

- Re-prove the post-WIP scoped-DI/runtime-lease/SSE fixes together with the completed backend, and gate the final changed-source architecture before release evidence.

## Success Criteria

- Scoped provider resolution and runtime-lease notification/disposal races pass current direct tests.
- Profile switch before response start, during a frame, and during a pending read closes normally without partial frames or early request-scope release.
- Durable replay/gap/heartbeat/terminal/disconnect/cancel behavior remains correct with one provider dispatch.
- Current exact auth/origin/redaction contracts pass the real Web/PostgreSQL host.
- Successor-owned architecture guards and a new CodeAnalytics snapshot show correct ownership, no new project/reference/cycle/partial, and no anti-stub.
- CP3 independent architecture review is `Pass`.

## Covered Inputs

- BC-001, BC-002, BC-006, BC-080 through BC-084.

## Prerequisites

- SB02-SB08 all `Pass`.
- CP1 and CP2 current at the candidate commit.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/CanonicalLlmChatProviderResolver.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/DatabaseProfileLlmChatRuntimeLease.cs`
- `repo://src/App/CanDoItAll.Web/Api/Streaming/LlmChatOperationEventReplayReader.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationEventStreamSession.cs`
- `repo://src/App/CanDoItAll.Web/Api/Streaming/ServerSentEventResponseWriter.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatBackendCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatProviderRuntimeTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ApiStreamingTransportTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiPostgreSqlIntegrationTests.cs`
- `repo://codex/bundles/Simple-Llm-Chats-Hardening-Sse/scripts/check_architecture_boundaries.py`

## UI Composition Contract

- N/A — SSE is HTTP transport, not a browser-rendered UI surface.

## Deliverables

- Current focused regression union for all later DI/runtime-lease/SSE fixes plus completed backend behavior.
- Successor-owned current architecture/source guard with tests if needed; do not trust the mismatched old checksum as proof.
- New CodeAnalytics snapshot covering every changed production project and direct consumers.
- Governed CP3 manifest, semantic invariants, hashes/transcripts, and independent architecture gate.

## Dependency Impact

- Final focused trust boundary. SB10 may run only against the exact CP3 candidate commit.

## Validation Depth

- Proof tier: `Governed`.
- Test solutions: Unit and Integration lanes.
- Filters: exact existing/current profile/SSE cases plus the focused `LlmChatsApiPostgreSqlIntegrationTests` class and bounded new cases from SB02-SB07.
- Selection reason: later commits repaired precise lifetime behavior after the old CP2, and final architecture must cover the changed union.
- Expected existing named cases: `Runtime_lease_unsubscribes_from_profile_notifications_when_disposed`, `Runtime_lease_ignores_a_profile_notification_captured_before_disposal`, `Lease_cancels_and_fails_when_profile_generation_changes`, `Profile_switch_during_dispatch_cancels_the_invocation_and_prevents_assistant_commit`, `Streaming_writer_treats_profile_switch_before_response_start_as_normal_completion`, `Streaming_writer_finishes_an_in_progress_frame_before_profile_switch_closes_the_response`, `Streaming_writer_drains_an_in_progress_read_before_profile_switch_releases_the_request_scope`, `Profile_switch_before_finalization_retains_committed_usage_and_blocks_later_writes`, `DurableSse_ReconnectsAfterDeltaWithoutRedispatchAndClosesAfterOneTerminalEvent`, `ReadAsync_reports_gap_and_replays_only_the_bounded_window`, `Streaming_writer_emits_heartbeats_and_treats_disconnect_as_normal_completion`, `Streaming_writer_emits_typed_public_envelopes_and_closes_at_terminal_event`, `CancellationApi_PersistsCancellationAndOperationStatusReturnsIt`, `AuthorizationEnabledHost_EnforcesDistinctScopesAndAuthenticatesSseOnlyThroughBearerHeader`, and `ConversationApi_PersistsServerOwnedApiOriginAndRejectsSpoofedOrigin` (15 existing cases), plus the exact accepted focused cases added by SB02-SB08.
- Expected discovery: each of the 15 existing names discovers exactly one case; accepted new-case counts equal their closed subbundle manifests. Any mismatch fails CP3.
- Invalidation keys: scoped DI registration, runtime lease callback/dispose, replay reader/session, SSE writer/cancellation, API/auth, any changed source/project reference, architecture guard, test filter.
- Broad-gate decision: not run here; required once in SB10 at this frozen CP3 commit.

## Implementation Steps

1. Freeze a candidate commit after serialized SB08 closure; verify no unclassified changed file.
2. List and run the fifteen exact existing lifetime/SSE/contract cases plus the accepted new focused union.
3. Run the real PostgreSQL host flow for `202`, dispatch, deltas, reconnect, profile switch, cancel, retention gap, terminal close, safe API/audit, and no redispatch.
4. Create/repair successor-owned architecture guards for current paths and forbidden dependencies; add self-tests for the guards where appropriate.
5. Take a new CodeAnalytics snapshot and query project references, cycles, service lifetimes, and the changed symbols/direct consumers.
6. Run source assertions for no inline HTTP provider call, no file-store activation, no Web persistence reference, no new agent dependency, no partial split, and no raw secrets.
7. Produce Governed artifacts with hashes and independent C# architecture review.

## C# Architecture Impact

- Validation/consolidation only unless a discovered regression requires reopening its owning work unit.

## Boundary Ownership

- Confirms all boundaries in `architecture/01-csharp-boundary-map.md` after the full implementation union.

## Dependency Direction

- Zero new cycle and exact allowed edges; any new edge is a stop/re-entry condition.

## Pattern Decision

- Reviews PSR-01 through PSR-11 against actual source; no retroactive rationale.

## Testability Contract

- Exact named lifetime cases and real host/PostgreSQL flow; no source-only claim can replace behavior.

## Partial Class Policy

- Guard must reject affected partial class/record/struct declarations and verify the Web split uses distinct types.

## Architecture Proof Required

- CodeAnalytics snapshot/query output, guard/self-test output, changed-file hash manifest, semantic invariants, direct-test mapping, anti-stub scan, and independent `csharp-architecture-review-gate` result.

## Scope Exceptions

- No broad Stable aggregate and no UI/browser proof in this checkpoint.

## Do Not Do

- Do not fix a regression inside SB09 without reopening its owner and rerunning downstream proof.
- Do not reuse old WIP guard checksums, CodeAnalytics snapshot, or CP2 result as current proof.

## Acceptance Checklist

- [ ] Fifteen existing cases and the accepted new focused union discover exactly as expected and pass.
- [ ] Real PostgreSQL HTTP/SSE flow passes with one dispatch and safe outputs.
- [ ] Architecture guards/self-tests and CodeAnalytics pass with zero cycles/forbidden edges.
- [ ] Governed manifest/hashes/semantic invariants complete.
- [ ] Independent CP3 review passes.

## Proof Required

- Exact discovery/execution transcripts, real-host event/row summaries, new snapshot/query output, guard/self-test output, portable hashes, semantic invariants, anti-stub and independent review artifacts under `proof/SB09`.

## Browser Validation Logging

- N/A — HTTP/SSE transport only; no rendered UI or screenshots.

## Progression Gate

- Freeze the CP3 application commit. SB10 may change only proof/status/non-runtime documentation; otherwise reopen the owner and CP3.

## Reopen Triggers

- Any production/project/build/test/workflow change after CP3 reopens the affected owner, CP3, and SB10.

## Suggested Agent Prompt

```text
Execute SB09 only after serialized SB02-SB08 pass. Freeze the candidate, re-prove the exact profile/SSE lifetime and contract union, run current architecture/CodeAnalytics guards, and obtain an independent Governed CP3 decision. Reopen owners instead of fixing findings silently here.
```
