# Inherited task-plan receipt identity

Status: implemented and validated. The parent saved-effect checkpoint is staged in a
separate signed commit waiting for the local Pinentry credential; this slice is not yet
staged. Backend stays on `concurrent-mutations`; SPA stays on `codex/concurrent-mutations`
without merge or push. Update signature state after Git completes.

The [design](../plan/P07-inherited-task-receipt-identity.md) adds explicit Process-Agent
caller/source kind 2. Inherited sources require active-work version 2; ordinary sources
retain versions 1 and 2. Caller binding, operation keys and the receipt primary key already
contain caller kind, so their existing format stays unchanged. Exact caller/source pairing
is now required before lookup/publication, receipt creation and receipt decoding. No SQL
query, lock, cache, service registration or project reference is added to the request path.
The identity itself grants no publication, permission or work-budget authority.

Migration `20261020071655_AddInheritedTaskPlanReceiptIdentity` changes only the caller
and interpretation check constraints. Existing rows, columns, indexes and primary key
remain unchanged. New kind 2 must use matching source kind and schema/result/source
version 2 with normalization 1. The old kind retains its SQL contract. Upgrade runs in
one transaction and requires the normal schema lock/constraint-validation window. Down
takes an exclusive table lock and explicitly refuses retained inherited receipts; it
does not delete or reclassify evidence. Empty/legacy-only downgrade restores both checks.
The generated idempotent SQL was reviewed and is retained in the proof.

## Validation

Direct standard Release builds pass: Workbench 18.06715 seconds, Migrations 40.4215614,
Unit 50.3860117 and Integration 42.7915394. EF `has-pending-model-changes` reports none.
Built discovery exactly matches execution with refreshed standard outputs:

| Filter | Discovered | Passed | Failed | Seconds |
| --- | ---: | ---: | ---: | ---: |
| `FullyQualifiedName~ProjectTaskPlanOperationContractTests\|FullyQualifiedName~ProjectTaskPlanAgentCodecTests` | 34 | 34 | 0 | 3.8423095 |
| `FullyQualifiedName~ProjectTaskPlanOperationTests\|FullyQualifiedName~ProjectTaskPlanActiveWorkTests\|FullyQualifiedName~ProjectTaskPlanAgentIntegrationTests\|FullyQualifiedName~InheritedTaskPlanReceiptMigrationTests` | 41 | 41 | 0 | 242.8574831 |

All 75 cases pass, including 16 new cases. Fixed legacy caller binding and operation key
remain exact. Same trusted key/operation ID across origins produces separate SQL receipts;
inherited replay retains exact accepted bytes after write revocation without spending work.
Cross-origin publication is refused before SQL or authority acquisition, and kind 2 without
active work is refused without effects. Migration tests cover fresh/legacy upgrades,
restart, canonical/owner model correspondence, unchanged rows and schema, enforced
kind/version checks, legacy downgrade and rollback of downgrade refusal. Migration seeds
exercise opaque receipt-byte preservation; the codec and actual publisher cases separately
prove semantic receipt decoding and replay. Existing interactive dispatch and failure
recovery tests pass. This is not actual Process producer or inherited Workbench adapter proof.

Full portability enforcement passes with 15,646 reviewed findings. Four generated target
model entries (`RootPathState`, `RootPathSyntax`, `RaisesEscalation`, `EscalationCount`) are
unchanged EF metadata copied into the new migration. The initial enforcement failure,
reviewed baseline diff and final no-write pass are retained. Baseline/secret-tool tests,
rendering, docs and diff checks pass. Existing broad Stable failures remain open.

## Architecture and resources

Architecture gate: Pass with follow-up. Scoped snapshots
`snap-20261020071229-9f0bb7eb` and `snap-20261020072108-9f0bb7eb` cover Workbench and
Migrations: 1195 to 1196 types, 10130 to 10137 members, 106 registrations unchanged,
458 to 459 findings. The only new finding is the standard 10,921-line generated EF
target model; two existing source-size/location findings change. No project cycle is
introduced. One existing module cycle and five existing type cycles are unchanged.
The 204 existing analysis diagnostics remain; direct builds and migration model checks
provide the compile/model proof. No runtime partial was added; generated EF partials
retain their tooling role. The Workbench/Process adapter and activation remain open.

Root `.artifacts/concurrent-mutations/validation/c4-inherited-task-receipt-identity`
is Released. Frozen identities verified unchanged: 9609 source inputs, manifest SHA-256
`4D5F09D1CFA0CDCAFD8F2882C3F5B1F69425579D98C6FD0A68EDAA0B9DA38E30`;
3700 runtime files, manifest SHA-256
`278B74C3A72363EAB82ABA6612DED32237B39D217C50FB5A397DE30AC0F86ABF`.
Observer startup initially refused an old baseline pin after its reviewed refresh; only
that pin and companion manifest identity were corrected before any test began. The exact
observer identity and full 264.3324453-second command coverage pass. No observed process
or database-name delta remains. Six unattributed TEMP entries are preserved; the shared
PostgreSQL 18 fixture remains needed. Ordinary 5032, installed tooling and historical
cleanup denials are preserved. Current reconciliation does not close historical cleanup.

Retained UTF-8/LF proof:
`proof/2026-10-19-planning-authority/c4-inherited-task-receipt-identity.json`, SHA-256
`553EFE1282809620F9B5728CBA92F5EEA1763F0AE8B1F916389A48E10E0641FC`.
It contains exact commands/results, migration SQL review, architecture, frozen identities,
static review, observer startup repair, coverage and resource reconciliation. Pre-commit
secret review covers this exact proof and the private fixture sentinel without echoing it.
