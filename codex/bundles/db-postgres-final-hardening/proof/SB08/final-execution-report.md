# Final execution report

## Summary

Implemented lease-guarded finalization for process outbox, connector outbox, and automation delivery dispatch. Added lease-loss telemetry/audit paths, bounded throughput defaults, startup option validation, diagnostic benchmark proof, and canonical database architecture documentation/tests.

## Fulfillment matrix

| Subbundle | Status | Evidence |
|---|---|---|
| SB01 | Complete | Residue audit and bundle validator pass. |
| SB02 | Complete | Stale worker negative tests pass for process, connector, and automation delivery. |
| SB03 | Complete | Lease-loss source audit and persisted audit/telemetry checks pass. |
| SB04 | Complete | Options source audit and focused option tests pass. |
| SB05 | Complete with diagnostic caveat | Timing benchmark passes; no SQL roundtrip counter added. |
| SB06 | Complete | Claim-first source proof captured. |
| SB07 | Complete | Architecture, runtime, and component profile tests pass. |
| SB08 | Complete with caveats | Build/unit/EF/residue pass; broad component/integration caveats are named. |

## Leased finalization proof

Process outbox, connector outbox, and automation delivery finalization now perform guarded updates using the row id plus the current lease or lock token and an unexpired lease window. Attempts and completion/deadletter/retry audit rows are committed only after the guarded canonical update wins.

## Lease-loss proof

Process heartbeat renewal failure stops finalization. Connector processing writes `LeaseLost` audit. Automation delivery writes `DeliveryLeaseLost` telemetry. Focused stale-worker tests verify that an old worker cannot complete after another worker steals the lease.

## Throughput defaults proof

Automation message dispatch and connector outbox default to max parallelism `4`. Process outbox defaults to batch size `20` and max parallelism `2` in web configuration. Options include range validation and startup validation.

## Benchmark report

See `../SB05/benchmark-report.md`. Automation dispatch improved from 746 ms single-worker to 205 ms with four workers in the diagnostic run. Connector outbox completed under bounded parallelism, but the small sample did not show a strong timing delta.

## Canonical runtime proof

`DatabaseCanonicalityArchitectureTests` scans production source and restricts profile-specific context factory usage to approved maintenance/bootstrap/transfer/admin boundaries. Existing runtime and component tests prove database activation remains pending-restart rather than live hot-switch.

## Broad validation status

Passed:
- Solution build.
- Full unit suite: 789 tests.
- Focused lease/options integration tests: 5 tests.
- Benchmark diagnostic: 1 test.
- EF pending model check.
- Bundle validator and residue audits.

Caveats:
- Full component suite timed out at 30 minutes. Hang diagnostic stopped after 198 passed tests and named `CanDoItAll.Tests.Components.ProcessWorkspaceTests.Steps_canvas_node_moves_update_role_and_branch_positions_in_editor_state` as the active test.
- Full integration suite timed out at 30 minutes under diagnostics after making progress. It exposed database transfer failures caused by tests bootstrapping the canonical runtime through the auto-provisioned `postgres/postgres` profile in this local PostgreSQL environment.
- `git fetch origin` failed because SSH authentication is unavailable, so branch freshness is proven only against the local `origin/development` ref.

## Merge recommendation

Do not merge solely on broad-suite status until the two named validation blockers are either fixed or formally quarantined by the owning test areas. The hardening changes themselves have focused proof and unit/build/EF validation.
