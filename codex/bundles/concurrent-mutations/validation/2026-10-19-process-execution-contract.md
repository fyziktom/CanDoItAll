# Process execution contract admission validation

Status: the [versioned admission design](../plan/P07-process-execution-contract.md)
is implemented with 114 distinct selected passing behaviours across the exact test-only
repair bridge below. This is not Process/Workflow execution activation or complete R5.

## Behaviour and boundary

Schema 1 retains its exact bytes and excludes the execution-contract field. Schema 2
requires an immutable opaque contract ID, positive contract version and Agent/Workflow
owner. Strict decoding rejects missing, null, duplicate, unknown and invalid fields;
schema downgrade cannot discard the obligation. The adapter-facing exact-match check
refuses a missing or different ID/version/owner. A declaration is not a capability grant.

Existing prepared-launch hashing and assignment storage bind the contract without a
new column, index, migration, registry or database call. Repair cannot add, remove or
replace it. Existing executors and persisted source-authority guards still refuse these
policies before provider/effect work. No business-module or SDK dependency was introduced
into Runtime. Inherited material, actual child journals and effect/manager integration
remain required before a producer can activate the contract.

## Builds, discovery and results

Direct Release builds passed for Runtime, the Process module, Web and the owning Unit
and Integration projects. Standard outputs were refreshed serially. All discovery used
the refreshed assemblies with `--configuration Release --no-build --no-restore`, local
libraries and `/m:1`; the existing isolated PostgreSQL18 fixture supplied database tests.

| Lane/filter | Expected/discovered | Latest result |
| --- | --- | --- |
| Unit: `FullyQualifiedName~ProcessDecisionDataPolicyAdmissionTests\|FullyQualifiedName~ProcessDecisionExecutionContractTests\|FullyQualifiedName~WorkflowProcessStepExecutorTests\|FullyQualifiedName~ExecuteAsync_rejects_invalid_contract_or_unsupported_policy_before_workflow_launch` | 60/60 | 60 passed |
| Integration: `FullyQualifiedName~ProcessPreparedLaunchPersistenceTests\|FullyQualifiedName~ProcessDecisionPolicyMigrationTests` | 34/34 | 34 passed |
| Integration: `FullyQualifiedName~ProcessExecutionAuthorityPersistenceTests` | 20/20 | 20 passed |

The 22 new protocol cases cover exact legacy encoding, both owners, strict versioned
fields, exact adapter matching and launch-fingerprint substitution. Existing executable
guards gained schema 2 cases. PostgreSQL proof includes preparation/commit/reload,
immutable repair, the existing real-host restart lane, migration/downgrade safety and
both initially admitted and later substituted policies at reservation/effect guards.

The initial candidate ran all 114 cases: 112 passed and two new Unit assertions failed
because they expected `InvalidDataException` rather than the existing dedicated
`ProcessLaunchIntentConflictException`. Production correctly refused the changed intent.
After complete identity/resource verification and freeze release, only that test file
changed. Its assembly was rebuilt, all 60 Unit cases were rediscovered and passed.

`production-bridge.json` compares every source and runtime identity: only
`ProcessDecisionExecutionContractTests.cs` and Unit's DLL/PDB changed. All production,
Integration and shared-test inputs/binaries are identical. The 54 earlier PostgreSQL
passes therefore remain exact evidence. The original failure is retained. An initial
secret-scanner test command used a nonexistent filename; the corrected documented
command passed, and the failed invocation remains in the first proof.

Full portability enforcement passed at 15,623 findings with no added/stale delta and
no baseline rewrite, including a regenerated scan after the test repair. Baseline,
secret-scanner and rendering tool tests, rendering register, documentation and diff
checks passed. No broad Stable, browser, throughput or complete E2E result is claimed.

## Architecture, identity and resources

CodeAnalytics `snap-20261019195351-d1ce2c09` covers the same four Process projects:
1,009 types, 9,044 members, 27 entities and 328 findings. No detected cycles or added/
removed findings. The existing 190 load diagnostics and one AddSingleton collector
ambiguity remain; direct builds and real owner behaviour supplement that incomplete view.
The gate is **Pass with follow-up** for this foundation; actual adapter activation remains
gated. No new runtime partial, interface, project reference or service-location appeared.

| Candidate | Source SHA256 | Runtime SHA256 |
| --- | --- | --- |
| `c4-process-execution-contract` | `B777A917B1F7B093466D840ED3E4C965B587C3D91D96597D5F0F321E2D2BAE2B` | `39D49F47D8C2ABB7C5F932E70AD6E4CB93E0B21CD104BC0F3C0C2C8BAA1C9683` |
| `c4-process-execution-contract-test-repair` | `E73570F5216CCAAAE4C5CDFC09D08E603503FA8100F2EAB26B9BC63CD6305E80` | `29123B1CB05EE3E0DE87460A10541792BDCF3937B3133EE6D5FC1FA95ABDA571` |

Both complete source/runtime identities were verified before their explicit freeze
release. Observers covered every execution command in full (237.6441491s and
11.6283026s). Reconciliation found no remaining observed process or database-name delta.
Eleven unattributed TEMP entries from the first candidate were preserved; the repair
found none. Earlier denied cleanup was not retried. The shared fixture remains needed
for continuing implementation, so this is not complete bundle cleanup.

Compact proofs are retained under `proof/2026-10-19-planning-authority`:

- `c4-process-execution-contract.json`: SHA256 `AD5FB650E2B8AF378D950F116E07789CC52265B4527663CE611CD3A39C4ABC35`.
- `c4-process-execution-contract-test-repair.json`: SHA256 `AAA25CFF6126B01096FA92B97E00BA406292849E395CBA6D767BBC96200AFFEB`.

Signing is still awaiting the fresh local Pinentry request after the stale request was
cancelled. This checkpoint retains exact Git blobs without altering the staged Agent
recovery checkpoint. Commit order remains Agent recovery, pure generations, durable
generations, child publication, then this contract foundation. SPA remains clean on
its own `codex/concurrent-mutations` branch at `70c65dd`; no merge or push occurred.
