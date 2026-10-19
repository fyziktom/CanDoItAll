# Canonical Process material persistence

Gate: **Pass with follow-up**, 2026-10-19. This storage checkpoint has 237 distinct
selected passing behaviours across an exact test-only repair bridge. It does not
activate Process/Workflow policy settings or close the E2E requirement.

## Implemented boundary

Immutable scope/material tables retain the original profile, namespace, real root/run
and optional step, executor contract, generation, UTC capture ticks and bounded UTF-8
payload. Capture ticks preserve identity below PostgreSQL timestamp precision. Payload
text preserves exact bytes; reads reconstruct and verify the original fingerprint.
Filtered unique scope indexes, material/generation uniqueness, byte checks and restrictive
foreign keys protect retained bindings. There is no newest-material query or implicit
current-head selection.

The existing runtime unit of work stages material, generation, capture settlement,
runtime state and receipt in its root-fenced transaction. A command carries a reference
and fingerprint; the separate payload is neither a runtime-state field nor receipt copy.
Replay compares original payload and metadata before returning the historical receipt.
Prepared launches reject runtime material/decision commands. Legacy opaque IDs are not
backfilled with invented facts.

Policy schema 3 explicitly declares canonical-material support; schemas 1/2 retain their
original encodings. Material-bearing receipts use schema 2 and legacy receipts retain
schema 1. Earlier admission/receipt readers reject the new versions. Migration
`20261019204112_AddProcessDecisionMaterials` refuses downgrade when material, new receipts
or schema 3 prepared/committed admissions remain, including before the first capture.

`IProcessDecisionMaterialReader` is a historical read boundary. It does not grant source
permission, select the active parent generation or authorize publication. Step-local
RunSnapshot refresh remains refused until a shared parent can reconcile and fence all
affected children.

## Proof and repair

| Selected lane | Discovered | Final passing |
| --- | ---: | ---: |
| Canonical material persistence | 36 | 36 |
| Existing generation, budget and publication regressions | 85 | 85 |
| Pure material/protocol/generation/child identity | 108 | 108 |
| Prepared admission and runtime-material exclusion | 8 | 8 |

The first frozen candidate executed all 237 cases: 234 passed and three tests failed.
Two assertions expected FK violation `23503` although PostgreSQL correctly reported
RESTRICT violation `23001`. The migration test runner passed generated SQL through EF's
composite formatter, which treated the literal fingerprint regex `{64}` as a placeholder.
The repair asserts `RestrictViolation` and executes generated SQL through an enlisted
`DbCommand`. Production code did not change.

The successor reran all 44 database cases which use that test/helper, all passing.
`Verify-RepairBridge.py` proves only `ProcessDecisionMaterialPersistenceTests.cs` and its
Integration DLL/PDB changed; every other source/runtime input is identical. It retains
the other 193 passing cases. This is 237 distinct behaviours, not 281.

Proof includes original-payload/timestamp/scope/contract substitution, strict protocol
versions, maximum-size payload, sub-microsecond capture precision, sibling reuse of one
run payload, restart after refresh with historical receipt replay, concurrent duplicate
commands, rollback after SQL flush, corrupt stored material/receipts, canonical profile
checks, legacy absence, SQL bounds/uniqueness/restrictive deletion, empty migration
round-trip and prepared/committed admission downgrade refusal.

Direct standard Release builds passed for Runtime, Application, Persistence, Process
module, Migrations and Web, plus Integration and Unit. EF reports no pending model
changes. Full portability enforcement passes with **15,627** reviewed findings. Its four
new entries are generated migration metadata for existing RootPathState/RootPathSyntax
and RaisesEscalation/EscalationCount properties; no executable host path or process launch
was added. No stale findings remain. Rendering, documentation, diff and validation-tool
checks passed. Existing package advisory warnings remain.

CodeAnalytics `snap-20261019205459-d1ce2c09`: four projects, 1,022 types, 9,121 members,
29 entities, 332 findings, 190 existing load diagnostics, one existing AddSingleton
collector ambiguity and zero dependency cycles. The two new informational member-count
findings describe the nine-column immutable scope entity and cohesive material store.
Both were reviewed; no new project reference or runtime partial was added.

## Identity and resource closure

| Candidate | Source SHA256 | Runtime SHA256 |
| --- | --- | --- |
| `c4-process-material-persistence` | `5A7696FCD9E1EFAD70BEF1F533FA3616A95988C1054DFBEA6A2E587A18CEDDA9` | `7CF3377E6B77FCD8936DE5B3BEA9B341805E4A67363875419D9054EAF6380F6D` |
| `c4-process-material-persistence-test-repair` | `7B7EEEBF505E68D760722D566958CB61BBF21AA63D04DB2EAC68C70444600359` | `F5747AA2781EE299753A1554680D8A571191EC978DC381382DF6993109B66F78` |

Both 9,475-source/6,065-runtime identities were verified before release. Observers covered
every complete test command for 487.6393983 and 181.1901893 seconds respectively. Neither
candidate left an observed process or database-name delta. Fifteen and eight unattributed
TEMP entries were preserved; that is not proof of ownership or permission to delete them.
Earlier cleanup denials remain unchanged. The existing PostgreSQL18 fixture is still
needed for continuing work; no replacement stack was created.

Local retained proof under `proof/2026-10-19-planning-authority`:

- `c4-process-material-persistence.json`: SHA256 `6317EAEA42C4FB6B61B8188BEEDA82B857AEE61C311FB31B2CF65385977B97FD`.
- `c4-process-material-persistence-test-repair.json`: SHA256 `1393E36E722A55725D81822B61DBA7E6892C3135D494065352AC59C9E28ED67D`.

## Remaining integration

The mixed-executor review found that the current material contract includes an executor
owner. That deliberately refuses a different executor, but cannot yet express one shared
RunSnapshot consumed by both Agent and Workflow steps. Separate the business content
schema from dispatch capability before activating those consumers; do not weaken exact
executor admission or relabel original material. Preserve historical protocol meaning.

The active shared scope, genuine parent budget/funding, nested scope membership, actual
child journal/manager continuation and real settings producer remain open. Indexed query
shape and per-record bounds are not workload latency/capacity qualification. Existing
broad Stable failures and full bundle cleanup remain explicit. SPA stays on its isolated
branch and is not merged or pushed.
