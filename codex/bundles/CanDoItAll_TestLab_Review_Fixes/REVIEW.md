# Review of the completed TestLab slice

## Verdict and boundary

Keep the extraction. The light contracts, complete four-section renderer, per-page production session, narrow owner adapter, real sandbox and original shell-lifecycle repair are present and architecturally consistent with the chosen in-process UI boundary. No new fundamental production persistence defect was established in the inspected paths. This is not a claim that every execution path or dependency was independently tested.

**Do not yet use this sandbox as a finished reference for the next module.** Correct R1 and R2 and close their real-control proof first. C1 is a small user-facing outcome-label correction, not a new mutation protocol.

Repository: `fyziktom/CanDoItAll`; reviewed branch: `components-decoupling`; reviewed HEAD: `3c579fd1a923ad90f619fe144e6e4c1fe081fa8b`. GitHub comparison against the preceding review (`97989b9d13b9a6fa16280da98ec5b005a2f968c7`) reported three commits ahead and none behind. The original assignment was also committed under `codex/bundles`; those instruction files are not implementation proof. See [sources](SOURCES.md).

## What should be retained

| Area | Source-supported result | Evidence |
| --- | --- | --- |
| S0 shell | Token captured while the source is alive; disposed checks between contributors and inside dispatched renders; subscriptions detached; initialization retained and eventually observed; source released after initialization without waiting for a noncooperative initializer in the usual disposal path | S01, S02 |
| Contracts | DTO namespace, enum values/defaults, JSON-ignored lifetime summary and exact Projects admission retained in a light assembly; shared child identity is a small public contract | R09, T02 |
| Renderer and host | `/test-lab` remains a module route. It creates its own session, maps notifications and renders the extracted four-section surface; it does not create a new HTTP backend | R06, R07, R08 |
| Production reads | Independent plan/list/project/party lanes and stale-completion fencing; repeated route preserves the editor; missing/unavailable/stale states are explicit | R03, T02 |
| Production writes | Deep captured submission, per-origin admission, duplicate-submit gate, original-row identity matching, known-commit identity before read-back, newer field/input retention, unknown-outcome lock and read-only retry | R03, R04, R05, T02, T04 |
| Sandbox build direction | Sandbox references the UI project, not Web or the implementation module. Production CSS is a linked content item, not a Web project reference | G01, G02 |
| Tests | Real BaseLib form tests, session races, production PostgreSQL-backed form paths, production and sandbox Playwright journeys, transitive-boundary rejection including unresolved assemblies | T01–T05 |

These findings describe the inspected source. The complete evaluated source-mode graph, transitive package counts and measured timings are author-reported receipts, not measurements repeated by this reviewer.

## R1 — committed read-back cancellation strands the active sandbox draft

**Priority:** required functional correction in the development sandbox. **Production impact established:** none for this specific sequence; the production session uses a separate party-read lane.

Affected source: `TestLabScenarioWorkspace.SaveAsync`, `ChangePartyAsync`, `RetireReads`, `WaitAsync`; `TestLabDraft.Finish`, `CanSave`; and the real responsible-party selector. Evidence R01, R03, R04, R06, T01.

Concrete sequence:

1. Open `DelayedReadback` with its representative plan. Save the current draft.
2. `Store.Commit` completes. The submission's identities and `LastCommitted` are retained. Save now awaits `WaitAsync(write: false)` for the committed read-back. The editor remains editable.
3. In Overview, change Responsible party to None. The callback is valid for the same draft and target version.
4. `ChangePartyAsync` calls `RetireReads`, which completes **all non-write waits** with `false`, including this save's read-back.
5. `SaveAsync` returns from `if (... && !await WaitAsync(write: false)) { return; }` before calling `origin.Finish`.
6. The same active draft still has `Pending == submission`, `SaveState == Pending`, and `CanSave == false`. The controlled wait is gone, so Complete pending cannot help. Retry refresh requires `Pending: null` before reconciling and also cannot settle it.

The fake record has already been saved. This is **not a rolled-back write** and must not become Unknown or be replayed to unlock the editor. It is not fixed by hiding the selector or freezing all editing during save. A reset abandons the current draft and therefore is not acceptable normal recovery.

The existing read-back form test types a title and case notes but does not change the responsible-party selector. The scenario disposal tests cover DelayedRead, DelayedReferences and DelayedSave, not this same-live-draft cancellation. Their presence does not close R1.

Accept either the smallest independent read-lane correction or safe settlement of a superseded read-back into a known-committed state with read-only recovery. Preserve the admitted write and the newer party choice. Any completion/cleanup must act only on its own submission and may not release a newer submission after A → B → A or editor retirement. Do not share production services with the sandbox to eliminate duplicate code.

## R2 — global saved-reference fallback differs from production

**Priority:** required sandbox parity correction. **Production impact established:** the production implementation already has the missing fallback behavior.

Affected source: `TestLabScenarioWorkspace.SetReferences`, `ChangeProjectAsync`, `TestLabScenarioStore.PartyId`, and the renderer's unavailable-choice presentation. Evidence R01–R03, R06.

Concrete sequence:

1. The representative sandbox has a saved `ResponsiblePartyId` equal to its known `Store.PartyId`; its project-scoped choices contain Delivery reviewer.
2. Explicitly change Project to None. The same draft keeps the saved responsible-party identity and now has the valid global `ProjectId == null` / `ExpectedProjectAdmission == null` pair.
3. `SetReferences` returns an empty list merely because the plan is global. The renderer inserts Unavailable responsible party, even though this scenario still has the known party and no lookup failure.

The production `LoadPartiesAsync` treats an absent project-scoped list separately from saved-reference resolution: it calls `owner.PartyAsync(savedId, ...)` when the saved ID is missing from the list, including for a global plan. A global scope does not itself establish that the saved party is unavailable.

Use a small actual fake reference catalog/lookup or equivalent scenario data. A known saved party must resolve independently of the current project list. A genuinely unknown ID, a deliberately unavailable reference and a failed lookup must remain distinct. Never manufacture a successful label for any arbitrary Guid, erase the saved ID, or silently change project admission.

The existing MissingReferences tests must continue to render the unavailable choice. Add a positive global-fallback case and a delayed/stale fallback check in the appropriate existing test layer. The fake needs only the reference behavior actually used by this workspace, not a simulated CRM backend.

## C1 — unknown production outcome receives a failure heading

**Priority:** low, bounded presentation correction requested with these repairs.

`TestLabWorkspaceOwner` correctly returns Unknown for an unresolved write and the draft correctly remains locked. However, `TestLabPage.Notify` routes all states other than Saved and SavedWithWarning through the title `Test plan save failed`. This includes Unknown, even though its detailed message explicitly states that the result is unknown. Evidence R07, R08.

Keep the explicit Unknown outcome and replay protection. Give it an uncertainty-specific title, such as `Test plan save outcome unknown`; use an appropriate severity under current UI conventions. Do not infer a failed commit, silently retry, or treat a previously committed receipt as evidence for this unknown submission. A focused real-host notification test should assert both the title/meaning and the unchanged lock/recovery semantics.

## Evidence and completeness

The implementation record reports 115 distinct passing cases: 28 session, 20 light UI/scenario/boundary, 28 host/shell-related, 35 integration, and 4 browser cases across TestLab and Collaboration. The inspected TestLab browser class has three Fact methods; the four-case receipt is a combined filter, not four TestLab methods.

The existing source includes material evidence for original owner/postcommit semantics and S0. No tests or benchmarks were executed by this review, and ignored TRX/screenshots were not supplied. See [proof status](PROOF_STATUS.md). Do not replace a historical receipt with a new assertion of success without executing the current filtered tests.

The read scopes in the source register are intentional. Full recursive-tree and directory inventories are not equivalent to semantic review of all files. No complete new-module implementation brief is justified by the limited candidate inventory made during this review.

## Scope of the corrective run

Required: R1, R2, C1, deterministic and actual-control regressions, appropriate production parity/browser checks, maintained documentation and current static gates. Preserve S0 and the extracted architecture. No new feature projects, schema changes, provider/CRM refactoring, API-only rewrite, general state framework, or next-module implementation.
