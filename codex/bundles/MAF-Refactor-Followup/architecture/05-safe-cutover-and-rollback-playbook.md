# Safe cutover and rollback playbook

## General rule

Never run old and new side-effecting paths in parallel. Shadow comparison is allowed only for pure projection, hashing, compatibility decisions, or authorization decisions where neither branch can mutate state.

## Authority cutover

1. Add failing tests and the new governance snapshot.
2. Feed snapshot to capability planner in shadow **decision comparison** mode only.
3. Log only decision category/fingerprint differences, never raw policy inputs.
4. Make new snapshot authoritative behind a bounded selector for tests/dev.
5. Run negative cross-project and read-only mutation cases.
6. Remove old grant derivation after checkpoint pass.

Rollback: restore selector to old path only before deleting old code; do not preserve dual grants.

## Workspace scope/lifetime cutover

1. Create full identity and owned aggregate.
2. Adapt one construction root.
3. Verify instance/disposal counts.
4. Move recovery/script helpers.
5. Move all profile-workspace construction.
6. Remove extra process host and unmanaged bundle.

Rollback: dispose the new aggregate and switch the construction root atomically. Never share processes between old/new hosts.

## State envelope cutover

1. Add v0/v1 fixtures.
2. Implement strict readers and v2 writer.
3. Read v0/v1/v2, write v2 only.
4. Verify approval continuation across restart.
5. Retain readers until operator removal criteria are met.

Rollback: continue reading v2; reverting to a v1-only binary after v2 writes is prohibited unless a downgrade migration exists.

## Lightweight LLM cutover

1. Harden contracts behind the same port.
2. Add reliability/failure pipeline.
3. Move neutral invoker/registration without changing workflow business semantics.
4. Compare pure request/usage mappings.
5. Delete MAF project ownership only after workflow tests pass.
