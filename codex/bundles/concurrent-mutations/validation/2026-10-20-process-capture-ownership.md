# Process capture ownership validation

Status: PASS for capture commit observation and dispatch attachment, with 127 distinct
passing behaviors under the exact test-only repair boundary. This is a prerequisite,
not Process/Workflow product activation or E2E closure.
Signed design: `e7f1cff4`; signed result/accounting prerequisite: `0ec44539d`.
[Accepted design](../plan/P07-process-capture-ownership.md).

## Requirement and implemented boundary

Owned requirement: concurrent users, agents and workflows must not duplicate work or
silently replace their original snapshot after a crash, retry or stale dispatch.

The Process unit of work now distinguishes a new successful commit invocation from
historical Applied receipts through the nonserialized CommittedNow observation.
Historical lookup, duplicate commit and pure mutation preparation never grant that
observation. Exact dispatch preconditions validate loaded original claim identity,
unchanged proposed claim authority, active root/runtime and the lease deadline before
staging and after the persisted-state reload. A typed failure rolls back flushed writes.

The production guard is a cohesive Persistence type; its contract is in Runtime.
The existing transaction owner delegates to it. No module reference, migration, runtime
partial, service locator, new provider or UI surface was added. The optional guard is
not yet a claim that the still-disabled producer supplies it. Its consumer integration
remains the next required vertical.

Shallow-pass trap: treating an original Applied outcome or a successful preliminary
read as permission to capture again; checking expiry only before awaiting the root lock;
returning a rejection after flushing material while still committing the transaction.
Negative proof must cover all three, including lost acknowledgement and post-flush
expiry with unchanged material, scope, budget and receipt. Positive proof uses the real
shared-scope reservation and material attachment, with two competing callers and one
fresh capture owner. This dependent smoke is not the eventual registered business producer.

## Candidate and selection

Owned directory: `.artifacts/concurrent-mutations/validation/c4-process-capture-ownership`;
its sidecar was recorded before creation. Reuse standard Release outputs and the
existing isolated PostgreSQL 18 fixture on 53619. Ordinary 5032 and its databases are
outside ownership. No new container, database server or compiled output root is created.

Build Runtime and Persistence directly, then Unit and Integration, serialized.
Expected discovery is 127:

| Assembly | Exact class topic | Expected |
| --- | --- | ---: |
| Unit | ProcessPersistenceStoreTests | 77 |
| Unit | ProcessRuntimeCommitObservationTests | 7 |
| Integration | ProcessDecisionBudgetPersistenceTests | 21 |
| Integration | ProcessRuntimeDispatchPreconditionTests | 22 |

Each lane uses `FullyQualifiedName~<namespace>.<class>.`, with the exact namespace,
arguments and listed names in the candidate manifest and discovery record.
New SQL cases cover live claim statuses, identity/attempt/lease mutation, current root
cancellation including a descendant, expired/replaced claims, waiting for a root lock,
historical and unknown-acknowledgement recovery, cross-step commands and rollback after
material flush. Existing accounting cases also assert fresh versus replay and retry
after rollback. Contract tests verify pure outcomes, omitted/ignored JSON and malformed
identities. No broad suite is inferred from these checks.

The initial frozen run executed all 127 cases: 104 passed, 23 failed. All 77 persistence
and 21 accounting regressions passed. One contract test wrongly assumed the default JSON
serializer could reconstruct the runtime's existing IReadOnlySet. All 22 new SQL cases
were stopped by the fixture's attempt to claim before initial shared capture, exposing
the design sequencing mistake. The corrected design keeps shared capture before claims;
dispatch attachment cases use StepSnapshot, and a separate real shared capture flow
proves fresh ownership and the existing prohibition on early claims.

Initial proof: `c4-process-capture-ownership.json`, SHA-256
`40672A4029EAE26ED6A19022271E1B7E98B20BB9BB959C61DE75C784E2F3F16F`,
218134 bytes, zero secret findings. Frozen source/runtime verification, all four command
observations and portability enforcement pass. Reconciliation found zero remaining
observed processes/database-name delta and preserved fourteen unowned TEMP entries.
No production code changes are needed for this correction. The successor repeats all
seven contract and 22 new SQL cases, retaining 98 unaffected passing regressions only
after checking the complete source comparison.

## Gates

The repaired candidate passes all 29 discovered cases: seven contract and 22 SQL.
The exact complete-source comparison identifies only the two test files and the design
and validation documents. All 1725 production outputs are identical. The 98 disjoint
persistence/accounting regression passes therefore remain valid: 127 distinct behaviors.

Final proof: `c4-process-capture-ownership-repaired.json`, SHA-256
`35A9FA38821F8A8E2A88630587E74DCA2E932F999EEB88199C2102BA660E31C9`,
56024 bytes, zero secret findings. Final source identity: 9745 files,
`A028620508A1BB0C071CDB77C0B260938955925820DAFF54A0C8CF18E8CF4EF8`.
Final runtime identity: 3976 files,
`B3FBB0603D284B03D41389AA6657C5E41FBB7CAD4AF4A241B09B577BB4695A66`.
Both remained unchanged through execution.

Runtime and Persistence direct Release builds passed on the unchanged production
candidate. Unit and Integration Release builds passed before both runs. Exact discovery,
rendering-register, documentation and diff checks pass. Full portability enforcement
passes with 15651 unchanged findings and no baseline rewrite.

Architecture snapshot `snap-20261020215316-5874c340` has 1108 types, 9647 members,
352 findings and the same 190 existing workspace diagnostics. The six project edges
are unchanged, with zero scoped cycles and no added findings. The dashboard was inspected.
The transaction owner gains delegation and the commit observation; claim validation is
in the cohesive guard. Root calls add no SQL read, descendants add one indexed scalar
root-status lookup. No sustained-capacity or healthy whole-solution claim is made.

The final 133.444-second observation covers both commands. Reconciliation found zero
remaining observed processes and zero database-name delta; eight unowned TEMP entries
were preserved. The initial failed candidate remains retained. Historical cleanup
denials remain unchanged and were not retried. Shared PG18 remains needed for producer
integration; the validation directories retain reviewed proof inputs and reusable helpers.

The historical broad Stable run remains incomplete (16199 executed, 16182 passed,
17 failed, 2444 unexecuted). Actual producer/manager paths, Workflow, settings/SPA,
sustained capacity and final broad closure remain open. SPA stays on its own
`codex/concurrent-mutations` branch without a main merge.
