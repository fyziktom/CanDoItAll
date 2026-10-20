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
| P07 | Agent/process/workflow settings persist and control real execution safely | P04 plus actual affected owners/effects | Workspace/Agent policy owners, retained native recovery and real interactive task planning have focused owner/API/browser proof. Process admission, budgets, generations, publication conditions and canonical material are implemented. Root funding passes 361 cases; active shared scope passes 68 focused repair cases, with initial failures retained. Inherited Agent journals and actual MAF input have 242 distinct passing behaviors; exact child admission adds 73, each across explicit test-only bridges. Shared planning coordination now passes all 170 selected cases after SDK options construction repair. Canonical child/material resolution adds 169 passing selected cases across an exact test-only bridge, with three bounded SQL reads. The registered inherited workspace lifecycle now passes all 185 selected cases, preserving exclusive provider ownership, original input/scope and retained reads after activity repair. Exact publication claim identity and expiry pass 106 PostgreSQL cases, including expiry while waiting for the root lock. Process/Workflow producer activation, inherited Workbench publication, manager continuation, settings capability negotiation and workload qualification remain open; these foundations do not close R5/R6. |
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

P07 continuation: [asynchronous original binding](../validation/2026-10-19-asynchronous-child-binding.md) passes 192 selected cases, repeating the prior 185 workspace/journal/MAF/architecture behaviors and seven new await/cancellation/source-revocation cases. Continue with selected Process source authority and inherited Workbench publication under the existing integration plan.

P07 continuation: [selected child dispatch authority](../validation/2026-10-19-selected-child-dispatch.md) passes 86 cases. Continue with registered source routing and inherited publication, preserving legacy denial and avoiding nested source/executor catalog leases.

The [inherited snapshot prerequisite](../validation/2026-10-20-inherited-publication-snapshot.md) passes 238 corrected cases. No Process/Workflow activation is implied. Next resolve the complete initial Process Project/root gate ordering while preserving original duplicate receipts, then implement held-source authorization, the enlisted publication fence and parent-owned effects/outcomes.

## Process Project/root ordering checkpoint

The [reviewed design](P07-process-project-lock-order.md) is implemented and [validated](../validation/2026-10-20-process-project-lock-order.md): 194 distinct selected passing cases across an exact test-only bridge, complete portability enforcement, unchanged production during fixture repair and retained initial failures. Next connect canonical selected-source reads and the enlisted publication fence, then actual Workbench/Agent continuation and atomic parent outcomes. Keep Workflow/manager/settings activation gated on their own integration proof.

The [selected source fence](../validation/2026-10-20-selected-publication-fence.md) now has 126 distinct passing cases across an exact test-only bridge. Continue with the enlisted Workbench hook, original inherited target/held source and parent-owned Core/MAF continuation. Process/Workflow/manager/settings activation and workload qualification remain open.

The [enlisted task publication checkpoint](../validation/2026-10-20-enlisted-task-publication.md) passes all 72 correctness cases, including real Process expiry before staging and after flush. Its one-second selected-guard performance reference failed for two writers (1006.85 and 1066.32 ms); current responsiveness is reopened and is not a green gate. Inherited producer/continuation and wider capacity remain open.

The [workspace path-probe repair](../validation/2026-10-20-workspace-path-probe.md) passes all 75 selected cases. Exact internal path comparison removes repeated filesystem case-probe writes while preserving fresh containment/link validation and durable journals. The identical selected SQL-wait reference now has all six writers below one second (162.67–670.72 ms). Earlier negative evidence is retained; sustained capacity and inherited runtime activation remain open.

The [live-parent saved-effect continuation checkpoint](../validation/2026-10-20-parent-saved-effect-continuation.md) passes all
201 selected cases on one frozen candidate. Core retains exact original parent authority,
receipt/claim ownership and finite work; MAF accepts an explicit inherited context/request
through its existing governed Process policy pipeline. Both earlier failed candidates
remain recorded. Five direct Release builds and full portability enforcement pass. Actual
Workbench/Process producer, workspace approval lifecycle, manager/historical recovery,
Workflow and capacity remain open; this does not activate inherited product publication.

The [inherited task receipt identity checkpoint](../validation/2026-10-20-inherited-task-receipt-identity.md) passes all 75 selected
cases. Explicit Process-Agent caller/source identity preserves ordinary receipt bytes and
partitions; kind 2 requires active work and matching origin. The two-check migration
preserves rows and refuses downgrade with inherited evidence. Actual inherited Workbench
adapter and Process producer remain the next integration; no product activation is claimed.

The [inherited task publication codec](../validation/2026-10-20-inherited-task-publication-codec.md)
passes all 124 selected cases. Original parent material stays shared; child-specific write
contexts, strict origin decoding and receipt semantics remain distinct. Two failed wire
candidates are retained and the full selection was repeated. Current source/executor
authority, actual inherited publication/producer and workspace approval remain next.

The [exact original Process source checkpoint](../validation/2026-10-20-selected-process-source-authority.md)
passes all 23 selected cases. Source checks select the original launcher from the held
two-Agent catalog without borrowing executor grants or acquiring another lease. The
earlier 22/23 result and exact test-only deletion-intent repair are retained. Current
executor authority, actual inherited publication and producer/approval integration remain next.

The [inherited task publication authority](../validation/2026-10-20-inherited-task-publication-authority.md) passes all 67 selected integration cases,
including 28 new cases. It binds original parent/live work, exact selected source/executor,
current profile generation and the enlisted Process fence through actual SQL publication.
The earlier 62/64 fixture result is retained. Historical receipt authorization, producer,
manager/approval activation and atomic Process outcomes remain open.

The [original inherited Process source read](../validation/2026-10-20-inherited-original-source-history.md) has 107 distinct passing cases across
an exact one-test-file repair. It preserves accepted source fingerprints after assignment
repair, expiry, closure and supersession while always denying dispatch. Four exact indexed
reads replace reliance on current assignment; the earlier 100/107 result is retained.
Product schema 7 receipt integration, producer/manager and atomic outcomes remain next.

The [inherited task receipt history](../validation/2026-10-20-inherited-task-receipt-history.md) passes all 64 selected cases, including 20 new cases. Registered schema 7 history retains original receipts after expiry/repair/closure, applies current disclosure fences and leaves workspace files unchanged. All 28 publication regressions pass with actual Process metadata. Exact SQL/catalog bounds, source/runtime identity, full portability and observed resource closure pass. Producer/provider activation, manager approval and atomic parent outcomes remain open.

The [root sequence registry repair](../validation/2026-10-20-root-sequence-registry.md) passes all 26 selected cases. Completed roots no longer retain semaphores, cancellation preserves waiting callers and disposal is concurrent-idempotent. The first single-mutex cost regression and all baseline/candidate measurements remain recorded; bounded registry partitions restore similar observed independent-root cost while retaining zero idle entries. Full portability and source/runtime checks pass. Probe output cleanup remains explicitly blocked by automatic approval review; sustained capacity is still open.

The [explicit initial inherited source admission](../validation/2026-10-20-inherited-initial-source.md) passes all 247 selected cases, including 16 new cases. Core admits the exact parent through an explicit owner observation and constructs schema 7 directly from its authorized session; ordinary source support does not imply inherited support. The initial 38/244 candidate remains retained. Full portability, architecture, source/runtime identity and observed resource checks pass. Registered Process source integration and complete parent-policy comparison are next; producer/manager/Workflow and capacity remain open.

The [registered inherited Process source](../validation/2026-10-20-inherited-product-source.md) has 153 distinct passing cases across an exact one-test-file repair. Initial and schema 7 observations use the registered owner and compare the complete original accepted policy; current generation, revocation and cancellation remain fenced. Full portability and source/runtime/resource checks pass. The retained performance review reopens whole accepted-launch decoding cost; qualify it before producer activation. Producer/manager, atomic outcomes, Workflow and capacity remain open.

The [accepted-source scaling baseline](../validation/2026-10-20-accepted-source-scaling.md) passes all 15 measurement executions but reopens capacity: a 1024-step source read allocates 31 MB and has a 71.075 ms median. The [persistence design](P07-accepted-source-projection.md) separates immutable original-source evidence from live authority checks, with explicit versions and atomic capture. Producer activation remains gated.

The [accepted-source codec foundation](../validation/2026-10-20-accepted-source-codec.md) passes 41 executions. Selected source bytes stay about 2.6 KB and allocations about 32 KB across the measured shapes, including 1024 steps and a 1 MiB prompt. The one-step codec timing tradeoff is retained. No runtime source path is switched yet; atomic storage, migration, actual SQL/capture cost and source-principal variants are next.

The accepted-source persistence checkpoint passes 193 distinct cases across an explicit one-test assertion bridge. Atomic capture, migration, corruption/rollback/retry and registered-source/history paths are verified. At 1024 steps the selected SQL source is 2319 bytes with 2.904 ms median versus 4449292 bytes / 137.054 ms legacy; initial acceptance still takes 2593 ms, so sustained capacity remains open. Actual producer/manager integration, Workflow and legacy conversion remain gated. [Candidate validation](../validation/2026-10-20-accepted-source-persistence.md).

The [registered inherited task participant](../validation/2026-10-20-inherited-task-participant.md) passes 172 distinct behaviors across explicit correction boundaries: 124 final cases plus 48 unchanged authority/history cases. The single saved tool supports original Process material and interactive planning; schema 7 interrupted receipt recovery preserves work and denies dispatch. Release builds, full portability, exact input checks and observed resource reconciliation pass. Earlier failures and historical cleanup blockers remain recorded. Actual Process producer/manager activation, atomic outcomes, Workflow, settings/SPA and sustained capacity remain open. SPA stays on codex/concurrent-mutations.
