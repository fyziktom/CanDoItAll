# SB01 Rollback Consistency-Boundary Procedure

## Decision

Rollback is a three-component restore, not a package downgrade over live state:

1. PostgreSQL database `candoitall_development`;
2. `%LOCALAPPDATA%\CanDoItAll\workspace`;
3. `%LOCALAPPDATA%\CanDoItAll\control-plane`, including Data Protection keys.

The same quiesced timestamp, repository commit, package graph, and manifest must identify
all three. Restoring only one component can separate database records from artifacts,
checkpoint shadows, active-profile metadata, or encryption keys.

This is an operator procedure. SB01 documents it but does not execute a destructive
restore against the development instance.

## Preconditions

- Resolve and record the exact managed host/session that owns port `5032`.
- Announce a mutation freeze and stop scheduler/agent/workflow launches and approval
  responses.
- Wait for in-flight runs to terminalize or explicitly mark them interrupted.
- Use a secure backup root outside the application workspace.
- Never place a database password on the command line or in committed output. Use the
  operator's protected PostgreSQL credential mechanism.
- Verify the configured database/profile at runtime; do not assume a launch-setting
  password is the active credential.

## Resolve Exact Paths

Run read-only resolution before stopping the host:

```powershell
$rollbackWorkspaceRoot = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables('%LOCALAPPDATA%\CanDoItAll\workspace'))
$rollbackControlPlaneRoot = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables('%LOCALAPPDATA%\CanDoItAll\control-plane'))
$rollbackExpectedParent = [IO.Path]::GetFullPath(
    [Environment]::ExpandEnvironmentVariables('%LOCALAPPDATA%\CanDoItAll'))

if (-not $rollbackWorkspaceRoot.StartsWith($rollbackExpectedParent, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Workspace root resolved outside the expected CanDoItAll local-data root.'
}

if (-not $rollbackControlPlaneRoot.StartsWith($rollbackExpectedParent, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Control-plane root resolved outside the expected CanDoItAll local-data root.'
}
```

Record resolved absolute paths, existence, total bytes, and file counts. Confirm
`dataprotection-keys` is inside the resolved control-plane root.

## Capture Procedure

1. Quiesce traffic and stop the identified managed child through the shared watch manager.
2. Confirm no process is listening on `5032`.
3. Create a uniquely named secure backup directory, for example
   `maf-1.15-cutover-20260728T050501Z`. Do not reuse or overwrite an existing directory.
4. Run `pg_dump` in custom format for `candoitall_development`, using protected credential
   input. Capture `pg_dump --version`, server version, exit code, dump byte count, and
   SHA-256. Do not log the connection string or environment secret.
5. Copy the resolved workspace root to a `workspace` child in the backup directory.
6. Copy the resolved control-plane root to a `control-plane` child in the same backup
   directory, preserving `dataprotection-keys`, profile catalog, and active-profile state.
7. Generate sorted relative-path/length/SHA-256 manifests for both directory copies.
8. Record:
   - capture timestamp and mutation-free interval;
   - repository commit;
   - stable and preview MAF versions;
   - captured pre-upgrade package graphs;
   - database name and server version;
   - source/destination absolute roots;
   - file counts, byte counts, and manifest hashes;
   - pending-approval/session/checkpoint counts;
   - operator and retention policy.
9. Restart the pre-upgrade host and perform read-only health checks only if cutover is not
   immediate.

The filesystem copies must occur while the host is stopped. Copying live checkpoint or
artifact files beside a database dump does not create a coherent boundary.

## Rehearsal

Rehearse before production use:

1. Restore the dump into a new isolated PostgreSQL database name.
2. Restore workspace and control-plane copies into new isolated directories.
3. Configure an isolated host to use only those isolated targets and a non-5032 port.
4. Start the known 1.13 binaries/package graph.
5. Verify:
   - protected database profiles decrypt with the restored Data Protection keys;
   - sessions and provider conversation IDs load;
   - workspace artifacts and checkpoint shadows resolve;
   - legacy pending approvals are visible for audit/reissue but no mutation executes;
   - no production scheduler or external provider mutation is enabled.
6. Record restore commands, exit codes, hashes, HTTP health, and teardown disposition.

## Rollback Procedure

Rollback requires explicit operator approval because database replacement and directory
replacement are destructive:

1. Stop new mutation and approval traffic.
2. Stop the exact managed host and confirm port `5032` is free.
3. Capture the failed 1.15-written database/workspace/control-plane boundary into a
   separate forensic backup. Never overwrite the pre-cutover backup.
4. Restore the pre-cutover PostgreSQL dump.
5. Restore the matching workspace and control-plane directory copies as one maintenance
   operation.
6. Re-verify every restored hash/manifest before startup.
7. Deploy the known 1.13 binaries and recorded package graph.
8. Start the managed host, verify HTTP health, and validate read-only session/artifact
   access.
9. Reconcile runs created during the canary. Do not feed 1.15-created pending approvals to
   1.13 unless bidirectional compatibility is separately proven.
10. Re-enable traffic only after database, artifact, checkpoint, profile, and key
    consistency checks pass.

## Remote Provider Limitation

Remote provider conversation state cannot be included in a local rollback archive.
CanDoItAll preserves only opaque provider/runtime IDs. During canary and rollback:

- do not duplicate a local transcript into a provider-managed conversation;
- do not claim the provider can be rolled back to an earlier remote state;
- classify missing or rejected remote IDs explicitly and start a new conversation only
  through a deliberate recovery decision.

## Failure Conditions

Abort capture or restore if:

- any resolved root falls outside the expected CanDoItAll local-data parent;
- Data Protection keys are missing;
- `pg_dump`/restore exits nonzero;
- any manifest/hash differs;
- traffic was not quiesced;
- the three components have different capture identities;
- the isolated rehearsal cannot decrypt profiles or resolve persisted artifacts.
