# Close the TestLab review findings

You are the senior C#/.NET and Blazor engineer implementing a small corrective slice on the user's actual CanDoItAll checkout. Complete the fixes, their real-control and production-parity proof, and maintained documentation. Do not stop at a plan. Use your judgment about the smallest implementation and test organization; there is no class, interface, file or commit quota.

## 1. Assignment and authority

Fix the reviewed TestLab issues **R1, R2 and the small C1 outcome-label correction**. Preserve the completed extraction and the S0 conversation-shell fix. **Do not start another module in this run.** The sandbox is the purpose of this development-loop extraction, not disposable test scaffolding, so its frozen editor and reference mismatch need closure before it becomes a template.

Read this complete package:

- [Review and concrete reproductions](REVIEW.md).
- [Validation and acceptance matrix](VALIDATION_MATRIX.md).
- [Proof status and limitations](PROOF_STATUS.md).
- [Pinned source register](SOURCES.md).
- [Original shared v3 foundation](original/shared/prompt.md), [original TestLab review](original/TESTLAB_REVIEW_NOTES.md) and [original TestLab matrix](original/VALIDATION_MATRIX.md).

`original/` is an unchanged historical assignment, not permission to rerun the extraction, redo S0, create duplicate projects or revive a completed migration. For this run the current assignment narrows the original scope. Current canonical repository instructions remain authoritative.

The reviewed source is `fyziktom/CanDoItAll`, branch `components-decoupling`, HEAD `3c579fd1a923ad90f619fe144e6e4c1fe081fa8b`. The preceding reviewed HEAD was `97989b9d13b9a6fa16280da98ec5b005a2f968c7`. These identify evidence only. **Do not checkout, reset, rebase or otherwise pin execution to either SHA.** Record actual start branch/HEAD, worktree changes, SDK, dependency mode, sibling revisions and relevant drift. Reconfirm the findings against current code. An equivalent fix already present should be proved and retained, not duplicated.

Before editing, read current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/ui-component-seams.md`, `docs/architecture/testlab-ui-boundary.md`, `docs/testing.md` and `.github/workflows/ci.yml`. Use the installed shared standards skill when the repository requires it. Use the actual Code Analytics MCP for references/affected tests when available, and the Components MCP when changing shared non-WebGL component behavior. If unavailable, record this and corroborate impact with local source, evaluated metadata and discovery. Do not claim an unavailable tool was used.

All code, comments, tests, UI strings, permanent docs and commit messages must be English. The final owner-facing report may be Czech.

## 2. Keep the architecture and production safeguards

Retain the existing projects:

```text
src/Modules/CanDoItAll.Modules.TestLab.Contracts
src/UI/CanDoItAll.TestLab.UI
src/Sandboxes/CanDoItAll.TestLab.UiSandbox
```

Retain the module's `Workspace/TestLabWorkspaceSession` and `TestLabWorkspaceOwner`, the per-page session in `Pages/TestLabPage.razor`, and all current owner persistence. Do not inject TestLab/Projects/CRM implementations, Web composition, DbContext, IServiceProvider or production owner ports into the sandbox. Keep CSS as content and keep the real BaseLib renderer. This is not an HTTP/API-only migration.

In particular preserve:

- Exact owner-issued project admission, global null/null semantics, historical/recreated-project behavior and explicit rebind; never recapture admission during a refresh.
- Deep submission ownership, single write per active submission, original child-object/durable-ID reconciliation, original/successor separation and newer unblurred input.
- Known refusal, known commit, committed-with-warning and genuinely unknown outcome as different states. A lost read-back is not an unknown write. A retry of reconciliation is a read, never another save.
- A newer editor or newer submission cannot be unlocked, relabeled, reset, reselected or notified as saved by a retired one.
- All four workspace sections, filters, raw timestamp/validation behavior, current route semantics, assets and interactive-readiness behavior.
- Existing S0 initialization observation, lifetime checks, subscription detachment and delayed safe resource release. Do not expand or rewrite Conversations, Collaboration or Agents to repair this sandbox.

## 3. R1 — reproduce and settle the sandbox's interrupted read-back

Inspect current `TestLabScenarioWorkspace.SaveAsync`, `ChangePartyAsync`, `RetireReads`, `WaitAsync` and `RetryAsync`, plus `TestLabDraft.Finish` / `CanSave` and the responsible-party control in `TestLabWorkspaceSurface`.

The reviewed sequence is deterministic:

```text
DelayedReadback scenario -> Save the active plan
-> fake store commits; identities and LastCommitted are retained
-> Save waits on a non-write controlled read-back
-> change Responsible party to None in the still-editable same draft
-> ChangePartyAsync retires all non-write waits
-> Save returns before Finish
-> the wait is gone but Pending still owns the draft
-> Save stays disabled and Retry cannot reconcile while Pending is non-null
```

First reproduce it with controlled completion using the actual current classes. The [regression seeds](regression-seeds/README.md) are uncompiled starting points, not proof and not a required final test shape. At least one regression must exercise the real form and selector, not just model mutation or a helper reimplementation.

Make the smallest ownership-correct repair. It is acceptable to give reference reads their own retirement scope, or to settle the exact active known-committed submission when its read-back is superseded. Choose the design that best fits current code and preserves existing scenarios. Do not hide the bug by removing the delay scenario, making reads always immediate, disabling all editing while saving, routing through the production session, changing the fake write into a rollback, or blindly clearing any Pending flag in a finally block.

Required invariants:

1. The originally admitted fake write commits exactly once even if selection later changes.
2. A same-draft responsible-party change does not strand save admission. The exact save eventually settles as Saved or SavedWithWarning; if a read failed/superseded, the message is truthful and retry reads only.
3. The user's newer party choice, other newer text, active section, draft and EditContext survive. The committed submission may contain the older party; reconciliation must not overwrite the newer field.
4. Parent and eligible child IDs retained at commit remain stable. An explicit later edit/save is allowed; read-only retry is not that edit/save.
5. Supersession, reset, project A → B → A, selection A → B → A, scenario switch and disposal cannot finish or release a different submission. If a correct current operation is still pending, its lock must remain.
6. Controlled waits are either completed or retired; the scenario does not accumulate orphaned waits/callbacks. The submit handler's task must finish after its applicable completion signal. Bounded WaitAsync is a test timeout, not race orchestration.

If you simplify shared scenario wait handling, inspect **all** calls to `RetireReads` and `WaitAsync`; do not fix the party path by breaking DelayedRead, DelayedReferences, DelayedSave, delayed read-back, known warnings or unknown locks.

## 4. R2 — resolve saved parties independently of project-scoped options

Inspect `SetReferences`, the fake store/reference data and production `TestLabWorkspaceSession.LoadPartiesAsync`. The production algorithm already resolves a saved `ResponsiblePartyId` via `owner.PartyAsync` when it is absent from the project list, including when there is no project.

The representative sandbox currently has a valid known saved party. Explicitly changing the project to None retains that ID but empties all party choices merely because the project is null. The UI then displays Unavailable responsible party for a reference that this scenario still knows.

Add the smallest real in-memory reference lookup/state needed for parity. Reference existence is separate from membership in the current project's choices. Do not import the CRM backend or move a whole directory/bridge into the UI library. The sandbox should answer from its own bounded fake catalog, not fabricate a successful response for arbitrary identifiers.

Required behavior:

- A known saved party remains named/selectable when the plan is explicitly global or is absent from the project-specific option list; the same stored ID is preserved.
- A genuinely missing or deliberately unavailable reference remains unavailable, preserving its ID and visible placeholder. The existing MissingReferences scenario must remain meaningful.
- A failed reference lookup is represented as a failure, not successful empty data. Do not silently erase IDs or change current/historical project admission.
- Delayed project-list/saved-party results are scoped to their actual draft and request. A → B → A and a newer party choice cannot be overwritten or relabeled by an older lookup.
- The fix cannot reintroduce R1 by treating reference resolution as ownership of a save read-back.

Add a production-session positive global saved-party-fallback test if it is not already present. Use a controlled fake owner in the existing production session test project. That test documents the parity being maintained; it is not a reason to change the already-correct production lookup algorithm.

## 5. C1 — render an unknown write outcome as unknown

Inspect the current `TestLabPage.Notify` switch. At the reviewed HEAD, Unknown is sent through `Notifications.Error("Test plan save failed", ...)` even though the detailed message correctly says the outcome is unknown.

Give Unknown an uncertainty-specific title, for example `Test plan save outcome unknown`. Preserve the existing detailed recovery instructions, origin ownership and replay lock. Use the current notification severity conventions; a severity color is not a new owner result. Refused, Saved and SavedWithWarning must keep their correct meaning.

Add a focused test through the real page/session seam with a controlled Unknown owner result, observing the actual notification and the draft state. Verify no automatic second write and no success/failure certainty manufactured by refresh or a project change. Do not simulate a database failure and claim it proves a definite non-commit. Reuse existing test fixtures or a narrow test owner; do not add a production fault endpoint or query parameter.

## 6. Prove actual controls and integration, proportionate to impact

Follow [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md) and current repository testing rules. Start with failing-first R1/R2 regressions, then fix and run the bounded owning tests. Do not rerun the entire Stable suite after each edit.

The primary existing light lane is:

```text
tests/Components/CanDoItAll.TestLab.UI.Tests/CanDoItAll.TestLab.UI.Tests.csproj
FullyQualifiedName~CanDoItAll.Tests.Components.TestLab
```

Its documented baseline was 20 cases, not a mandated new total. Current source and all added theory rows determine the new expected count. Discover it before execution. A zero-case run is not proof. Confirm all new files are in the owning project and selected by the current CI shard.

Also use the current production-session lane and the real route/notification tests affected by C1. Existing session tests prove that production party lookup and committed-refresh lanes are independent; do not replace them with sandbox tests. Required lower-layer cases include the real selector during delayed read-back, same draft/context retention, no second fake commit on retry, subsequent explicit save, positive/negative global fallback, and retired/successor operation isolation.

Extend the **standalone TestLab Playwright journey** to exercise the same actual selector transition while a committed read-back is held, then observe an operation-specific terminal result and enabled submit. A pre-existing Saved label or a temporarily disabled button is not an acknowledgement of this operation. Use a fixture gate/receipt, current operation identity or an equivalent demonstrably fresh observation; do not add sleeps. Prove exactly one fake-store commit and the retained field. Exercise the global reference label and the intentional missing-reference scenario through real controls.

Run the TestLab production browser journeys against the owned real host and isolated PostgreSQL 18 as closure, including existing aggregate identity and postcommit warning/read-only recovery. The fixture's real shell stays present. Capture and inspect desktop screenshots, page/console/asset failures and server logs. Tests that deliberately isolate a fake owner prove only their stated layer.

Build changed production projects directly (normally the sandbox and, for C1, the TestLab module/route), and the actual UI/host/test consumers affected by your edits. Rebuild test assemblies before --no-build execution. If draft/submission contracts or production session semantics change, expand to the affected host reconciliation, owner/admission and cross-module tests from the original matrix. If persistence or shared composition changes, stop and justify why this narrow fix requires it, then apply all named current invalidation gates. Do not silently broaden into architecture work.

S0 needs no new implementation. If unchanged, record source equivalence and historical proof separately; if touched, run all its deterministic lifecycle cases, affected consumers and the production Collaboration journey again. A sandbox fix does not authorize skipping a required shared-shell gate after changing the shell.

## 7. Assets, development loop, static gates and environment safety

Keep the sandbox dependency direction unchanged and re-run the transitive boundary tests. Inspect evaluated metadata if references or build settings change. Rebuild/start the real backend-free sandbox with its existing Parity CSS, icons and scripts. Do not remove watch inputs or introduce a Web project reference to make assets easier.

A current quick smoke of Razor editing through the sandbox is appropriate. Do not fabricate another pre-extraction benchmark: the recorded Web/sandbox measurements are historical local observations. Repeat detailed graph/watch/latency measurements only when this fix changes those dimensions or a focused check exposes a regression. Report samples and limits; do not promise the whole Web host became faster.

Use only owned processes, loopback ports and explicitly isolated PostgreSQL 18 through current test helpers. Record sanitized provenance; do not print credentials or reuse the user's ordinary database/app on port 5032. Respect output locks, unrelated worktrees, sibling changes, containers, watch and MCP sessions. Match the current supported test configuration and child-host configuration; leave external Playwright base URLs unset for an owned fixture. Clean up only resources whose ownership is verified.

Run current mandatory portability-static and relevant documentation/evidence checks plus `git diff --check`. Review any new or stale findings, repair real portability defects, inspect any justified baseline delta and require final enforcement without --write-baseline. Passing focused tests does not waive this gate.

Maintain `docs/architecture/testlab-ui-boundary.md`, the sandbox/UI READMEs and testing notes only where behavior or proof changes. Add a dated corrective receipt; preserve previous measurements as historical. Do not copy this package into a competing global rulebook or make the product build depend on it.

## 8. Deliverables and stop

Deliver R1/R2/C1 fixes; failing-first and passing regression evidence; actual-control and browser proof; an impact-based test receipt; unchanged light boundary; updated maintained docs; and resource cleanup.

Report: actual start/final branch and HEAD, touched files, relevant sibling revisions and SDK; each issue's reproduction and resolution; exact build/discovery/test commands with expected/discovered/pass/fail/skip counts; proof paths; current versus historical/unrun evidence; static gate status; asset/graph checks; and signed-commit status. Do not say that the reviewer-provided C# seeds were already compiled or passed: they were not.

Local signed commits are authorized under existing repository policy. Preserve the ordinary unlocked GPG agent/session without weakening its timeout/security policy, storing passphrases or disabling signing. A signing block is separate from code/test correctness. Do not commit unrelated changes. **No push, merge, rebase, pull request, tag, publication, release or next-module implementation is authorized.**

When these bounded TestLab corrections and their applicable proof are complete, stop. Do not begin SchedulerPlanner, Plugins, Resources, Processes or Workbench from the historical module list.
