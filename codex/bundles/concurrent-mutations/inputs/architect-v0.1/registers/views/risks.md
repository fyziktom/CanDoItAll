# Risks

Generated from the adjacent JSON register. Narrative documents may also require review after decisions change.

| id | title | trigger | impact | mitigation | phase | tests |
| --- | --- | --- | --- | --- | --- | --- |
| R01 | Enabled alternate writer bypass | Any unconverted enabled caller | Data loss | Close pilot call graph and convert or disable entire owner perimeter. | P00 | T001; T020; T021; T090 |
| R02 | Fresh-token laundering | Convenience adapters and automatic retry | Data loss / invalid decision | Bind original read/proposal and reject semantic mismatch; new intent on replan. | P02 | T004; T056 |
| R03 | Child-only mutation does not stamp root | Direct child/bulk/import writers | Lost child edits | Explicit aggregate participation and root-child atomic tests. | P01 | T005; T007; T016 |
| R04 | Mixed aggregate read | Multi-query editor read under concurrent commit | False acceptance/rejection | Bounded coherent read and actual interleaving test. | P01 | T006 |
| R05 | Whole-list identity churn | Broad snapshots or large lists | Data loss / extra writes | Guarded replacement first; stable row mapping and validated narrow intentions. | P03 | T035; T049; T050 |
| R06 | Unrelated local stripe contention | Relational SerializableMutationScope path and fan-out | Latency / reduced throughput | Measure wait/hold and branch; change strategy only with multi-host proof. | P01 | T013; T088 |
| R07 | Per-key SQL roundtrip amplification | Large dependency/guard sets or remote database | Latency / long transactions | Bound complete sets, batch permitted checks, avoid N+1, preserve ordering. | P01 | T003; T063; T088 |
| R08 | Source ordering regression | Cross-owner/governed integration | Deadlock / authority gap | Map lock classes and reuse source-first branch before adding guards. | P01 | T012; T093 |
| R09 | Gate retained or leaked on cleanup | Postcommit callbacks or cleanup faults | Liveness / blocked unrelated work | Measure branch-specific lifetime; safe finally cleanup with outcome preservation. | P01 | T010; T011; T017 |
| R10 | Commit misclassified as rejection | Postcommit exception / cancelled view | Duplicate effect / confusing UI | Record known commit at owner; separate read retry and unconfirmed lookup. | P02 | T008; T009; T034; T092 |
| R11 | Duplicate operation without durable truth | Lost response, duplicate create, restart | Duplicate data/effect | Minimal owner durable result linked to existing journals, uniqueness and same transaction. | P02 | T024; T025; T027; T029 |
| R12 | Receipt identity or privacy mismatch | Retries/client reuse/access revocation | Wrong result / unauthorized disclosure | Versioned request identity, trusted scope and authorized result lookup. | P02 | T026; T028; T096 |
| R13 | Receipt retention breaks long replay | Long-lived workers/clients and cleanup jobs | Duplicate historical effects | Separate full detail and dedupe retention; explicit expired-operation contract. | P06 | T078 |
| R14 | Restored dataset re-admits future work | Destructive restore/replacement | Wrong-incarnation write / duplicate | Proven dataset activation epoch or equivalent fence, not per-restart reset. | P02 | T039; T040; T041; T096 |
| R15 | Projection regression or lost intent | Concurrent saves, restart, worker failure | Stale/missing search | Choose atomic small projection or durable ordered/canonical rebuild. | P02 | T036; T037; T038 |
| R16 | Projection increases write critical section | Index growth or hidden external computation | Latency / availability coupling | Measure bounded participant cost; choose durable alternative if necessary. | P02 | T037; T088 |
| R17 | Snapshot-only source check mislabelled strict | Long decision or overlapping source writer | Stale agent decision | Explicit publication semantics and all-writer source guards/predicate witness. | P04 | T053; T054; T057 |
| R18 | Model omits material facts | Generic agent basis design | Invalid decisions / false conflicts | Trusted mandatory input policy and bounded exact proposing-call lineage. | P04 | T055; T062; T063 |
| R19 | Set membership/absence unprotected | Planning/assignment/capacity queries | Invalid relational decisions | Owner collection/predicate witness maintained by every relevant writer. | P04 | T054; T065 |
| R20 | Approval reused for changed intent | Conflict recovery/resume | Unapproved effect | Bind effect+basis+scope; explicit new approval for material change. | P04 | T056; T081; T094 |
| R21 | Automation fights manual edit | Human override without product policy | Repeated unwanted changes | Explicit pin/pause/takeover vs edit-with-automation; owner attempt fence. | P04 | T059; T060 |
| R22 | Replan/lock livelock | High update rate / long LLM computation | Compute cost / poor UX | Narrow material dependencies, bounded retries/replans and escalation. | P04 | T061; T062; T063 |
| R23 | Draft lost by conflict/read-back | Typing during save, late callbacks, duplicate events | User work loss | Separate B/L/S/C, existing reconciler semantics and editor generations. | P02 | T030; T031; T032; T035; T092 |
| R24 | Missed notification bypass | Disconnect/reorder/drop | Stale acceptance or stale display | Final owner conditions independent from notification delivery. | P02 | T091 |
| R25 | Renderer/backend recoupling | Component extraction plus mutation redesign | Build/maintenance/performance coupling | Minimal data-only contract, actual evaluated graph and sandbox guards. | P03 | T002; T044; T070 |
| R26 | Full-surface validation overhead | Many objects/owners | CPU/DB/payload growth | Use existing bounded typed GetMany/fact reads; optimize complete-set queries. | P01 | T003; T063; T088 |
| R27 | Existing domain guarantees weakened | Mechanical rollout | Invalid task/authority transitions | Owner-specific adoption and retained positive/negative regression cases. | P05 | T066; T080; T083 |
| R28 | File/remote conditional guarantee overclaimed | External editor/provider lacking CAS | Lost bytes / duplicate external effect | Driver capability matrix, immutable staging, honest uncertainty/recovery. | P06 | T072; T073; T074; T077 |
| R29 | Compensation erases later work | Recovery after another actor change | Data loss | Narrow inverse with original effect/lifetime and current safety predicates. | P06 | T075 |
| R30 | Shared connection concurrent use | Task.WhenAll or deferred callbacks | Failures / broken transaction boundary | Await bounded owner participants and enforce retirement/profile guards. | P01 | T014; T018; T067 |
| R31 | Old binaries re-enable unsafe writes | Cutover, rollback or retained workers | Data loss / invalid authority | Drain/refuse incompatible writers; compatible rollback build or forward repair. | P02 | T085; T086 |
| R32 | Testing false confidence | Fast partial validation or unavailable tools | Unproven release | Exact discovery/current binaries, PG/two-process/tool/browser lanes and honest missing evidence. | P00 | T001; T013; T043; T064; T087 |
| R33 | Cross-owner scope explosion | Overgeneralized basis/receipt framework | Coupling / latency / deadlocks | One governed pilot, explicit owner ports, bounded semantic dependencies. | P04 | T063; T070; T088 |
| R34 | Guidance/branch/migration drift | Concurrent refactoring | Broken integration / incorrect plan | Read-only refresh plus semantic caller review and stable logical IDs each phase. | P00 | T002; T090 |
| R35 | Telemetry leaks or explodes cardinality | Error handling and heavy concurrent workloads | Privacy / observability cost | Safe bounded labels, authorized trace correlation, minimal retained details. | P08 | T069; T089 |
| R36 | Unsafe no-op/partial-batch shortcut | Optimization under contention/large payload | Invalid approval / partial effect | Validate original semantic conditions, explicit no-op and staged-batch contracts. | P03 | T051; T052; T063; T079; T094 |
