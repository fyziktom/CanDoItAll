# Source-derived Projects risks to resolve within P1

These are source review findings in the pre-extraction Projects page, not failures introduced by Workspace R2 and not runtime reproductions performed by this reviewer. Codex must first add deterministic failing/controlling tests and preserve already-correct behavior. [R10–R23]

## PR-01 — publication generations do not admit mutations

`ProjectsPageLoadGeneration` prevents an old result from publishing, but it does not stop two Save/Save-and-open invocations from reaching their owner. Current footer controls and handlers do not provide one shared mutation admission. A duplicate create can therefore not be ruled out by a generation counter.

Give each actual draft/target one explicit admission and keep save, alternate save, delete and seed settlement distinct. Capture a deep immutable request before the first await. Treat repeated Enter/click and conflicting actions in handlers, not just through a disabled button. Do not hold an unrelated successor draft hostage to an abandoned read, and do not permit a second mutation of the same target while its first outcome is genuinely unknown.

## PR-02 — acknowledged save followed by secondary failure

The page calls `ProjectsService.SaveAsync`, then seeds objects, then loads portfolio and exact editor; only after those steps does it replace the editor with the loaded result. A seed or read-back failure can therefore leave a new editor without the known saved ID. A late successful read-back replaces the entire live draft, losing later typing and child-row edits. [R10, R12]

Retain at least the actual acknowledged project ID immediately. Preserve the original lifetime/admission and each created phase/option ID when authoritative evidence is available; never adopt the lifetime of a separately recreated project from a later read. Do not pair rows by mutable display name or by a live index that has shifted. Compare accepted values with the submitted snapshot and stable local row identities. Keep newer fields and the same EditContext/raw invalid inputs; merge only unedited fields and exact returned identities.

The current owner returns an ID; it generates child IDs server-side, preserves matched existing IDs, captures creation admission and commits before search/activity follow-up. A small backward-compatible editor acknowledgement projection from the existing committed owner is permitted when required for safe reconciliation. Reuse the existing save implementation; do not introduce another writer, change schema or reconstruct a commit receipt after an uncertain commit. Legacy HTTP/agent callers retain their contracts. [R15]

## PR-03 — starter drafts belong to the editor that created them

The page-level `starterObjects` list is cleared on New and successful save, but not when cancelling New or opening a different existing project. Test: create an unsaved draft with a planned object, close it, open B, save B. A plan from the abandoned draft must not be seeded into B. Bind the plan list to the editor instance rather than a long-lived page collection. Also test old save completion after a new draft has added its own plan.

The current Workbench seeding implementation creates the submitted batch in one separate native transaction. Do not describe that batch as per-item commits. The overall project+seed sequence is two owner effects and not one atomic transaction. [R16]

Use a submitted seed snapshot. Once its batch is acknowledged, remove/mark only the submitted original plan items; never clear newer items typed into the same or next editor. If the seed result is unacknowledged, keep the project commit fact, forbid blind seed replay and provide observation/review guidance. Do not pretend a view cancellation rolled back the batch. No UI refresh may seed anything.

The existing shared seed port only takes a project ID. For the new editor's exact-lifetime flow, inspect the final native transaction. If it cannot retain the original accepted ProjectWriteAdmission, add a minimal compatible Projects-owned admitted seed port/overload implemented by Workbench and pass that value into its existing mutation scope. Reuse the seed algorithm and schema. Do not make SharedKernel reference Projects, move Workbench into the renderer, add a durable seed replay engine, or turn a fresh read of a recreated ID into the old admission. Add a delayed deletion/recreation negative control at the real owner boundary.

## PR-04 — independent reads and exact acquisition

Preserve existing route/preview/agent-completion generation tests. The current page couples list, hierarchy, editor and package reference reads; a package-target failure must not falsely announce the editor missing or wipe an acquired draft. Track core editor availability separately from auxiliary failures. A missing exact ID must never become a new draft eligible for Save. Same acquired target, rerender or wizard step must preserve the editor; a failed acquisition of the same ID must remain retryable.

An agent completion refresh may update the portfolio but must not reload over user edits or reopen a dismissed modal. Navigation and automatic selection belong to captured route/user intent, including A→B→A and close/reopen. Captured target errors may remain in that operation's safe result, not in the successor's form.

## PR-05 — form and child identity

The actual editor uses model-based EditForm, with Save-and-open wired separately from submit. Both paths must validate the same stable context. Capture text as it is typed rather than losing the last unblurred edit. Preserve invalid raw numeric/date state and validation when switching among all five steps or inspecting details. Bind repeatable phase/option/plan rows by stable identity, including replacement/removal while a write is pending.

Respect current service semantics: nonempty name, trimmed accepted strings, ordinary UTC date normalization, default option categories and replacement of removed saved phases/options. Do not add new scheduling rules or silently turn optional inputs into mandatory fields. Invalid parsing is still invalid.

## PR-06 — deletion and package observations

Keep the current exact deletion cleanup identity and typed partial-completion result. Store acknowledged deletion/recovery facts before attempting portfolio refresh; stale success must not close a successor modal, and failed refresh must not erase a confirmed deletion. Reopen the current inventory, not a new cleanup intent. No automatic deletion retries.

Package rendering can move with the board, but the owner stays unchanged. Capture the exact path/target and preserve existing inactive/non-pending/non-locked target restrictions. Unknown import outcome is not permission to repeat a multi-owner operation. Do not expand the package format or transfer scope in this extraction.

## Preserve, rather than repair speculatively

The Projects owner already catches normal search/activity postcommit errors. Do not wrap those again and claim every exception means no commit. Keep ExpectedLifetimeId, ProjectWriteAdmission, reservations, source authority, current transaction gates, file authorization and graph projection ownership. The source provides a baseline, not a license to redesign every owner the UI happens to call.
