# SB08 Artifact Review

## Query plans

Reviewed `bundle://proof/SB05/query-plans/`:

- `process-outbox-claim.txt`
- `automation-delivery-claim.txt`
- `connector-command-claim.txt`
- `process-step-dispatch-header.txt`

The outbox, automation delivery, and connector command plans use PostgreSQL claim indexes for pending/due claim order. The process step dispatch header path uses the existing process-run/sequence index; SB05 removed the extra candidate partial index after plan proof showed it was redundant.

## Benchmark

Reviewed `bundle://proof/SB06/benchmark-output.json`:

- Process outbox: 64.264 records/s sequential, 419.581 records/s bounded parallel.
- Automation delivery: 64.359 records/s sequential, 434.896 records/s bounded parallel.
- Connector command: 64.307 records/s sequential, 411.112 records/s bounded parallel.
- All workloads processed 768 of 768 seeded records in both modes.
- Runtime metrics observed claim, process, stale-finalization, duplicate-suppression, and batch-duration instruments.

## Broad-suite classification artifacts

- `bundle://proof/SB08/component-suite-diff-audit.log`: component test projects are untouched by this hardening bundle.
- `bundle://proof/SB08/integration-failure-scope-diff-audit.log`: broad integration failure files are untouched by this hardening bundle.
- `bundle://proof/SB08/integration-failed-tests-after-postgres-role-fix.log`: the initial environment-only PostgreSQL auth failures were repaired locally and the same tests now expose non-hardening runtime-switching assumptions.
