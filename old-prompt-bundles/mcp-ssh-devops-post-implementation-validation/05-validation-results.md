# Validation Results

Validation date: 2026-03-21

Target:

- `rpi3-test`
- Host: `10.190.32.143`

Final outcome:

- The repaired `CanDoItAll.Mcp.SshOps` implementation passed the full remote validation runner end to end on the Raspberry Pi.
- A focused detached-job diagnostic also passed after the runner launch/cancel fixes.

Commands used:

```powershell
$env:CANDOITALL_RPI3_TEST_SSH_PASSWORD='root'
dotnet output/remote-validation-runner/RemoteValidationRunner.dll
dotnet output/remote-job-diagnostic/RemoteJobDiagnostic.dll
```

Highlights:

- Compose tools now work on a host that only has `docker-compose 1.25.5`.
- `compose_apply.postWaitPolicy` is now enforced and was validated live by waiting for the PostgreSQL service to become healthy.
- `compose_logs` correctly degrades behavior for legacy compose by ignoring unsupported `--since` and warning.
- `stack_rollback(strategy="last-known-good")` restores a working stack after a deliberate bad compose change.
- Detached operations now keep a stable PID, report `running`, and transition to `cancelled` through `operation_cancel`.
- Detached operations now preserve explicit `timedout` state when the wrapped command exits with timeout code `124`.
- Root-owned-path support is materially improved through sudo-aware directory creation and upload fallback.

Validated tools:

- `targets_list`
- `target_test`
- `target_audit`
- `host_bootstrap_prepare`
- `fs_apply_bundle`
- `fs_read_text`
- `fs_backup_path`
- `fs_restore_backup`
- `docker_network_ensure`
- `docker_volume_ensure`
- `compose_validate`
- `compose_apply`
- `compose_ps`
- `compose_logs`
- `compose_exec`
- `compose_down`
- `stack_rollback`
- `http_probe`
- `http_wait`
- `cert_check`
- `postgres_ready`
- `ipfs_status`
- `ipfs_private_validate`
- `operation_status`
- `operation_wait`
- `operation_logs`
- `operation_cancel`
- `dangerous_raw_exec`

Known remaining follow-up:

- Shared `FileLogStore` integration is still absent from runtime flow.
- Some config knobs still need behavior-level wiring.
- Path-guarding is still lexical and does not resolve remote symlinks.
