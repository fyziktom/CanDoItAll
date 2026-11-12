# 21 · Proposed test catalogue

**96 behavior specifications. All are proposed and not executed against the application.** The JSON register also contains the exact intended setup/interleaving and blank fields for actual symbol, command, count and result. Use this list for impact-driven discovery, not as a passing test report.

## P00 · Evidence refresh, writer perimeter and cost baseline

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T001 | audit: All enabled Projects writers are mapped | No unresolved writer in the pilot perimeter; paths and symbols recorded. | INV-01, INV-18, INV-21 |
| T002 | architecture: Decoupled contract graph | Renderer/contracts have no persistence or concrete-owner dependency. | INV-19, INV-21 |
| T003 | baseline: Matched cost baseline | Measured costs and missing measurements explicit; no fabricated speedup. | INV-15, INV-21 |

## P01 · Minimal foundation proof and prerequisite repairs

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T004 | postgres: Original token survives server reload | B remains; old body cannot use freshly loaded token. | INV-02, INV-05 |
| T005 | postgres: Child-only stamp advancement | Root edit stamp advances with child commit and old editor conflicts. | INV-03, INV-18 |
| T006 | postgres: Coherent aggregate read | Returned stamp/data form one valid snapshot, not mixed observations. | INV-04 |
| T007 | postgres: Conditional failure rolls back children | No child/receipt/projection effect commits. | INV-03, INV-05, INV-12 |
| T008 | fault: Commit succeeds; observer fails | Committed identity/stamp preserved; no retry of primary effect. | INV-11, INV-17 |
| T009 | fault: Commit acknowledgement lost | Unconfirmed observation resolves by exact operation; no assumed rollback. | INV-11, INV-12 |
| T010 | fault: Dispose failure releases gates | Owned process gates release; original/cleanup errors remain distinguishable. | INV-17 |
| T011 | fault: Source lease finalization after commit | No relabelling as rejected; cleanup problem visible with committed evidence. | INV-11, INV-17 |
| T012 | concurrency: Source-first nested ordering | No cyclic acquisition/self-deadlock; existing authority precedence preserved. | INV-07, INV-16 |
| T013 | concurrency: Two-process ordinary and governed paths | Same declared invariants without relying on process semaphores. | INV-05, INV-16 |
| T014 | postgres: Retired enlisted scope | Deterministic refusal before owner data effect. | INV-06, INV-17 |
| T015 | policy: Retry taxonomy | Only safe technical transaction retries reuse original operation/basis. | INV-09, INV-11 |
| T016 | postgres: Bulk update predicate | Explicit condition and affected rows; aggregate token participates. | INV-02, INV-18 |
| T017 | concurrency: Cancellation while acquiring multiple gates | All acquired resources released, no operation effect. | INV-15, INV-17 |
| T018 | architecture: No parallel enlisted commands | Bounded awaited sequential participation, no Task.WhenAll database commands. | INV-16, INV-19 |
| T093 | concurrency: Dynamic guard expansion | Abort/replan or explicit bounded rejection; no unsafe late lock-order inversion. | INV-15, INV-16 |

## P02 · Projects end-to-end protected edit pilot

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T019 | postgres: Two editors phase versus name | Second conflicts; phase persists; no silent overwrite. | INV-02, INV-03 |
| T020 | tool-roundtrip: Actual agent versus human update | Owner rejects stale target; tool receives structured conflict. | INV-02, INV-07, INV-09 |
| T021 | http: Missing original stamp | Known precondition refusal, not null bypass or fresh-stamp refill. | INV-18, INV-25 |
| T022 | http: Conflicting header and body | Deterministic documented rejection; no weaker alternate path. | INV-02, INV-18 |
| T023 | postgres: Delete and recreate same public ID | New project untouched; lifetime-specific failure. | INV-06 |
| T024 | postgres: Concurrent duplicate creation | One exact creation/lifetime; both resolve same accepted result. | INV-12 |
| T025 | postgres: Duplicate after later edit | Return A receipt, not fresh-content conflict and not reapply A. | INV-11, INV-12 |
| T026 | policy: Same ID different intention | Semantic identity mismatch; no effect. | INV-12 |
| T027 | fault: Pending duplicate lookup | No new independent effect; protocol handles pending/not-visible outcome. | INV-11, INV-12 |
| T028 | security: Receipt disclosure revoked | No unauthorized result fields disclosed; effect is not replayed. | INV-07, INV-22 |
| T029 | postgres: Root-child-receipt atomic rollback | Either all declared records commit or none; no lying success receipt. | INV-03, INV-05, INV-12 |
| T030 | ui-session: Conflict keeps draft/EditContext | Draft and validation preserved; current state displayed separately. | INV-13 |
| T031 | ui-session: Typing during save | Compare S vs L, retain new typing and adopt accepted untouched fields/stamp. | INV-13 |
| T032 | ui-session: A to B to A late callback | Old completion cannot alter A2, reopen dialog or reset busy state. | INV-13 |
| T033 | ui-session: Save button plus Enter | One admitted submission; successor editor is not blocked by retired lifetime. | INV-12, INV-13 |
| T034 | ui-session: Committed but read-back fails | Visible saved-with-warning; retry reads only. | INV-11, INV-13 |
| T035 | ui-session: Owner-generated child IDs | Rows retain correct identity; no delete/recreate duplication. | INV-03, INV-13 |
| T036 | postgres: Old projection runs after new save | Final search reflects current state or correct ordered cursor. | INV-14 |
| T037 | fault: Crash between source and projection work | Same-transaction rollback/commit or durable rebuild recovery; no lost required work. | INV-11, INV-14 |
| T038 | postgres: Delayed projection after recreate | No revival/overwrite of new incarnation. | INV-06, INV-14 |
| T039 | lifecycle: Import source stamps/history | New local live tokens; imported approvals/receipts/reservations inert. | INV-06, INV-18 |
| T040 | lifecycle: Destructive restore and future client | Activation namespace/fence rejects invalid old authority/replay. | INV-06, INV-25 |
| T041 | lifecycle: Ordinary restart preserves receipts | Original durable operation remains reconcilable; no gratuitous epoch reset. | INV-06, INV-12 |
| T042 | http: Explicit missing versus create read | No accidental rebind to blank create; documented compatibility behavior. | INV-06, INV-25 |
| T043 | browser: Two real editors end to end | UI and persisted data agree; original text retained and final new write conditional. | INV-02, INV-13, INV-21 |
| T044 | architecture: Renderer dependency and sandbox | No owner persistence/MAF/Web implementation imported. | INV-19, INV-21 |
| T045 | positive: Fresh saves/creates/authorized updates | Legitimate work succeeds; safety not implemented by rejecting everything. | INV-01, INV-05, INV-07 |
| T091 | concurrency: Missed notification cannot admit stale write | Original expected condition still rejects stale update. | INV-02, INV-23 |
| T092 | ui-session: Read-back newer than original commit | Original accepted receipt and later current snapshot are labelled and reconciled separately. | INV-11, INV-13 |
| T095 | http: Edit stamp versus dashboard ETag | Dedicated edit token contract is not falsely advertised as strong validator of full dashboard. | INV-04, INV-18 |
| T096 | lifecycle: Operation namespace across profiles | No false replay or disclosure across profiles/epochs. | INV-06, INV-12, INV-22 |

## P03 · Narrow intentions and selective editor reconciliation

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T046 | policy: Independent scalar three-way merge | Only permitted nonconflicting values merge; resulting invariants validated. | INV-13, INV-24 |
| T047 | concurrency: Third writer during conflict resolution | Resolved save conflicts again rather than forcing overwrite. | INV-02, INV-13 |
| T048 | policy: Cross-field invalid merge | Owner refuses invalid result despite nonoverlapping changed fields. | INV-05, INV-24 |
| T049 | postgres: Explicit add/remove/reorder | Stable identities and declared order; no implicit list deletion. | INV-02, INV-03 |
| T050 | security: Foreign/duplicate/unknown child ID | Deterministic safe rejection; no mutation or silent recreation. | INV-03, INV-18 |
| T051 | policy: Normalization/default rows/no-op | Documented stamp/UpdatedAt/activity/receipt behavior; no hidden extra rows. | INV-03, INV-24 |
| T052 | policy: Value A to B to A | Generation-sensitive policy detects obsolete observation; value-based exceptions explicit. | INV-02, INV-08, INV-24 |

## P04 · One governed decision-basis and approval pilot

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T053 | concurrency: Objective changes during reasoning | StaleDecisionBasis-style rejection, no task effects. | INV-08, INV-09 |
| T054 | concurrency: Membership phantom | Scope witness invalidates proposal even if original row tokens unchanged. | INV-05, INV-08 |
| T055 | security: Model omits mandatory input | Trusted runtime/owner refuses; model cannot choose an empty validation set. | INV-08, INV-18 |
| T056 | security: Refresh stamp under old approval | Binding mismatch or stale basis refusal; no effect. | INV-09, INV-10 |
| T057 | concurrency: Source edit during publication | Declared freshness holds through target commit, including snapshot established before a gate wait; no false snapshot-only guarantee. | INV-05, INV-08, INV-16 |
| T058 | security: Revocation before actual effect | Owner current-authority check rejects exact attempt. | INV-07 |
| T059 | concurrency: Retired worker ignores cancellation | Old generation rejected at owner despite active task. | INV-07, INV-09 |
| T060 | policy: Manual edit keeping automation vs pin | Future automation follows chosen mandate; no unconditional human/agent precedence. | INV-07, INV-09 |
| T061 | policy: Bounded semantic replans | Escalation after agreed limit; no infinite compute/token loop. | INV-09, INV-15 |
| T062 | policy: Unrelated updates do not invalidate | Basis remains valid unless those facts are actual declared dependencies. | INV-03, INV-08 |
| T063 | policy: Oversized basis and truncated input | Explicit bounded rejection/staged contract; never successful incomplete validation. | INV-08, INV-15, INV-18 |
| T064 | tool-roundtrip: Actual governed planning positive/negative | Fresh proposal succeeds, invalid proposal has no effects and structured recovery. | INV-07, INV-08, INV-10, INV-21 |
| T094 | policy: Equal target but stale approved basis | No-op shortcut cannot authorize obsolete proposal. | INV-08, INV-10, INV-24 |

## P05 · Bounded owner adoption waves and structural invariants

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T065 | postgres: Opposing graph moves/cycle | Final graph invariant holds under complete owner guard set. | INV-05, INV-16 |
| T066 | postgres: Task assignment/cost/execution preservation | Existing expected-state, assignment and pricing semantics not weakened. | INV-01, INV-05 |
| T067 | concurrency: Multi-owner batch rollback | Declared same-DB atomic set rolls back; no independent hidden commit. | INV-05, INV-19 |
| T068 | postgres: Conversation append versus definition edit | Append not lost through parent snapshot; own request dedupe/order preserved. | INV-01, INV-03, INV-12 |
| T069 | security: CRM conflict privacy | Safe conflict detail without rate/personal-data disclosure. | INV-07, INV-22 |
| T070 | architecture: Owner adoption graph guard | No global AppDbContext or foreign owner persistence access. | INV-01, INV-19 |
| T071 | lifecycle: Maintenance path participation | Maintenance provenance/conditions explicit; production bypass absent. | INV-18, INV-25 |

## P06 · Storage, external delivery and projection integration

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T072 | storage: External file edit between read/replace | Actual driver guarantee observed; unsupported strict CAS is explicit. | INV-20 |
| T073 | external: Provider accepts then timeout | Provider-specific identity resolves result; no unsafe resend. | INV-11, INV-12, INV-20 |
| T074 | external: Provider without dedupe/lookup | Manual/explicit recovery, no claim of automatic exactly-once effect. | INV-20 |
| T075 | concurrency: Stale compensation after human change | Narrow guarded compensation refuses destructive stale undo. | INV-05, INV-20 |
| T076 | fault: Outbox duplicate/reorder/poison | Latest valid projection preserved; bounded retries and unrelated work progress. | INV-14, INV-15 |
| T077 | security: Revocation overlapping external dispatch | Report actual supported ordering/uncertainty, not fictional rollback. | INV-07, INV-20 |
| T078 | lifecycle: Receipt retention versus checkpoint replay | Compact evidence or expiry refusal prevents duplicate effect. | INV-12, INV-22 |
| T079 | fault: Partial staged batch | Exact per-item outcomes, no atomic-success claim or whole-batch blind replay. | INV-11, INV-20 |

## P07 · Workflow and Process resume/publication integration

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T080 | runtime: Resume pinned definition after live edit | Pinned execution remains distinct; publication into current target separately checked. | INV-06, INV-08 |
| T081 | runtime: Legacy checkpoint missing required basis | Readable history or explicit readmission, never fabricated current basis. | INV-08, INV-10, INV-25 |
| T082 | runtime: Replay stored denial | No conversion into an executing effect or unauthorized disclosure. | INV-07, INV-11, INV-20 |
| T083 | runtime: Process claim transfer late write | Old attempt fenced with no effects; new valid attempt succeeds. | INV-07, INV-16 |
| T084 | browser: Real workflow/process operator journey | Owner effects and operator outcome agree; no approval/retry bypass. | INV-07, INV-09, INV-10, INV-21 |
| T085 | lifecycle: Saved protocol downgrade | Admission/version policy prevents unsafe downgrade. | INV-25 |

## P08 · Consolidation, measured optimization and release closure

| ID | Layer / scenario | Required assertion | Invariants |
|---|---|---|---|
| T086 | release: Mixed-version deployment rehearsal | Incompatible writer refused; safe pause/forward repair path. | INV-18, INV-25 |
| T087 | release: Cross-platform canonical gates | Actual pass/fail/skip evidence per platform, no inferred green. | INV-21 |
| T088 | load: Matched final performance comparison | Budgets/exception decisions supported by counts and distributions. | INV-15, INV-21 |
| T089 | observability: Metrics cardinality and secret redaction | Useful bounded metrics and safe logs; no secret-bearing payloads. | INV-22 |
| T090 | architecture: Complete protected perimeter closure | Zero undocumented enabled bypasses in advertised coverage. | INV-01, INV-18, INV-21, INV-25 |
