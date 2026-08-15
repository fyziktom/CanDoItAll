# Current-State Analysis

## Gate Verdict

The WIP bundle is not honestly resumable at SB13. Its named package blocker has been superseded, but later implementation, test-topology, documentation, architecture-guard, and build-graph changes invalidate the candidate and downstream checkpoints. Treat it as useful historical evidence and execute this successor from a fresh baseline.

Effective predecessor state: `Fail — repairable`, with no current external blocker.

## Historical Work Classification

| WIP work | Useful implementation retained | What the successor must do |
| --- | --- | --- |
| SB00 baseline classification | Nineteen historical stable failures were classified with zero unresolved branch-induced cases. | Record current test entry points and discovery; do not repeat obsolete case names/counts. |
| SB01 canonical transactions | Definition/conversation transaction ownership and PostgreSQL canonical state exist. | Re-prove and repair current CAS and definition-pin races. |
| SB02 turn state machine | Atomic admission/finalization, reducer, idempotent operation identity, cancellation, and compensation exist. | Repair replay availability ordering, cancellation notification, and evidence-based recovery. |
| SB03 profile fencing | Whole-use-case runtime profile fencing exists. | Reopened by later runtime-lease and SSE/profile fixes; re-prove at current head. |
| SB04 durable dispatch | Detached hosted dispatch, database leases, heartbeats, and remote cancellation exist. | Repair provider task supervision and define bounded concurrency/queue age/duration. |
| SB05 bounded reads | Keyset/bounded reads and provider context exist. | Repair public system-message filtering before paging and retain focused capacity proof. |
| SB06 CP1 | Historical backend checkpoint passed. | Invalidated by DI/runtime-lease changes; issue a successor checkpoint. |
| SB07 streaming providers | Provider-neutral incremental/fallback streaming and attempt audit exist. | Repair raw-exception logging and complete durable completion evidence. |
| SB08 durable event pipeline | Journal, coalescing, retention, transfer, and noncanonical partial output exist. | Repair coherent replay, cleanup starvation/bounds, high-water state, audit consistency, and transfer bounds. |
| SB09 HTTP/SSE | `202`, replay/gap/heartbeat/disconnect/terminal behavior and SSE transport exist. | Definitely reopened by four later SSE/profile lifecycle commits; retain fixes and add regression proof. |
| SB10 external contract | Exact scopes, server-owned origin, redaction, and Problem Details mapping exist in current source. | Repair validation, prompt exposure, editor completeness, audit DTO, metadata, and fingerprint exposure. |
| SB11 CP2/source proof | Linux/provider/PostgreSQL/SSE proof existed for an older graph. | Replace obsolete package-mode proof with current pinned sibling-source proof. |
| SB12 cleanup/docs | Historical guards and docs existed. | Re-run against current source; several claims and paths are stale. |
| SB13 release | Never ran restore/build/stable/pending-model/matrix closure. | Run only after successor work, once at a frozen commit with current commands. |

## Why The Recorded Blocker Is Stale

- WIP `bundle-status.json` records SB13 blocked at candidate `dea90cfd...` by unavailable Spreadsheet package `0.1.18`.
- Commit `738ca377` changed the repository and CI to pinned sibling Components/FileTools source; WIP ADR-H13 explicitly says the unpublished feed is no longer a prerequisite.
- Current `docs/testing.md` declares `CanDoItAll.slnx` a product-only graph with no tests. WIP SB13's old solution test command therefore cannot prove anything.
- Commit `a06def5` moved tests into `tests/Solutions/*.slnx` and changed namespaces, invalidating old filters and discovery assumptions.
- WIP `CHECKSUMS.sha256` has three current mismatches: `architecture/12-architecture-decision-register.md`, `inventories/03-prior-failure-classification-template.md`, and `scripts/check_architecture_boundaries.py`.
- Root status, subbundle status, manifests, and final reviews contradict one another; one final link names a nonexistent CP0 file.

## Post-Candidate Invalidation

| Commit | Current effect | Reopened proof |
| --- | --- | --- |
| `738ca377` | Replaces package dependency gate with pinned sibling source. | SB11, SB13, CP2, FINAL |
| `ec55926` | Repairs scoped provider resolution and runtime-lease callback/disposal races. | SB03, SB06/CP1, downstream |
| `b55def2` | Stabilizes profile-bound SSE response completion. | SB09, CP2, FINAL |
| `b266e172` | Keeps profile-bound SSE frames atomic. | SB09, CP2, FINAL |
| `d4da7efa` | Normalizes profile-switch cancellation. | SB03, SB09, CP2, FINAL |
| `4de328cb` | Drains pending SSE reads before request-scope release. | SB09, CP2, FINAL |
| `a06def5` | Moves tests to lane solutions/namespaces and changes CI/docs. | All recorded test discovery/filter evidence and SB13 commands |
| `a8e3f87` | Adds CodeAnalytics scripts. | Architecture validation inputs |

## Confirmed Current Defects

| Priority | Defect | Source evidence | Owning work unit |
| --- | --- | --- | --- |
| Critical | Event retention repeatedly selects already-empty oldest operations, so newer eligible events can be starved forever; one batch can also delete millions of rows because `take` counts operations. | `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories/EfLlmChatOperationEventRepository.cs` | SB06 |
| Critical | A heartbeat/control exception after provider start can exit without cancelling and awaiting the provider task, orphaning paid work. | `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationExecutor.cs` | SB04 |
| High security | Read-scoped transcript paging includes persisted `System` messages and Web mapping returns their content, exposing definition system prompts. | `LlmConversationService`, `EfLlmChatConversationReadStore`, `LlmChatApiMapper` | SB02 |
| High | A committed idempotent turn replay is rejected with 503 when no local executor is available because availability is checked before existing-operation resolution. | `LlmChatOperationApplicationService.SendAsync` | SB03 |
| High | Cancellation lookup races registration disposal and callbacks can throw after durable cancellation committed, producing an inconsistent 500 response. | `LlmChatOperationCancellationRegistry`, `CancelAsync` | SB03 |
| High | Definition update/status and conversation rename can leak `DbUpdateConcurrencyException` after a real two-context CAS race instead of stable 409. | application services and EF repositories | SB03 |
| High | Conversation creation reads active definition/current revision before its unit-of-work transaction and can pin stale or no-longer-active state. | `LlmChatConversationApplicationService.CreateAsync` | SB03 |
| High | `RecoveryRequired` lacks a Web reconcile route and cannot deterministically settle durable failed/cancelled evidence, despite an application reconcile contract. | state machine, lease service, `LlmChatOperationsApi` | SB04 |
| High | Operation details load invocation records but the Web response discards them, contradicting docs and removing durable evidence after SSE retention. | `LlmChatOperationDetailsReader`, operation mapper/contracts | SB05 |
| High | `llm.response.completed` omits model/finish reason/delivery mode promised by the event schema because these facts are not fully persisted. | audited streaming port and event API mapper/contracts | SB05 |
| High | Stream-limit failures can record different failure categories in operation, invocation, and SSE evidence. | streaming pipeline and audited streaming port | SB05 |
| High | Replay page assembly runs several unrelated read-committed queries, allowing terminal event data and operation snapshot to come from different commits. | `EfLlmChatOperationEventRepository.ListAfterAsync` | SB06 |
| Medium/High | One serial dispatcher has no bounded concurrency, maximum queue age, saturation truth, or total operation duration although the WIP spec requires those bounds. | dispatcher hosted service/options | SB07 |
| Medium/High | Streaming options are default-constructed rather than configuration-bound/startup-validated; byte/event/text limits disagree with persistence bounds. | streaming options, DI, entity configuration | SB07 |
| Medium | Event sequence regresses to zero after retention deletes all rows because `MAX(sequence)` is used as the high-water authority. | event repository and details reader | SB05/SB06 |
| Medium | Singleton signal and retention-schedule dictionaries never evict completed operation/profile-generation keys. | event signal and retention schedule | SB06 |
| Medium security | Definition/conversation routes construct typed IDs from empty GUIDs, paging validation is inconsistent, GET conversation omits documented 400 metadata, and binder tests assert only status. | `LlmChatsApi.cs`, API tests | SB02 |
| Medium privacy | Public operation DTO exposes an internal fingerprint derived from exact prompt/settings even though clients do not need it. | operation API contracts/mapper | SB02 |
| Medium | Transfer import under-validates enum/state relationships and materializes an unbounded graph. | `LlmChatsTransferDocument.cs` | SB07 |
| Medium | Provider streaming adapter logs exception objects whose nested driver exceptions may contain raw provider response details. | provider runtime adapter and driver protocol | SB08 |
| Refactor | One 466-line Web owner mixes definition and conversation endpoint mapping/handlers. | `LlmChatsApi.cs` | SB02 |

## Contract Gaps And Decisions

- `LlmChatOperationKind.Cancel` and `.Recover` have no production or test consumers. Remove them; cancellation/recovery remain actions/statuses.
- `docs/llm-chats-api.md` promises invocation evidence. Implement a bounded sanitized projection rather than weakening the documentation or exposing raw internal records.
- The public read definition intentionally redacts system prompt, but no manage-scoped read allows safe editor round-trip. Add a manage-only editor projection; never put the prompt back into read scope.
- Strict unknown-member model binding must produce stable `llm-chat.invalid-request` Problem Details, not merely status 400.
- Conversation creation idempotency remains deliberately deferred, with the existing no-blind-retry description, because caller namespace belongs to a future deployment/identity aggregate.
- `LlmChatConversationEngine` length alone is not a responsibility violation. No split is planned unless the implementation work discovers a separately owned/testable responsibility and reopens the architecture decision.

## Architecture Baseline

- Scoped CodeAnalytics graph: nine product projects, 1,354 types, 9,546 members, 43 service registrations, zero cycles.
- Required direction remains Models/Providers/LLM abstractions inward, LLM Chat core independent of EF/Web, persistence implementing ports, Composition wiring, and Web depending on the product module rather than persistence.
- No new project boundary or project reference is required. The only locked structural extraction is within the existing Web project.

## Preparation Boundary

- No product source or test implementation was changed.
- No test suite was executed. Existing source/test names were inspected only to make the successor executable.
