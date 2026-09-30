# W2 — Data Sources, transfer and restart activation

## Full shipped scope

Complete the entire current Data Sources surface: saved PostgreSQL profile list/search, exact editor, current canonical selection and pending-restart summary, raw connection fields, password intent, root, create/reset/save/delete, connection test, schema health/apply, Create empty and the full settings-transfer dialog. Include explicit transfer source selection, source/target schema actions, available groups and selected-group transfer. Preserve the current absence of legacy SQLite/clone/snapshot UI. [WS14, WS15, WS19]

Do not start a full MainLayout redesign. Its startup prompt, selector/flyout, browser notifications and activation messages are mandatory consumers to regress. Reuse the existing service; an intentional app-host adapter is not a reason to push module dependencies into AppComponents. [WS16]

## Read the owners before moving anything

Inspect the actual current implementations of `IDatabaseProfileService`, credential protection/control-plane writes, runtime accessor, `IDatabaseSwitchCoordinator`, bootstrapper, driver registry, profile factory and every selected `IDatabaseTransferHandler`. This review read the façade and transfer coordinator, not every implementation. Record commit boundaries and known deterministic refusals before designing outcome projections. [WS14, WS17, WS18]

There are distinct identities: the profile editor, persisted profile catalog, current process's canonical database, pending restart profile, transfer source and target, and per-handler transfer result. Do not collapse them into one mutable `CurrentProfile` field or use current UI selection after an await.

Configuration-owned startup overrides remain locked. A normal unlocked UI fixture must start from a task-owned persisted profile catalog; supplying a startup connection override merely to simplify setup exercises the locked path, not normal profile management. Existing tests show how the unlocked fixture removes those overrides. [WS19]

## Preserve application behavior

`ActivateProfileAsync` delegates to the existing coordinator. The UI labels the action **Activate for restart**. Preserve persisted selection versus effective canonical process state; a stored new choice does not make already-built factories or scoped owners use another database. Restart only the owned test host to prove activation. Do not implement hot switching, change singleton/scoped factory lifetimes or bypass the write fence. [WS14, WS16]

Save profile does not mean create its database. Create empty and Apply schema include actual driver/bootstrap steps. Test connection currently goes through the database driver's `EnsureDatabaseAsync`; inspect its exact semantics and do not describe an effectful path as a pure ping. Do not run these operations on retained, production or unknown endpoints. A failure after a completed step may leave real progress. Present the known stages and provide safe observation/recovery, not a false global rollback guarantee.

Profile deletion must preserve the original owner's meaning and safeguards. Verify whether it removes only saved configuration or also physical data; never turn it into a database drop as part of UI cleanup. Preserve active/pending/locked-profile refusal and root/secret rules found in the real owner.

Transfer is not a newly invented all-or-nothing transaction. The coordinator dispatches selected handlers in order and gathers per-group results. Retain exact SourceProfileId, TargetProfileId, selected keys and ReplaceExisting. Validate selected groups against current available previews in the UI and preserve actual backend policy. Record successful, refused and uncertain group outcomes; do not convert a later failure into zero overall progress. [WS18]

## Reproduce and repair the existing UI lifetime risks

The unextracted panel currently sets busy flags around awaits without a universal finally, passes its live profile model to Save and replaces the editor on completion. Transfer close clears `transferBusy`, and the transfer completion later reads mutable target state for Reload. These are risk paths in the old surface, not findings against the last Storage-selection commit. [WS15]

Build failing-first tests for the reachable variants and repair them in the new state layer:

- Capture immutable non-sensitive request fields and one-use password intent before the first incomplete await. Copy selected lists; no deferred enumeration over a changing UI collection.
- Admit Save/Delete/Create/Test/Apply/Activate/Transfer in the handler, not just through disabled markup. Conflicting operations must not overlap merely because one dialog closed. Independent read lanes need not freeze all Settings.
- Separate acquisition of the exact profile editor from loading the list, schema health or transfer preview. Missing must not silently become a new profile. Same current acquired target preserves its draft; error/missing target can retry.
- Preserve EditContext, raw incomplete numeric text and later edits. Confirmed ID and safe version metadata are adopted before optional reload. Never retain a full connection/password command in generic history.
- Same source/target plus a newer dialog or newer selection revision is still a new UI lifetime. A-to-B-to-A, close/reopen and later completion cannot retarget a transfer or close a successor editor.
- An accepted operation may finish at its captured owner while its view is closed. Keep safe result records independently; do not drop confirmed progress or unlock an uncertain operation by opening a new dialog.
- Keep current runtime summary, persisted profile list, schema health and transfer data independently available. Initial read failure is not an empty healthy workspace.
- Recompute schema/transfer readiness from complete current evidence. A missing health result must not mean schema current. Do not waive owner checks on stale previews.
- Do not expose raw connection-string exceptions or password material in messages, receipts, URLs, trace attachments or snapshots.

Password semantics must match the real owner (for example whether blank retains an existing value). Never infer or change that behavior from field labels. Clearing sensitive references is not a claim of guaranteed managed-memory zeroization. Use a separate ephemeral field/intent with an explicit lifetime.

## Required real tests

Create two private PostgreSQL 18 databases/profile roots, A and B, with distinguishable harmless markers. Seed baseline groups through actual owners. Exercise the unlocked Data Sources UI to create/save B, test the connection, apply schema as supported, select A as transfer source, preview and transfer a bounded explicit group. Verify source unchanged and exact target records/identities according to each real handler's contract. Do not use a direct service call to stand in for the target UI action.

At least one transfer case includes a failed later handler after earlier progress, and another covers a revoked/unavailable source or target and a stale/closed dialog. Use owner read-back and per-group results, not only a success toast. Never copy real credentials, real user accounts or unrelated application data. API accounts/tokens are instance-local control-plane state and are not business-profile transfer data.

Select B for restart; verify A remains canonical in the still-running owned host. Restart that host using its persisted selection and verify B is effective. Reopen Settings and Project Structure, check profile-owned preferences/secret metadata/resources are isolated and permitted API account/token metadata remains instance-local. Test the startup-override locked mode separately. Test pending selection and restart failure visibly without modifying production launch configuration.

Regress the real MainLayout startup dialog, selector, browser back/forward and cross-tab notification handling. The old Canonicality architecture tests and profile/transfer/secret/storage integration suites remain applicable. A changed façade signature or root registration triggers all actual consumers; do not delete test assertions to hide a graph break.

Deliver an independent Data Sources sandbox with actual renderers, raw fields, locked/unavailable/empty/current/pending states, two profiles, staged transfer and deterministic partial outcomes. It must never create a database or launch an external process.
