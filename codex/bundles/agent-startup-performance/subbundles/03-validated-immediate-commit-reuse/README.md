# SB03 — Validated Immediate Commit Reuse

## Status

- Status: Completed
- Focused gate: Pass. Execution authorization and Phase 0 are satisfied. Integrated real UI, native approvals, performance and final host checkpoint passed; see proof/SB03/manifest.md and proof/SB03/ui/validation-summary.md.

## Objective

Remove up to five redundant raw comparison reads from changed payloads in the immediate existing-run commit, while retaining every fresh conflict-validation read, both journal validations, the full chat-target rebuild, complete recovery behavior, metadata publication and durability.

## Covered Inputs

- N001/N003/N004; R03/R04/R05/R06/R07/R08/R10.
- Owns combined real UI5032/UI5214 and measured-performance closure after all three units. This does not implement recommendation4.

## Prerequisites

- Execution authorization, Phase0 baseline and SB01 security/downstream gate: satisfied; see inputs/04-execution-request.md and proof/SB01/independent-review.md.
- SB02 may be independent of implementation but must pass before frozen combined proof.
- Characterize required timestamps/index metadata and all recovery boundaries first.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs`

Symbols/ownership: UpdateExecutionRunDetailAsync, SaveExecutionRunDetailCoreAsync, CommitExistingRunDetailJournalAsync/recovery; generic-new-run immediate/recovery distinction.

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs`

Symbols/ownership: PrepareExistingRunUpdateAsync, slice persistence, projection calculation, persisted-state validators (resolve exact current symbols by file outline).

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceChatProjectionStore.cs`

Symbols/ownership: existing-run projection prepare/validate/commit.

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs`

Symbols/ownership: unchanged durable JSON/history boundary.

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs`

Symbols/ownership: AppendExecutionLogAsync unchanged behavior oracle.

- `bundle://plan/test-selection.md`

Owning tests, selectors and crash-host discovery are defined there.


## Deliverables

- Locally controlled immediate path reuses freshly prepared/validated plans while holding the same gate and cross-process lock.
- Full recovery validation retained for deserialized journals, restart, lock reacquisition and every untrusted entry.
- Only proven unaffected transformations are reused; required index/activity changes still published.
- Governed recovery/concurrency/performance proof and combined real browser/host gate.

## Implementation Steps

1. Characterize immediate and recovery paths, per-collection reads, canonical projection outputs and all failure boundaries.
2. Make immediate trust explicit and private/typed; a recovered journal cannot opt into trust. Preserve journal schema/version/previous-target snapshots, order and deletion.
3. Reuse already validated in-memory plans within the uninterrupted lock; retain fresh actual write/commit safety. Do not assume a workspace lock protects against noncooperating filesystem edits where the old contract checked them.
4. Apply the accepted narrow branch only: after the existing fresh typed read and transition validation, a Prepared payload that differs from its target uses the unchanged atomic writer directly. Matching typed targets and every RecoveredJournal path retain WriteJsonIfChangedAsync, including its fresh raw comparison and canonicalization. Scope is session, run, execution index, aggregate usage index and the separate chat-index owner. Do not change JsonStore globally, ProviderUsageObservation/history, collection diffs, either journal validation, or complete chat-target rebuilding. Preserve run/session UpdatedAtUtc, agent LastUsedAtUtc, all revision metadata and summary selection.
5. Preserve pre-journal cancellation/no writes and post-journal CancellationToken.None committed continuation; preserve history atomicity and exactly-once effects.
6. Validate all isolated safety tests, then freeze integrated source with SB01/SB02 and perform the full two-host UI/performance protocol.

## Dependency Impact

- Critical recovery foundation; any lost log/usage/approval/tool/terminal metadata invalidates combined proof.
- SB01 reopening invalidates this unit. SB02 reopening invalidates combined provider UI/performance, not automatically unrelated pure projection tests.

## Validation Depth

- Proof tier: Governed.
- Test project or non-test check: U/I SB03 rows plus U/C combined failure/activity rows; real Playwright MCP/host/performance checks.
- Filter: stable exact class prefixes in `plan/test-selection.md`.
- Selection reason: immediate/recovery distinction, projections, cancellation and current user-visible agent behavior.
- Expected discovered tests:20projection unit+70integration source cases; combined5orchestration+2cancellation+1running-Stop+9component cases, plus declared new tests. Reconcile runtime discovery, no zero/early-return proof.
- Invalidation keys: lock interval, journal/schema/order, projection metadata, JSON/history, source/test/dependency hashes and both host/fixture configs.
- Broad-gate decision: Executed once at Frozen Integration after scoped CodeAnalytics impact promoted all supplied Unit/Integration/Components suites. Retained failures and root startup-specific acceptance are recorded in reviews/01-execution-report.md; no broad rerun or all-green claim.
- Critical foundation: yes; combined actual conversations/tools/reopen on5032and5214 are mandatory downstream proof.

## Acceptance Checklist

- All nine existing-run failure boundaries roll forward exactly once; old complete/new complete state, never torn payloads.
- Cancellation before journal persists nothing; after journal completes the committed update. Errors remain explicit and logged.
- Empty/corrupt/conflicting journals and unexpected recovered records/payloads fail clearly and preserve diagnostic evidence.
- Second-store readers cannot observe intermediate writes; competing updates use latest detail with no lost logs/transcript.
- Progress-only update preserves usage totals but advances required activity/revision fields. Latest-session/active-run index semantics survive updates to old runs.
- New usage, approvals, tool receipts, artifacts/checkpoints and terminal/failure transitions still publish projections/history exactly once.
- Canonical full rebuild equals the published projection; up to five immediate raw comparison reads decrease while fresh validation reads and complete recovery remain. Canonical/compact/unknown-property matching-target behavior is covered for both the generic payload helper and separate chat owner.
- Every applicable progress entry remains awaited/durable/ordered and survives reload; no batching or disabled stage.
- UI01-UI06 pass on both instances; applicable approval proof passes; real tool-backed answers verified against source assets.
- Paired performance gate passes on both hosts; no proxy marker or unmeasured microbenchmark substitutes for actual startup.

## Proof Required

- `proof/SB03/manifest.md`, `proof/SB03/semantic-invariants.md`, source/test hashes and exact discovery/command transcripts.
- Failure-boundary/negative/cancellation/concurrency and projection-equality evidence; source assertion that trusted entry cannot be selected by recovery.
- Producer→consumer→lifecycle evidence for existing logs/run/session/usage/approval/tool records; prove actual producer execution, not only seeded row existence.
- `proof/SB03/ui/5032/`, `proof/SB03/ui/5214/`, `proof/SB03/performance/`; inspected screenshots and actual MCP actions/IDs.
- Independent verifier/review, host rollback/config preservation and anti-stub audit.

## UI Composition Contract

Use `plan/live-ui-validation.md`: existing transcript/composer is primary, details remain overlays, existing compact stats and floating list/editor layout, readable textarea/dialog,1920×1080 first viewport, transcript/log-body scroll owner. No UI design changes.

## Browser Validation Logging

For both exact origins record routes, actions/assertions, run/session/tool/approval IDs, actual tool facts, normal/progress/tool/error/reopen/approval screenshots and visual review of clipping/layering/scroll/actions. No browser-evaluated API substitute. Running Stop is not cancellation.

## C# Architecture Impact

Small local optimization inside existing Persistence owners; no runtime logging redesign.

## Boundary Ownership

Store owns lock/journal orchestration; slice/chat owners own projections and validation; JSON writer owns durable file boundary. New internal helper only if it creates a real directly testable boundary.

## Dependency Direction

Existing Persistence→Core/Models/Infrastructure abstractions; no outer module/UI reference or public trust switch.

## Pattern Decision

P03 explicit immediate/recovery trust. Preserve complete recovery; no global skip-validation flag or cache spanning locks.

## Testability Contract

Existing real filesystem fault/diagnostic hooks plus direct canonical projection tests. Trust validation is tested independently when extracted; full host is only composition/real-UI proof.

## Partial Class Policy

No new partial file to hide logic, no nested behavioral class. Existing partial store changes are minimal and cohesive.

## Architecture Proof Required

Actual csproj/dependency diff, immediate/recovery call-site audit, no new cycles/partials, source proof of removed redundant reads and direct helper tests. If extracted, old responsibility leaves original class.

## Progression Gate

- Isolated security/recovery/projection tests and architecture review pass before any live deployment.
- Bundle completion waits for SB01/SB02/SB03 manifests, both-host real UI and paired performance gates, independent verifier and raw-note closure.
- Missing platform/host/provider proof or unresolved performance regression keeps the gate open; no scope expansion into batching.

## Reopen Triggers

Recovered data uses trusted mode; metadata/log/usage mismatch; lost ordering/durability; unsupported fault fixture; tool/conversation regression or inconclusive benchmark. Reopen owner and dependent proof rather than recording accepted residual risk.

## Suggested Agent Prompt

After authorization and SB01, optimize only immediate committed-plan reuse. Preserve all per-stage durability and full recovery semantics, then prove real agents/tools/history on5032and5214 and a controlled speedup before final closure.
