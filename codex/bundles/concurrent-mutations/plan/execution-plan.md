# Execution plan

Work resumed on 2026-10-18 at the user's request. The native chat browser repair passes;
P03 now has passing owner, caller, creation-provenance and six actual-process restart
scenarios. See the [resumption](../validation/2026-10-18-resumption.md) and
[lifecycle integration](../validation/2026-10-18-lifecycle-integration.md) records.
SPA remains on its own branch, named "codex/concurrent-mutations", without merging into
main. Persisted Workspace/Agent policy editing is tested, but runtime enforcement,
remaining owner cutovers and capacity work remain open. The
[earlier handoff](../validation/2026-10-17-checkpoint-handoff.md) retains historical
evidence and preserved drafts; it is not a current stop instruction or final closure.

Status: foundation committed through `702d0a93`; end-to-end implementation remains
open. Current evidence is summarized in the [bundle status](../README.md), with exact
historical candidate outcomes retained in their validation records. Broad Stable is
incomplete, and neither scoped correctness nor finite characterization closes it.

The [C3 candidate](../validation/P04-C3-focused-runtime-checkpoint.md) passed all 190
application cases. Independent nine-child observation and earlier blocked cleanup remain
open. Its source/runtime freeze was released for dormant D1 at
2026-10-17T07:02:11.8227579Z. D1 has seven passing Release builds and final static validation. Its first 247-case run
passed 237 with ten fixture failures; three test-only corrections and 20 additional existing
rows form the [267-case successor](../validation/R1d-D1-first-runtime-and-fixture-repair.md), which passed all cases and final static enforcement. Periodic process observation
stopped on an identity mismatch; final resource checks found no new residue, with that
capture limitation retained. The [C4 cost probe](../validation/P04-C4-small-coarse-runtime-checkpoint.md)
completed all effect/guard checks and failed only its responsiveness reference, confirming
five-second delays for unrelated writers under the coarse organization lock. The reviewed
narrow protocol is integrated. Its [first 703-row candidate](../validation/P04-C4-first-runtime-and-capacity-calibration.md)
stopped with 213 passes, one real reservation race and 489 unexecuted rows. The reviewed
two-file repair passed its reservation cases. The successor discovered 704 rows and
executed 383: 376 passed, seven deletion cases failed, and 321 remain unexecuted.
The deletion failure exposed projection normalization inside an unchanged-source witness;
the correction also protects the earlier preload interval. Legacy audit writers still
need to join the owner protocol before runtime activation.
Resource capacity's repaired successor passed all 126 finite matrix batches and control
checks: 2,195 calls in total. At 32 writers, small saves in one project measured
425–429 ms at p95, versus 34–161 ms across independent projects. This accepts the bounded
baseline and justifies evaluating shared project locks; it does not establish sustained
capacity. Both failed candidates remain retained, and the joint freeze is released.
The joint Resource/deletion candidate is
integrated; canonical migration/model/script review passed. Two test-only compile fixes
have passing builds; all eight standard Release outputs and final portability enforcement
passed before the [first Resource runtime candidate](../validation/P05-R1-resource-first-runtime-checkpoint.md).
It executed 228 cases: 227 passed and one migration-test observation query failed. The
reviewed cast correction passed the complete [346-case successor](../validation/P05-R1-resource-catalog-cast-runtime-checkpoint.md),
including actual migration refusal/Down proof. Both failed browser candidates remain
preserved. The active-source fixture repair passed all four fresh browser Facts and
parent-reviewed lane closure; the freeze was released at 2026-10-17T11:20:36.0215536Z.
Ordinary 346 remains historical evidence under the exact source/runtime bridge, not a
rerun on the browser candidate. The incidental sandbox Web profile gap and Data Sources'
external second-profile gap remain explicit; the latter is supplemented only by the
actual after-restart HTTP runtime response. Resource shared-lock stage 3 still requires
its reviewed implementation and comparison against the accepted finite baseline; browser correctness does
not establish capacity or complete allocation observation.

The user authorized implementation after design review, tests and incremental commits.
Dependency-ready work proceeds within that scope without another approval ceremony.
Source-only preparation is not executed proof. Persisted planning policy, runtime
activation, Process/Workflow producers, durable lifecycle recovery, remaining owners,
capacity and final operational closure retain their entry gates. SPA transport is
signed as `70c65dd`; signing remains enabled for the final MAIN checkpoint.

| Phase | Coherent outcome | Dependencies | Entry/closure evidence and status |
| --- | --- | --- | --- |
| P00 | Refreshed architecture, complete first-owner perimeter and risk/test plan | Source package/current repository | In progress: independent persistence/runtime/UI reviews; baseline Infrastructure build passed with existing NuGet warnings |
| P01a | Transaction cleanup always releases acquired process gates | P00 cleanup design review | Pass: six discovered/passed tests, Infrastructure Release build, independent diff review and unchanged portability enforcement; see validation/P01a-cleanup.md |
| P01b | Commit knowledge, source lock order and lifecycle namespace defined/proven | P00; P01a for lock fault proof | Foundation committed: explicit file/SQL activation, retained epochs and bounded exact receipts; focused transfer-lock, lifecycle, recovery and offline-command process proof recorded. Integrated Stable and later-owner participation remain open. |
| P02a | Projects definition handler and original-state contract | P00/P01 applicable guarantees | Conditional-write checkpoint recorded in validation/P02a-validation.md: builds, focused owner/HTTP/tool/UI/browser, migration/import, model/script, rendering/docs and portability pass; performance comparison retained with P02b tail-cost follow-up; no durable-replay or broad Stable closure claim |
| P02b | Complete Projects callers and durable result/projection | P02a/P01b | SQL receipt/search, protected HTTP and selected governed owner paths committed. R1a passed 41 Unit +50 Integration; bounded retention passed 12 Unit +10 Integration +12 epoch regressions. H1 passed 54 cases again. Repaired H2 passed 10 Unit +8 observer +12 PostgreSQL rows. R1b initial immutable evidence passed four rows; its complete bounded continuation source is reviewed and awaits build/runtime proof. Complete runtime activation, producer inventory and sustained capacity remain open. |
| P02c | Draft-preserving conflict and unknown-result recovery | P02a/P02b | Native Projects recovery, strict IndexedDB CAS and metadata-only reference disposal committed. Workbench browser and repaired 17 component cases passed. HTTP/schema 49 passed; the final documentation repair passed 23 coverage +3 repeated structural cases. SPA generator/asset preparation is committed at `2fe9e8e` with 20 Node cases, app build and real asset/base proof; generated client and complete writer cutover remain open. |
| P03 | Original-state deletion and hierarchy intentions | Closed relevant P02 producer/runtime boundaries | Owner protocol, canonical migration, native/API retained intents, creation cleanup, governed hierarchy and exact definition claim fencing have focused passing proof. Created definitions support bounded original creation provenance through live/replay/reconciliation paths; all six actual Agent/Process restart scenarios pass. See validation/2026-10-18-lifecycle-integration.md. Complete original execution-store recovery capture, remaining compensation/caller recovery, legacy quiescence, selected-key capacity and final combined proof remain open. |
| P04 | One governed task-plan publication with trusted bounded basis | P02; selected Workbench owner witnesses | B1 passed 12 Unit; its test-only exact-backend observer repair passed all 10 Integration cases. C1 atomic staging passed 10 Unit +6 Integration. C2 neutral epoch metadata, permanent migration refusals and installed-pool fence are integrated with canonical migration and focused proof; the repaired readiness lane passed all 12 cases. C3 canonical integration and 190 application cases passed; independent nine-child observation remains incomplete. D1 dormant dispatch has passing builds/static enforcement and a complete repaired 267-case pass; original 237 passed/ten fixture failures remain retained. C4's coarse probe failed responsiveness. Its first narrow candidate passed 213 and failed one reservation race, with 489 unexecuted. The repaired 704-row candidate passed 376 and failed seven deletion cases, with 321 unexecuted. A corrected deletion witness and selected-authority lease have reviewed source checkpoints; audit producer coordination, actual authority/provenance, persisted settings, sustained capacity and approval proof remain open. |
| P05 | Remaining owner waves have complete original-state protection | Closed relevant foundations | Workspace W1b owner/native/HTTP protection is integrated; 25 presentation and three browser cases passed in the 330 checkpoint. SPA cutover and durable recovery remain open. Resource R1 passed canonical migration/model/script review, affected builds and portability enforcement. The first candidate passed 227/228; its test-only cast correction passed all 346 ordinary cases, including PostgreSQL refusal/Down and deletion stage 2. Both failed browser attempts remain retained. The repaired active-source fixture passed all four browser Facts; ordinary 346 remains historical under the exact bridge. Sandbox auxiliary-Web and DataSources external-B profile capture limits remain explicit. Resource's repaired finite capacity baseline passed 126 batches and 2,195 calls; shared-lock implementation/comparison and remaining owners remain open, including Workflow definitions, components and global settings with original namespace and durable intent recovery. |
| P06 | External effects have explicit conditional/idempotent/uncertain semantics | Relevant owner wave | Installed-driver capability matrix and recovery/compensation plan source-reviewed in P06-external-effects.md. Implementation and provider-specific proof remain pending. |
| P07 | Agent/process/workflow settings persist and control real execution safely | P04 plus actual affected owners/effects | Pure policy/resolver/codec passed eight Facts. Workspace complete-policy contracts/owner, Agent policy owner and migration tests are integrated. The canonical Release build passed, and migration 20261017130214_AddWorkspaceDecisionDataPolicy was generated with only the nullable policy column and byte-bound constraint. Reviewed refusal SQL is installed; migration execution, affected builds/static, UI/API integration and native/browser proof remain required. Actual admission/checkpoints, policy-bound approvals, claims, bounded replan and full Agent/Process/Workflow producer-to-owner proof remain pending. |
| P08 | Complete release/operational closure | All required work | Pending: compatibility, migration/restore, measurements, frozen broad gate, portability and owned-resource cleanup |

Parallel work uses disjoint file ownership and reviewed contracts. During a frozen validation candidate,
new source is authored only in the owned continuation checkouts; no builds, tests or
database changes run there. Projects command, writer and caller changes remain ordered;
do not independently invent DTOs. Builds sharing standard outputs remain serialized by
the parent agent after the gate releases them.

## Validation rules

Each selected work card records exact affected projects, filter, expected discovery,
invalidation keys and results. Later phase envelopes above are not falsely declared
implementation-ready: decompose them using the then-current accepted contract before
their source edits. Existing test class names are discovery seeds, not passing proof.

Foundation/schema/public-contract changes trigger a single broad stable checkpoint
when that integrated candidate is frozen. Repeat only after a relevant invalidating
change or failure, never after an evidence-only edit. Apply final portability-static
enforcement to every protected-source change and refresh only reviewed baseline deltas.

## Reopen rules

Reopen the owner design if another writer, incompatible caller, lock inversion,
unbounded source read, missing receipt knowledge or dependency cycle is discovered.
Reopen runtime policy if the selected publication cannot validate its material inputs
atomically or a replan can duplicate an uncertain external effect. Invalidate dependent
proof when source/migrations/contracts/asset mode change. Do not substitute prose for
missing SQL, browser, process or retained-data evidence.
