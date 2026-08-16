# Simple LLM Chats Backend Completion

This initiative bundle finishes and re-proves the backend-only Simple LLM Chats capability. It supersedes the execution plan of `Simple-Llm-Chats-Hardening-Sse`; that bundle remains historical evidence and must not be edited to manufacture closure.

## Profile

- `initiative`

## Mission

- Preserve the sound backend delivered by the two predecessor bundles, repair the concrete correctness, security, durability, lifecycle, and capacity defects found in the current implementation, perform only the justified local backend refactors, and close the feature against the repository's current source-based build and test workflow.

## Outcome Contract

- Requested outcome: an implementation-ready successor bundle for all currently identified Simple LLM Chats backend work.
- Hard constraints: prepare only; do not implement product code in this bundle-preparation turn; no Razor, Blazor, shared chat component, floating chat, or other UI integration; ordinary chats remain separate from agent execution; PostgreSQL remains canonical; public contracts remain strongly typed and fail closed.
- Evidence required before closure: focused failing-first and passing proof for each changed behavior, real PostgreSQL and real Web-host proof where those boundaries matter, current test discovery records, architecture and security review, one broad stable gate at the frozen final checkpoint, and same-commit Windows/Linux/macOS CI evidence.
- Known blockers: none at preparation time. The unpublished `CanDoItAll.Components.Spreadsheet 0.1.18` package recorded by the predecessor is no longer a prerequisite; current builds use pinned sibling Components and FileTools source.
- Explicit scope exceptions: UI integration, live external-provider certification, retrieval/RAG, moderation, external participants/channels, and organization/per-user ownership. Conversation creation remains explicitly non-idempotent until a deployment-owned caller identity namespace exists; this bundle must preserve and document that contract rather than invent a global idempotency namespace.

## Audit Verdict

- `Simple-Llm-Chats-Backend-Api` and `Simple-Llm-Chats-Hardening-Sse` contain substantial implemented work worth retaining.
- The hardening bundle is effectively `Fail (repairable)`, not `Blocked`: its package blocker is stale, its CP1/CP2/FINAL proofs were invalidated by later production and test-layout commits, and its checksums and status files disagree with the current tree.
- Current analyzed baseline: clean product tree at Git commit `a8e3f87e9ac917357c13fae56ab5eb1f0659521d` before this new bundle was added. Execution must record its actual start commit and re-audit drift from this baseline.
- CodeAnalytics baseline snapshot: `snap-20260815201127-356b279c`; nine affected product projects, zero dependency cycles, and no diagnostics. The final snapshot must cover the changed-source union rather than reuse this identifier as proof.

## What Is Already Implemented And Retained

- Strongly typed definitions, immutable revisions, conversations, operations, profile identities, fingerprints, and explicit concurrency tokens.
- PostgreSQL canonical state, atomic turn admission/finalization, durable dispatch leases, cancellation/recovery state, provider-neutral invocation, durable attempt audit, replayable operation-event journal, SSE transport, and database transfer.
- Exact LLM Chat read/manage/execute authorization scopes and server-owned `Api` conversation origin.
- Later source fixes for scoped provider resolution, runtime-lease notification/disposal races, SSE response completion, frame atomicity, profile-switch cancellation normalization, and pending-read draining.
- Pinned sibling-source Components/FileTools build graph and the current lane-specific test solutions under `tests/Solutions`.

## Work Still Required

1. Reconcile the stale predecessor status and establish executable current-head proof.
2. Repair API validation, sensitive transcript/editor boundaries, public audit projection, and transport ownership.
3. Close transactional CAS, definition-pin, durable replay, cancellation-notification, and recovery correctness gaps.
4. Supervise provider tasks on every executor exit and define bounded dispatcher/queue/operation behavior.
5. Complete the durable invocation/SSE schema and make operation cursors monotonic across retention.
6. Make replay reads coherent, cleanup row-bounded and starvation-free, and singleton transient state evictable.
7. Bind and validate streaming/dispatcher limits and harden transfer validation/materialization.
8. Prevent raw provider response details and exception objects from entering logs.
9. Re-prove the post-bundle profile/SSE/DI fixes and run the C# architecture gate.
10. Close once at one frozen commit with current local and three-OS CI evidence.

## Bundle Layout

- `inputs/`: verbatim request, source authority, and normalized intake
- `analysis/`: predecessor reconciliation, current defects, assumptions, risks, and explicit decisions
- `requirements/`: stable, testable requirements
- `architecture/`: current/target ownership, dependency direction, pattern decisions, and testability
- `plan/`: ordered work, checkpoints, invalidation, and final broad-gate trigger
- `traceability/`: requirement-to-work-unit-to-proof closure
- `subbundles/`: ten execution-ready work units
- `reviews/`: preparation self-review, execution ledger, and final gate target

## Recommended Execution Order

1. `01-current-state-reconciliation-and-executable-baseline`
2. `02-api-contract-correctness-and-transport-ownership`
3. `03-transactional-command-correctness`
4. `04-execution-supervision-and-recovery`
5. `05-durable-audit-and-sse-contract`
6. `06-replay-retention-and-transient-state`
7. `07-bounded-dispatch-configuration-and-transfer`
8. `08-provider-failure-redaction` executes after SB07; SB05 and SB08 both own changes in the shared ProviderRuntime adapter and are deliberately serialized.
9. `09-sse-profile-lifecycle-and-architecture-checkpoint`
10. `10-release-evidence-and-closure`

See `plan/01-phase-plan.md` for the authoritative dependency and invalidation map.

## UI Target Policy

- N/A to execution: this bundle changes no browser-visible UI and requires no Playwright or screenshot proof.
- A later, separate bundle owns chat component and UI integration work.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Pass`
- Execution status: `Blocked at SB10 entry — SB01 through SB09 and CP0 through CP3 passed; the application candidate is not yet a commit and same-commit three-OS CI cannot be dispatched without commit/push authority`
- Subbundle gate review: `SB01 Pass; SB02 Pass; SB03 Pass; SB04/CP1 Pass; SB05 Pass; SB06 Pass; SB07 Pass; SB08 Pass; CP2 Pass; SB09/CP3 Pass; SB10 Blocked before broad execution`
- Final closure gate: `Blocked — broad Stable remains deliberately unrun until a frozen application commit and CI authority exist`
- Browser validation analytics: `N/A — backend only`
