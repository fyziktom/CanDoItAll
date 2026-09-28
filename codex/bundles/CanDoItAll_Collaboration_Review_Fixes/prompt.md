# Codex GPT-6 Astra Max — close the Collaboration UI review findings

Finish the existing Collaboration UI decoupling in the main CanDoItAll repository. **Do not redo the extraction and do not start TestLab or another module.** The architecture is broadly correct; this task is a bounded production-state, scenario-fidelity and validation follow-up.

## 1. Read the complete context and preserve the current architecture

Read [REVIEW.md](REVIEW.md), [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md), and the complete original [review notes](reference/original-assignment/REVIEW_NOTES.md) and [acceptance matrix](reference/original-assignment/VALIDATION_MATRIX.md). Also read the bundled unchanged [shared v3 entry point](reference/original-assignment/shared/prompt.md), its architecture documents and validation guidance. The original [assignment](reference/original-assignment/prompt.md) is historical context, not an instruction to create the same projects again. These inputs are physically included; resolve them from the extracted package rather than assuming a particular Downloads path.

At the actual checkout read `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`, `.github/workflows/ci.yml`, `docs/architecture/ui-component-seams.md`, the current Collaboration boundary record and applicable CanDoItAll.SharedInfo standards. Current repository rules are authoritative; this task specializes the original assignment's acceptance obligations without creating another general architecture standard.

Preparation reviewed `components-decoupling` at `e55a780b36e75db39a05b7400408d8267d0f32d5` (parent `714e42706904e796e5e628f30457ca40370c7d4c`). **These SHAs are provenance only.** Work on the user's actual branch/HEAD. Do not check out the review SHA, reset, clean, stash, rebase, switch branches or overwrite unrelated changes. Record the actual HEAD, pre-existing changes, SDK, relevant sibling revisions, source/package mode and asset mode. If a finding is already fixed by subsequent work, prove that with current source and a regression instead of reapplying an obsolete change.

Keep the accepted dependency direction:

```text
Production routed host + per-page session -> existing Collaboration owner
                         |
                         v
             Collaboration.UI -> Collaboration.Contracts + real BaseLib
                         ^
                         |
          backend-free Collaboration.UiSandbox
```

Retain the current owner-port registration as an alias of the actual scoped `CollaborationService`. Keep EF/persistence, mappings, activity mirroring and transfer ownership in the module. No API-only rewrite, new database schema, per-user unread model, optimistic concurrency protocol, distributed idempotency layer, general event bus, generic draft framework or backend reference from the sandbox is needed.

Use Code Analytics MCP for symbol/consumer/affected-test discovery when available and follow the repository's required Components MCP workflow for component changes. Corroborate with source and actual test discovery. Tool unavailability is a limitation to record, not a successful tool result and not permission to invent replacement components.

## 2. R1 — prevent automatic reconciliation from destroying a newer reply draft

Inspect `CollaborationWorkspaceSession.ReplyAsync`, `ReadAsync`, `RealignAsync` and `ChangeTarget`, plus the real reply form in `CollaborationWorkspaceSurface.razor`.

At the reviewed revision, a successful reply immediately replaces `Reply` with a fresh unlocked draft, then awaits `ReadAsync`. The old selected detail remains rendered during this same-target refresh. The real form disables fields only for `reply.IsLocked`. A user can therefore type the next reply while reconciliation is pending. Local replies correctly use `MarkAsUnread: false`; a fresh snapshot can remove A from an active Unread-only list. Automatic realignment then selects B or clears the selection, and `ChangeTarget` replaces `Reply` again. The newly entered, unsent text disappears.

Reproduce the empty-list variant first with the existing scripted `Owner` and controlled `TaskCompletionSource` helpers:

1. Load A, enable Unread only and make the first reply valid.
2. Let the owner append succeed, but hold its following workspace read incomplete.
3. Obtain the newly exposed reply draft, enter a distinct second message through the real input/field-change path, and retain its identity and validation context.
4. Complete the read with A now read and no visible unread candidate.
5. Demonstrate that the reviewed implementation silently drops that new draft.

Also cover a remaining unread B. Use a separate scenario for an already dirty reply while Mark-read or a manual/background refresh makes A disappear from the filter. An explicit user target change is not the same as automatic refresh-driven realignment.

Implement the smallest coherent policy that prevents this loss. You may keep a dirty active editor attached to its original target until explicit navigation/discard, or use another bounded policy that visibly preserves/recoverably retains its draft and context. If admission/reconciliation locking is part of the solution, guard it in the host and prove the real controls; disabling only the short write phase is insufficient. Preventing typing after a reply alone also does not address text already present before Mark-read or a later refresh.

Required behavior:

- An automatic refresh or a completed older operation must not silently discard text typed into a newer editor.
- Text bound to A must never silently become a message to B. Keep target/lifetime identity explicit.
- Preserve the documented intended explicit navigation/reset behavior; do not accidentally make every dirty form impossible to leave.
- Clean Unread-only empty results still have a genuinely empty selection; the backend's null fallback must not select an unrelated read thread.
- Same-target reads and view changes retain the intended `EditContext` and validation lifetime.
- Saved/refused/unknown outcomes remain distinct; no create/reply replay is introduced.
- Do not change the owner's local-reply unread semantics to hide this defect.

Choose and document the small transition policy rather than copying CRM/HR's entire mutation framework. Add a deterministic host/session regression and proof through the actual form. Test exact target, retained text/context, owner-call count and navigation effects, not just a generic success label.

## 3. R2 — make the scenario host model admitted writes and newer user intent faithfully

Inspect `CollaborationScenarioWorkspace`, particularly `ReplyAsync`, `CreateAsync`, `SelectAsync`, `SetSectionAsync`, `SetUnreadOnlyAsync`, `AlignAsync` and delayed-operation completion.

### R2a: a retired view must not cancel an admitted simulated write

In the reviewed `AdmittedSave` scenario, a reply waits, then returns on `!Current(target)` before updating the in-memory thread data. Switching A -> B while the reply is pending therefore deletes the simulated effect of an already admitted operation. Production dispatches the real owner operation for A and fences only the later UI effects when A is no longer current.

Reproduce: submit a unique reply to A, select B, release the admitted reply, then reopen A. The simulated store must contain the reply exactly once, while B's draft/selection remains untouched. Repeat A -> B -> A with a newer A draft and an independent pending successor submission. Do not let an older completion unlock, overwrite or notify for the successor.

Separate the scenario's stored result from its current view projection. Apply an admitted successful write to its captured target independently of whether that view is still current; use current-lifetime checks for editor reset, navigation and visible completion. Keep explicit refusal and unknown outcomes meaningful. Retiring the entire scenario must not render/mutate its replacement, and pending operations must terminate under the scenario's documented disposal policy without leaked waits. No production database or implementation assembly may be imported to achieve parity.

### R2b: tab/filter intent must retire old create-navigation effects

The sandbox increments `selectionGeneration` on target selection but not when a section/filter changes while the same target remains visible. In production those effective view changes increment the relevant generation. An admitted create started on Inbox can consequently finish in the sandbox after the user moves to Threads and force the view back to Inbox.

Reproduce a delayed create with A selected, switch to Threads while A stays visible, then release the write. Preserve the newer Threads selection/section while retaining the created identity and stored thread. Cover an effective filter change as well. Use no-op-aware transitions consistent with the production semantics; do not create another ad hoc navigation authority.

Keep the sandbox a small deterministic implementation of the UI contract, not a cloned production backend. Reuse a minimal shared transition policy only if it improves the actual dependency boundary; do not add a framework or move owner services to the UI. Its delayed scenarios must test the semantics of R1 as well as R2, not merely show a loading label.

## 4. R3 — wait for a completed mark-read outcome, not the busy control

In `CollaborationBrowserTests.Production_create_reply_mark_read_filter_deep_link_and_context_use_real_persistence`, the test clicks Mark-read, waits for the button to be disabled and immediately reads the owner database. That button is disabled both while `target.IsMarkingRead` is true and after a selected thread has become read. The assertion can therefore pass before persistence finishes; it is not a completion barrier.

Replace this with an operation/target-specific accepted-state observation. For example, wait for the selected identity's actual read state and successful reconciliation, or another existing semantic signal that cannot also be true before the write is complete. A generic `data-phase=ready` alone can already be true before the handler starts; a generic disabled button is the present bug. Keep the real owner read-back after a meaningful completion signal.

Do not use arbitrary sleeps, force-clicks, swallowed exceptions, fake production DI or test-only production endpoints/query switches. Prove the distinction under a deterministically delayed owner write in an appropriate test seam, and run the real production browser journey. Preserve browser/console/asset-error checks and owned-resource cleanup.

Reconcile the original matrix now included in this package. In particular, the reviewed production browser journey creates a default notification and a System reply, but not an escalation through the actual production create form. Add the missing positive escalation creation/selection/deep-link proof and the relevant dirty-target journey. Scenario fixtures containing a prebuilt escalation are not proof of the production form path. Keep assertions scoped to task-owned records where the shared fixture can contain other records.

## 5. Validation: reproduce narrowly, then close the actual affected layers

Follow [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md). Read the existing test implementations; do not replace them with mocks that hide the renderer or owner behavior under review.

Start with deterministic red regressions for R1 and R2, and a delayed-completion demonstration for R3. Use controlled completions rather than timing-dependent sleeps. Run discovery on current assemblies, check expected/discovered counts, then execute the same filters. A test file's presence, a reported historical pass or zero-case success is not proof.

Retain existing session, real-renderer/boundary, host/shell, owner/schema and browser regressions. Run focused projects first. Expand for actual affected references and the repository's named broad-suite triggers, not after every edit by habit. Do not automatically run all Processes, providers, LiveProcess or the entire Stable suite for this bounded correction. Do not omit required static gates or affected owner integration merely because sandbox tests pass.

Use an explicitly owned isolated PostgreSQL 18 setup through the current documented test configuration. Leave the user's ordinary application/database on port 5032 and unrelated watch/MCP processes alone. Use fresh task-owned ports and processes. If Release output is locked, use an appropriate task-local proof configuration/output approach consistent with repository rules; do not terminate the developer app or weaken build settings.

Rerun the light boundary guard and relevant builds. Preserve source-mode sibling resolution and Parity assets. If project/assets/watch membership changes, obtain a fresh evaluated graph and watch inventory. Do not inflate the sandbox graph to fix scenario tests. Historical watch counts and timings can remain attributed historical evidence; they are not new measurements. A wholesale old-HEAD checkout or a repeated benchmarking project is not required for a state-machine repair.

Run current portability-static over the full protected tree, review added/stale findings, refresh only justified baseline deltas, then finish enforcement without `--write-baseline`. Run maintained documentation and whitespace checks. Check command exit statuses. Keep new tests in appropriate test solutions/CI shards; do not pull test projects into the product solution or silently leave a new test project unexecuted in CI.

## 6. Documentation and evidence

Update `docs/architecture/collaboration-ui-boundary.md` and relevant sandbox/test guidance to describe the actual safe automatic-selection policy and matching scenario semantics. Reconcile every previously unavailable original review/matrix obligation with implementation or evidence, using `passed`, `failed`, `blocked`, `not run` and `not applicable` accurately. Do not fabricate the unavailable original TRX files or infer a rerun from their historical summary.

Preserve the historical proof context. The old document describes an uncommitted implementation based on `714e...`; the user has since committed and pushed it as `e55...`. Append/correct current closure metadata without rewriting past command runs as if you executed them. Report actual current final HEAD and signing status for your own work.

The review found no GitHub Actions run for the reviewed SHA at inspection time. This is an evidence limitation, not a claim that local tests failed and not a request to trigger an expensive CI workflow unconditionally. Current local proof and truthful coverage are required.

## 7. Scope, Git and final receipt

Local, task-scoped signed commits are permitted. Keep the normal GPG agent/session available and verify signatures. Never disable signing, change its security policy, store a passphrase or commit unrelated work. A locked signer blocks a commit, not independent validation. Do not push, merge, create a PR, publish packages or release unless separately authorized.

All source, comments, identifiers, tests, UI text, repository documentation and commit messages stay English. The owner-facing final report may be Czech. This handoff is external planning material; do not copy the full archive or its historical audits into the repository.

Finish with a concise receipt mapping R1, R2a, R2b, R3 and original-matrix reconciliation to changed code, red/green evidence and exact commands/counts. Report actual limitations and unresolved issues, actual graph effects, owned-resource cleanup and commit/signature state. Do not call the module closed while an unsent draft can still disappear through automatic reconciliation or the sandbox models admitted operations incorrectly. Do not stop at a plan. **Do not implement TestLab in this run.**
