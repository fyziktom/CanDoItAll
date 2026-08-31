# SB01 — Operation-local Filesystem Facts

## Status

- Status: Completed
- Focused gate: Pass. Execution authorization and Phase 0 are satisfied. Integrated real UI, native approvals, performance and final host checkpoint passed; see proof/SB01/manifest.md and proof/SB03/ui/validation-summary.md.

## Objective

Reduce repeated case-sensitivity probe I/O inside verified filesystem-operation intervals while keeping the current fresh security and durability contract.

## Covered Inputs

- N001/N004; R01/R04/R08/R10. Recommendation4 explicitly excluded.

## Prerequisites

- Execution authorization, source/host re-anchor and Phase0 paired baseline were satisfied before implementation.
- Characterize actual root/path freshness and existing negative cases; record test-platform capability.
- Read architecture/checkpoint/test/performance plans.

## Exact Source References

- `repo://src/Foundation/CanDoItAll.Infrastructure/FileSystem/PhysicalFileSystemPathPolicy.cs`

Symbols/ownership: factory.Create, constructors, DetectCaseSensitivity, EnsureSafePath/RevalidateMutationTarget.

- `repo://src/Foundation/CanDoItAll.Infrastructure/FileSystem/DurableFileWriter.cs`

Symbols/ownership: WriteTextAsync/WriteBytesAsync/WriteStreamAsync and directory traversals, flush and commit boundaries.

- `repo://src/Foundation/CanDoItAll.Infrastructure.Abstractions/FileSystem/PhysicalFileSystemPathContracts.cs`

Symbols/ownership: public contracts unchanged.

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs`

Symbols/ownership: downstream consumer; any read-path reuse must obey the same freshness gate.

- `bundle://plan/test-selection.md`

Owning tests, selectors and crash-host discovery are defined there.


## Deliverables

- Small local reduction of duplicate fact probing; fresh containment/native-root/reparse/mutation checks retained.
- Meaningful regression/performance tests and governed evidence.
- Source inventory of global factory consumers unchanged; no semantic global cache.

## Implementation Steps

1. Capture probe/flush counts and case/root mutation characterization before editing.
2. Identify intervals in which case facts can safely be reused. A path-string or root-identity check alone does not prove unchanged case semantics.
3. Reuse a known typed fact only within a verified interval. Reprobe Unknown after root creation; reprobe whenever root recreation/mount/case setting or an external callback/await makes freshness unproven.
4. Keep security validation at its original boundary; preserve real payload WriteThrough/Flush(true), atomic rename, permissions, cancellation/timeouts and cleanup exceptions.
5. Directly test helper/writer, then real workspace commit; review before unlocking SB03.

## Dependency Impact

- Critical foundation for SB03 and combined UI/performance proof.
- SB02 provider-source edits can be independent; no shared artifacts/live fixture races.

## Validation Depth

- Proof tier: Governed.
- Test project or non-test check: U/I and platform-specific real filesystem tests in `plan/test-selection.md`.
- Filter: SB01 rows (19unit+31integration source cases) plus declared new cases.
- Selection reason: security/freshness and actual atomic write consumers.
- Expected discovered tests: reconcile named source inventory and added cases before execution; actual nonzero discovery required.
- Invalidation keys: factory/writer/contracts, path/case/root freshness, permissions/flush/lock behavior, test platform and dependency hashes.
- Broad-gate decision: Executed once at Frozen Integration after scoped CodeAnalytics impact promoted all supplied Unit/Integration/Components suites. Retained failures and root startup-specific acceptance are recorded in reviews/01-execution-report.md; no broad rerun or all-green claim.
- Critical foundation: yes; real existing-run workspace update/recovery smoke required.

## Acceptance Checklist

- Probes decrease in eligible unchanged-root intervals and do not grow merely with directory depth there; do not assert an unsafe global one-probe limit across freshness boundaries.
- Payload flush/atomicity contract is unchanged.
- Missing root/Unknown→known, case-distinct sibling escape, root/ancestor symlink swaps, same-path root/case-setting change are exercised affirmatively.
- No outside-root writes; concurrent writers/cancellation/crash retain complete old or committed new content, not torn content.
- Required platform permission/link cases did not silently return.
- No new public interface/partial/global cache or startup callback changes.

## Proof Required

- `proof/SB01/manifest.md` with source/test before/after hashes, commands/discovery/results and `proof/SB01/semantic-invariants.md`.
- Deterministic probe-count regression before/after; positive real writes and adversarial boundary/crash cases; source assertions and anti-stub audit.
- Direct helper test plus dependent workspace commit/recovery transcript; architecture gate and exact downstream unlock.

## UI Composition Contract

No UI edits. Existing desktop proof is centrally specified in `plan/live-ui-validation.md`; SB01 closes its implementation foundation on real filesystem/downstream tests, while overall bundle closure still requires both-host UI proof.

## Browser Validation Logging

No browser-only substitute for path security. Integrated SB03 matrix cites SB01 hashes/build; changed SB01 binaries invalidate that proof.

## C# Architecture Impact

Local Infrastructure optimization; no new layer or responsibility family.

## Boundary Ownership

Filesystem facts stay in Infrastructure; runtime and JSON application consumers gain no filesystem implementation knowledge.

## Dependency Direction

Existing Infrastructure→Abstractions/SharedKernel; no AgentFramework/Module/UI dependency.

## Pattern Decision

P01 explicit operation-local facts; no singleton cache/AsyncLocal.

## Testability Contract

Existing factory/observer and scratch-file seams; any extracted helper is top-level internal and directly tested without application runtime.

## Partial Class Policy

No new partial/nested behavior container; existing owners remain cohesive.

## Architecture Proof Required

Before/after dependency/source diff, no global consumer semantic change, direct tests and removed redundant work. If extracted, prove old behavior removed; no artificial facade requirement for a local change.

## Progression Gate

- Pass all freshness/security/durability/platform and downstream store tests plus independent architecture review before SB03.
- If freshness cannot be proved, keep probes at that boundary; if no useful safe reduction remains, reopen rather than weaken security.

## Reopen Triggers

Root/case assumptions fail; public factory behavior changes; hidden skip/platform gap; downstream lost/torn records. Invalidate SB03 and integrated proof.

## Suggested Agent Prompt

After execution authorization, implement only SB01's bounded reduction. Preserve fresh security and durable writes; capture real adversarial evidence and stop before expanding contracts or reducing per-stage persistence.
