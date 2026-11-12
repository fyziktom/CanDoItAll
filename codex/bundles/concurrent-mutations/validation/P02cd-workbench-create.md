# Workbench root creation: protected native cutover candidate

2026-10-16, integrated execution checkpoint. Changes originated in the managed continuation checkout
`C:\Users\lucys\.codex\worktrees\concurrent-mutations-runtime\CanDoItAll`, based on
`702d0a93b1081c42d5a06893e7070219aee5d089` and were selectively transferred to main.
The owning standard Release builds passed. Components discovered/executed 17:
15 passed and two failed; targeted test-only repairs await a new candidate run.
The browser discovered/executed one and passed. The frozen candidate's rendering
and final portability-static checks passed; see [the validation checkpoint](P02b-managed-candidate-validation.md).

## Behavior and boundary

`ProjectStructurePage.ProjectCreation.cs` now acquires the original native
LocalWorkspace dataset/caller scope when opening. The new adjacent
`ProjectCreation.Pending.cs` partial reads the bounded shared pending inventory
before enabling either save action, validates raw input before capture, then uses
`ProjectNativeDefinitionOperations`, `ProjectEditorOperationSession` and
`ProjectPendingOperationSession` without another codec or transaction abstraction.
Its previous root reservation and legacy `CreateEditorAsync` call are removed.
Creation remains an independent root; hierarchy attachment is a separate action.

Exact frozen preparation and an applied dispatch claim precede the owner call.
Accepted metadata is retained before acknowledgement. Recovery uses explicit
check, same-operation retry, apply, current read, safe prepared cancellation and
resolved clear. Current reads do not replace historical acceptance. Existing
Projects recovery provides restart inventory and metadata disposal; this host does
not add another store, global inventory or automatic recovery writer.

Close, route reset, replacing the hierarchy opening, loss of action context and
component disposal retire the create session. A late acceptance can retain exact
evidence but cannot acknowledge into the retired draft, seed, navigate or refresh
its successor. A successful live acceptance preserves post-dispatch edits in the
same draft and uses the explicit terminal `AcceptedReadOnly` state. It cannot turn
those edits into another create or an update with a silently adopted expectation.
Actual refusal, absence and prepared cancellation retain their existing refusal
state. The shared renderer still enables mutation only for `Ready`.

Starter effects begin only after first live, non-replayed definition acceptance,
with an explicit attempt marker before the seed owner. Refusal and unknown seed
completion keep the accepted definition distinct. Recovery never seeds. Confirmed
seed completion/refusal permits exact-CAS definition-evidence cleanup; unknown
completion remains held for explicit review. A later hierarchy read failure does
not erase the known save result.

The modal's optional `AdditionalEditorActions` slot exposes Projects recovery even
when the draft is terminal or pending. It is a rendering fragment only; Workbench
owns retirement and navigation. An external notice supplies the same action when
opening fails or the editor has closed. All rendering uses existing shared
components. The specific host, partial and shared modal register entries are
updated; no new CSS, package, DI registration or project dependency is added.

## Source-derived validation expectation

| Assembly / exact filter | Expected discovery/runtime cases | Candidate status |
| --- | ---: | --- |
| Components: `FullyQualifiedName~CanDoItAll.Tests.Components.ProjectStructure.ProjectStructureProjectCreateLifetimeTests` | 2 / 2 | Existing two serializable inline rows amended |
| Components: `FullyQualifiedName~CanDoItAll.Tests.Components.ProjectStructure.ProjectStructureProjectCreatePendingTests` | 15 / 15 | Five Facts plus 3 + 4 + 3 serializable inline rows |
| Playwright: `FullyQualifiedName~CanDoItAll.Tests.Playwright.Smoke.ProjectStructureProjectCreateRecoveryBrowserTests` | 1 / 1 | One new Fact |

The two component class filters joined with `|` select **17** cases. Both classes
carry `Category=HostPlatform`. The browser class carries Playwright and
HostPlatform. No provider invocation or fresh runner enumeration supplied these
counts. This adds 15 component and one browser case to the foundation; the main
checkout's already-frozen Stable expectation does not include this candidate.

| Required arrangement | Concrete candidate source / limit |
| --- | --- |
| Prepare failure; lost prepare acknowledgement; lost claim acknowledgement | Three storage-fault rows: no root effect, original operation unchanged, no fallback or repeat preparation; lost claim cannot be canceled as Prepared |
| Actual acceptance, browser write failure, later independent edit | Known-creation Fact: exact outcome retained before ack; explicit lookup/apply and separate current read; no second root, claim or seed |
| Live delayed acceptance with later input | Live-acceptance Fact: same EditContext, later name/phase goal/starter input preserved, accepted IDs/expectation mapped, accepted read-only terminal state |
| Close/reopen during owner save | Existing two lifetime rows: retained exact result, successor unchanged, unresolved root blocks another dispatch |
| Close, route, dispose, new hierarchy while accepted persistence waits | Four retirement rows: no late acknowledgement, seed, navigation or hierarchy attachment; exact browser record remains |
| First-attempt seed success, refusal, unknown | Three seed rows: one explicit attempt, accepted definition retained, unknown retry/check never seeds again, exact resolved cleanup |
| Pending inventory failure | One Fact: editor remains closed, explicit recovery navigation, zero dispatch |
| Restart through Projects recovery | Browser Fact: real owner acceptance followed by scoped IndexedDB accepted-write failure; same reference after full reload; fresh lookup/apply; one root, no hierarchy link or starter object |
| Namespace change after retained preparation | New host Fact uses real `DatasetActivationCoordinator.ReactivateAsync` after lost preparation acknowledgement: original scope and frozen record retained, DatasetChanged visible, Retry disabled, explicit retry/save sends nothing and creates no new root. |
| Draining and next epoch after retained preparation | New host Fact moves only the exact issued epoch's timing through the existing PostgreSQL fixture pattern, checks actual Draining with retry/clear disabled, invokes real `RotateExpiredAsync`, observes ClosedAbsent with original frozen identity, and requires explicit resolved clear. It does not test elapsed wall-clock timing or weaken a production replay promise. |
| Namespace change during editor acquisition; actual caller switch | **Not directly arranged in this Workbench host.** Acquisition retains the existing adapter's before/after scope checks. Shared `Different_caller_context_cannot_claim_or_rekey_the_original_operation` covers a different caller. The new retained-preparation reactivation case is not a claim about every acquisition race. |
| Malformed accepted mapping at this host | **Not newly injected here.** Shared `ProjectEditorDraftTests.Malformed_acknowledgement_is_rejected_before_any_draft_state_changes` and `ProjectPendingOperationSessionTests.Reconciliation_failure_preserves_acceptance_and_cannot_repeat_after_lookup` cover atomic draft rejection and terminal acceptance. Direct host injection remains open. |

Other reused shared contracts include `Only_an_applied_dispatch_claim_authorizes_an_owner_call`,
`An_applied_claim_without_the_exact_dispatch_state_cannot_authorize_the_owner`, and
`ProjectEditorOperationSessionTests.Replay_or_restored_sessions_never_create_starter_effects`.
Their existing proof is evidence for reused types, not a claim that the Workbench
host arrangements have run. Browser loss of post-dispatch typing and raw starter
plans is stated visibly; only the original frozen typed submission is durable.

The two direct scope/epoch arrangements require no production authority seam.
The native adapter is concrete and the owner/receipt codec validates accepted
results; fabricating malformed writer output would require an artificial injection
boundary. That scenario therefore remains with the actual codec/shared session
and atomic draft tests. The new host arrangements did expose a presentation issue:
cached opening context could leave Retry enabled after DatasetChanged or Draining.
The narrow view correction excludes those observations and unavailable recovery,
matching the existing dispatch refusal. Both Facts assert disabled retry without
converting uncertainty into absence or clearing evidence automatically.

## Runtime evidence and remaining closure

The two component failures were test assertions: successful seeding intentionally
removes the submitted starter, and bUnit disallows obtaining Instance after
disposal. Prepared repairs retain the owner before disposal and assert seed outcome
semantics plus one explicit seed attempt. They do not change production behavior.
Rebuild, rediscover and execute those repaired cases before closing this host slice.
Reassess the stated direct-host coverage limits before caller closure.
No legacy owner method is disabled: governed reserved creation, conversion,
hierarchy/deletion and sibling SPA callers remain separately owned.

The passing browser case used `LiveUiHost`, its existing isolated database leases,
1920×1080 page, and captured host log. Its output names are
`workbench-root-accepted-browser-failure.png`,
`workbench-root-projects-recovery.png` and the failure capture
`workbench-root-recovery-failure.png`. The page-only fault targets the exact pending
database/store and Accepted put, asserts one invocation, restores its wrapper in
finally, and is also removed by full navigation. The owner fixture is disposed by
the case. Record actual artifact directory, process/start identity, listener and
lease cleanup after execution.

Actual artifacts are under
`output/playwright/crm-hr-live-agent-tools/20261016T223445618Z-099ca534c9094f14887d0773cb2e00bf`.
Both captures were inspected: recovery reference and actions were readable, repeat
save was disabled and modal/footer controls were not clipped. The case took
30.0396 seconds. Parent verification found host PID 38800 and listener 56411 absent,
and exact database `cditall_primary_8ee0fb439ed64009b30e75df36cbe713` absent with zero
clients. The timestamped server log supplies host identity; the post-completion
process snapshot cannot establish the full historical descendant/start ledger.
Web runtime hashes were unchanged. Unrelated old browser directories and the
separately blocked interrupted-Integration residue were preserved.
