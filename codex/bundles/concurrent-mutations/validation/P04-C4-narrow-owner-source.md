# C4 narrow file-owner source checkpoint

This checkpoint prepares the existing file-store writer perimeter and caller compatibility for independent review. It is managed source only. No build, test discovery, implementation test, database, host, portability scan or performance run was executed. The previous D1 267 passes and coarse C4 reference failure remain evidence for their original candidates, not this candidate.

The exact transfer map is `../reviews/P04-C4-narrow-owner-source-inventory.json`: 24 C# inputs, comprising 16 guarded existing successors and eight new files. Sixteen are production inputs and eight are test inputs. Every existing source has a preserved byte-exact MAIN predecessor, checked again at freeze. New paths must still be absent in MAIN. The unchanged JsonStore, ExecutionSlice and DurableFileWriter are dependency pins, not transferred optimization changes. The inventory separately binds selected test/dependency sources, the two UI-owned checkpoints and the proof records.

## Ownership and publication order

The existing store owns one explicit operation. Preparation and existing recovery start under its instance gate and workspace file lock. A closed, sorted Agent/run/chat key set is computed from the real prepared mutation. The operation releases the workspace lock and instance gate before waiting for those keys, then reacquires them and checks the saved byte/membership/pending witness. Nested publication may only use an already-held subset. A changed original, newly required key or pending publication refuses without replaying the caller callback.

The complete sorted key set has one monotonic one-minute acquisition budget. Each durable-file acquisition receives only the remaining allowance, and a post-acquisition check refuses late completion before another key or publication. Original caller cancellation takes precedence. The optional internal TimeProvider exists only on the internal scope entry, defaults to System and adds no store/DI contract. `workspace.publication.keys.wait` ends before `workspace.publication.workspace.reacquire`; the existing `workspace.coordination.wait` remains nested in actual workspace acquisition. Filesystem calls, workspace waiting, witness reads and terminal disposal are not collectively claimed to have a hard deadline.

Publication holds the short workspace guard while applying the unchanged durable boundary. The target is deeply frozen before a key wait. Existing immutable journal/history evidence can remain shared; every reachable mutable list is copied, including nested lists. Malformed null collections/elements are preserved for the existing validator rather than normalized by the freezing helper. The existing original run-reference witness remains the original observed object; only callback inputs/targets are frozen.

Operation and cleanup failures remain observable together. Partial keys release in reverse order, and ownership is cleared before each terminal disposal attempt. A direct prepared conflict cannot conceal an earlier effect, and an aggregate containing uncertain cleanup is not translated to a definite conflict.

## Complete source perimeter for this checkpoint

| Source owner | Included paths and boundary |
|---|---|
| Catalog/full workspace | Existing Save/Update, legacy split/import, seed normalization and catalog policy publication use the operation. Full-workspace and catalog-observing callbacks retain the broad original witness. |
| Runs, chat and background reservation | All ordinary and D1 writes enter through the existing store and its background/chat paths. Existing run-only updates bind their exact run root plus previous/target chat bytes; shared execution/chat/usage indexes are planned from current data after reacquisition. An unrelated run can commit during the key wait; the selected original cannot change. |
| Reporting/read-triggered mutation | The reporting partial's index advancement now carries the operation into split initialization and recovery. Lower JsonStore/ExecutionSlice/chat read paths were audited for canonical payload writes; their other read-triggered writes are indexes/projections. Recruiting evidence remains in its existing separate payload domain. |
| Four semantic recoveries | Chat-run start, generic run start, existing-run detail update and Agent deletion derive complete keys from the actual retained journal and compare its exact original before replay. No new journal format or retry loop is introduced. |
| Agent deletion | Final catalog policy equality is checked before the deletion journal. After effects and on recovery, the exact journal TargetCatalog is published; no later transform is adopted after deleting the run data. |
| Final capability validation | The reviewed existing-store overload accepts one synchronous validation-only Action. It receives the exact frozen target after final physical/key/policy checks, including an unchanged accepted result. Unsupported implementations throw explicitly; the capability consumer never falls back to the old overload. The provider check is an observation, not a held provider SQL/runtime permission fence. |
| Caller knowledge | Effect checkpoint conflicts use the existing typed mapping. External provisioning and package import map only direct physical/revision conflicts to their existing typed conflict result. Prepared receipt locals never escape a failed store await. Cleanup aggregates stay uncertain. The existing capability proxy now instruments and forwards the three-argument overload without changing its two-argument behavior. |

Both witness capture and final comparison check DurableFileWriter's exact generated pending-file grammar. This includes absent targets, affected recursive directories and shared catalog/workspace/execution/chat/usage/journal targets. A matching marker refuses before this new prepared operation writes or deletes anything; malformed lookalikes do not. The four semantic journal witnesses are separate. Existing recovery may have completed before a later operation refuses, so this is not a whole-call no-write guarantee. The directory/hash work has no capacity qualification here.

## Source-derived proof selection

The exact method identities, source lines, explicit row counts, filters and selected source hashes are in `../reviews/P04-C4-narrow-owner-source-selection.json`. The current selection contains 703 ordinary rows: 247 Unit and 456 Integration, across ten disjoint lanes, 487 methods and 75 source files. A whole-class filter audit includes the existing typed-recovery and execution-tracking partial files. All theories use explicit InlineData; no dynamic data, skips or overlapping method selection is counted. These are expected discovery counts, not executed results.

| Lane | Expected rows |
|---|---:|
| D1 schema/authority Unit regression | 51 |
| D1 receipt/result Unit regression | 21 |
| D1 activity/current-profile Unit regression | 60 |
| D1 admission/cancellation Integration regression | 82 |
| D1 origin/receipt Integration regression | 31 |
| D1 application/activity Integration regression | 16 |
| D1 dormant owner Integration regression | 6 |
| Store/catalog/deletion/projection/caller Unit | 115 |
| Store publication/recovery/selected-run Integration | 148 |
| Owner/capability/external API Integration | 173 |

The authored increment is 20 rows: three prepared-value Unit Facts, two caller receipt Unit Facts, four final-validation Integration Facts, nine recovery/pending/selected-run Integration rows, one aggregate-budget Integration Fact, and one additional row from converting the existing competing-run Fact into two explicit overload cases. The existing capability proxy correction adds no row. The prior D1 267 remain an unchanged subset, but require fresh execution against this candidate.

The new proof source distinguishes its arrangements:

- Real separate file-store instances and durable key/workspace handles test callback-once conflict, unrelated-run progress, exact raw byte and semantic-journal changes, terminal release and all four recovery paths. The generated `.pending` files are deliberate filesystem arrangements, not a Windows replacement-fault execution claim.
- The immutable-value tests compare actual JsonStore serialization around caller alias mutation. They do not assert that reflection or unsafe mutation is supported.
- Final validation uses the real capability publication consumer plus a controlled independent provider-revision source, invalidated after a positive key-wait observation. It does not run provider dispatch or assert a held external permission fence.
- Caller receipt tests run the real provisioning/import services with a store that prepares then refuses publication. They prove return/classification behavior and callback/read counts, not filesystem atomicity.
- The clock-controlled acquisition test uses actual file keys and terminal release. It establishes aggregate-budget control flow, not an OS deadline measurement.

The source review trail binds UI prepared values 9459B84B/ABE6DAB1, final validation 076AF28D, caller tests 22FA9A9B/621104AB and the competing test EE257EE4. Both peers reviewed pending witness 57D6453A and run-only Store BB97CAE6; persistence reviewed recovery test D92FECB7 including terminal observation of the unrelated run task. The later aggregate-budget successor and its proof are pinned by the final inventory; earlier 77598C74 scope bytes remain preserved as a rejected budget predecessor. Bounded source review is not compilation or execution proof.

## Qualification and next checkpoint

The parent must first review the guarded transfer, then build the affected standard Release projects serially, run the repository portability-static and other required static gates, and require exact discovery/execution for the selected candidate. The full repository suite is not replaced by this selection. This source stage does not invoke or prepare a new validation runner.

The existing coarse catalog read lease remains coarse. A held selected current-authority lease, the Workbench/C3 binding and matched C4 probe are the next coherent checkpoint. No current-source authority is inferred from the broad mutation witness. Whole-directory witness cost, mixed writer behavior and recovery at actual process boundaries remain qualification concerns. Supported adoption requires stopped, homogeneous writers; an old writer does not understand these private keys.

Ordinary live-provider origin, schema-six policy/basis provenance, persisted active-work accounting, crash recovery, Process/Workflow producer entry and settings activation remain separately gated. Neither this source inventory nor a later ordinary test pass enables those features or establishes capacity/SLA claims.

## Final unchanged-dependency binding

Final verification found the managed activation-boundary test missing the parent's already-repaired Infrastructure import, and a line-ending-only difference in the Unit project. Both are explicitly bound to current MAIN in the selection/inventory; neither is a transfer input and neither managed file was changed. The initial unexecuted 698C5EF2 inventory and 96589C32 selection are retained in owned staging. The current selection's MAIN source line locations are refreshed; all methods, filters and row counts are identical. The source-only generation scripts and original class-filter audit remain historical steps, followed by this explicit binding amendment.
