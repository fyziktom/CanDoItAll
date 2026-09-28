# Collaboration implementation review

## Decision

**Retain the architecture; complete a bounded corrective pass before the next module.** One source-derived production data-loss path, two related sandbox-lifetime discrepancies and a browser completion race warrant correction. This is not a recommendation to restart the decoupling.

Repository: `fyziktom/CanDoItAll`; branch: `components-decoupling`; reviewed HEAD: `e55a780b36e75db39a05b7400408d8267d0f32d5`; parent: `714e42706904e796e5e628f30457ca40370c7d4c`; review date: 2026-09-28. References below are pinned to that HEAD. Reproduction traces are derived from code, not executed C# failures in this review environment.

## What is already correct

The [contracts project](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Modules/CanDoItAll.Modules.Collaboration.Contracts/CanDoItAll.Modules.Collaboration.Contracts.csproj) is an ordinary SDK library with no declared backend reference. The feature UI declares only the contracts project and real BaseLib/component requirements. The [sandbox project](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Sandboxes/CanDoItAll.Collaboration.UiSandbox/CanDoItAll.Collaboration.UiSandbox.csproj) references the feature UI and links the production CSS as content, rather than taking a Web project reference. These are inspected declarations, not an independently evaluated restore graph.

The [routed page](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationHomePage.razor.cs) delegates state/effects to a per-page session. [Registration](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Modules/CanDoItAll.Modules.Collaboration/CollaborationModuleServiceCollectionExtensions.cs) aliases the owner port to the actual scoped Collaboration service. There is no reason to substitute HTTP or move persistence into the renderer.

The [session](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Modules/CanDoItAll.Modules.Collaboration/Pages/CollaborationWorkspaceSession.cs) has current-read generations, independent draft/target objects, cancellation ownership, distinct initial/explicit/empty selection and unknown-outcome write protection. The [draft gate](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/UI/CanDoItAll.Collaboration.UI/CollaborationDraft.cs) performs form/model validation and rejects duplicate admission while locked. [Owner notification](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Support.cs) now isolates synchronous subscribers individually. These changes directly address important defects in the old page.

The [boundary tests](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/tests/Components/CanDoItAll.Collaboration.UI.Tests/CollaborationBoundaryTests.cs) explicitly fail on unresolved non-framework edges instead of silently accepting them. [Renderer tests](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/tests/Components/CanDoItAll.Collaboration.UI.Tests/CollaborationSurfaceTests.cs) use real BaseLib descendants and actual forms, and production/sandbox browser test implementations are present. Their existence is not a claim that this reviewer ran them.

## R1 — P2: automatic unread-filter reconciliation can discard a newer unsent reply

**Location:** `CollaborationWorkspaceSession.ReplyAsync` -> `ReadAsync` -> `RealignAsync` -> `ChangeTarget`; `CollaborationWorkspaceSurface.razor` reply form.

The source trace is:

```text
A selected; Inbox / Unread only
  -> reply #1 is admitted and the owner returns success
  -> ReplyAsync creates a new unlocked reply draft
  -> same-target reconciliation awaits the owner read; old A detail remains rendered
  -> the user enters reply #2, but does not submit it
  -> read returns A as read
  -> automatic realignment chooses B or clears selection
  -> ChangeTarget creates yet another reply draft
  -> unsent reply #2 and its context are no longer accessible
```

This follows from two inspected facts: the [reply fieldset](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/UI/CanDoItAll.Collaboration.UI/CollaborationWorkspaceSurface.razor) is disabled for `reply.IsLocked`, not the whole reconciliation period; and [local reply handling](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Commands.cs) intentionally sets unread state to false for `MarkAsUnread: false`. The latter is existing owner behavior to preserve, not the defect to change.

Blazor's logical single-thread model does not rule out this interleaving: a component is re-entrant when awaiting incomplete tasks. See [Microsoft's synchronization-context documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/synchronization-context?view=aspnetcore-10.0). This review did not measure how often a user encounters the timing window.

An existing dirty draft before a Mark-read/manual refresh provides a second important path to check. The policy must distinguish user-authorized target navigation from an automatic read deciding that the selected item no longer matches a filter. The current explicit target-switch policy can remain; background reconciliation must not silently destroy newly entered data.

**Required correction:** retain/recover the draft with its original target and context, or prevent the unsafe transition under a clearly surfaced bounded policy. Keep clean empty-filter behavior correct and prevent the old text from rebinding to B. Add deterministic state and real-form tests. A global busy lock, changed unread semantics or a new cross-module draft framework is not required.

The existing `Superseded_reply_reconciliation_cannot_relabel_a_newer_saved_reply` regression protects a related completion-status case but does not cover an unsent successor draft being discarded by filter-driven realignment.

## R2 — P2: the scenario host differs from the production command/view lifetimes

**Location:** [CollaborationScenarioWorkspace.cs](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/src/Sandboxes/CanDoItAll.Collaboration.UiSandbox/CollaborationScenarioWorkspace.cs).

### R2a: a reply disappears from the simulated store after switching targets

In `AdmittedSave`, `ReplyAsync` captures the body/kind and waits. After the wait it checks `Current(target)` and can return before modifying the `threads` list. Submit on A, select B, release the pending action, return to A: the scenario never records the admitted reply.

Production behaves differently: the owner has already been called for A; the session checks the retired target only to suppress effects on the newer UI. This is a sandbox behavioral defect, not a claim that the production database loses that same admitted write. It makes the preview/test environment an unreliable model for precisely the lifetime behavior this extraction was intended to prove.

**Required correction:** distinguish scenario stored results from current UI effects. A successful admitted write affects captured A exactly once; B and a later A editor remain untouched by the old completion. Add delayed A -> B and A -> B -> A tests using stored read-back, not merely a visible title.

### R2b: a late create can override a newer section/filter choice

The scenario's `selectionGeneration` increments in `SelectAsync`, but its `SetSectionAsync`/`SetUnreadOnlyAsync` do not retire the old create's navigation intent when the target stays visible. Production increments the generation for effective section/filter changes.

Reproduce: start a delayed create on Inbox with A selected; switch to Threads (A remains visible); release the create. The sandbox can select the created item and force Inbox again because its generation has not changed. The production session preserves the newer view intent.

**Required correction:** align effective user-intent lifetimes with production without importing the production module. Keep the write/identity, suppress obsolete view changes, and include same-target section/filter tests. Disposal and no-op transitions also need consistent treatment.

## R3 — P2: the production browser test can mistake admission for completion

**Location:** [CollaborationBrowserTests.cs](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/tests/Playwright/CanDoItAll.Tests.Playwright/CollaborationBrowserTests.cs), immediately after clicking `collaboration-mark-read`.

The test waits for `ToBeDisabledAsync()` and then immediately asserts unread state through the real owner. The renderer disables that control both for `target.IsMarkingRead` and when the thread is read. A slow successful write can therefore satisfy the UI assertion while the database still has its old unread state.

Playwright's retrying assertion waits for its stated element condition, not an application-level transaction. [The official assertion documentation](https://playwright.dev/dotnet/docs/test-assertions) does not make a disabled control a persistence receipt. This is a source-derived nondeterministic-test risk; no flaky run was reproduced in this review environment.

**Required correction:** wait for a target-specific accepted read/command outcome that was not already true before the action. Preserve real owner read-back. Do not replace this with `Task.Delay`, a forced click, only a generic pre-existing Ready marker or a fake owner in the production fixture. Demonstrate the busy-versus-complete distinction under controlled delay.

## C1 — close the original-matrix coverage and provenance gap

The [committed evidence record](https://github.com/fyziktom/CanDoItAll/blob/e55a780b36e75db39a05b7400408d8267d0f32d5/docs/architecture/collaboration-ui-boundary.md) explicitly says the original module-specific review notes, validation matrix and selection document were unavailable. This package restores the complete prior handoff unchanged.

The original matrix requires real production-form creation of both a notification and an escalation, plus a dirty-target journey. The reviewed production browser implementation creates the default notification and a System reply; it does not drive an escalation create form. Sandbox fixtures with a prebuilt escalation do not fulfill that production obligation. Add the missing bounded journey and map the remaining original obligations to actual evidence. Do not infer that every missing companion file caused an implementation defect.

The evidence record reports 59 passing cases across six focused lanes, plus graph/watch measurements. Those are the implementation agent's documented local results. Raw TRX/screenshots/measurement artifacts are described as local ignored files and were not available to this source review. The GitHub Actions query for the reviewed SHA returned zero workflow runs at inspection. This neither validates nor disproves the reported local runs.

## Scope and evidence limitations

No C# compiler/.NET test execution, PostgreSQL run, browser journey, screenshot inspection or watch measurement was performed in this review environment. The findings are supported by read source paths and explicit state transitions; the executor must obtain red/green C# evidence and rerun affected lanes. No repository files were changed by this review.

No production schema change, backend ownership rewrite, auth change or wholesale shared-bundle rewrite is recommended. [TestLab](NEXT_MODULE.md) remains a subsequent candidate, but the next task is to close these findings first.
