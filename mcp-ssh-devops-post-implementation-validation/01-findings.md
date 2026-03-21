# Findings

Status date: 2026-03-21

Update after implementation and remote re-validation:

- Resolved and validated on `rpi3-test`: compose-command compatibility, failure-status mapping, `compose_exec` policy, elevated-path write hardening, bootstrap ownership, `target_audit` compose/network honesty, `compose_ps` service-state handling, remote HTTP probe duration, `ipfs_status` gateway semantics, `stack_rollback` last-known-good behavior, and detached-job start/cancel semantics.
- Addressed through a scripted harness: the repo now contains `RemoteValidationRunner` and `RemoteJobDiagnostic` under this folder.
- Remaining follow-up items after the repaired pass: `postWaitPolicy` is still advisory, `FileLogStore` is still not integrated into SshOps runtime flow, some config knobs remain only partially wired, and `RemotePathGuard` is still lexical rather than symlink-aware.

Method:

- Contract review of `CanDoItAll.Mcp.SshOps.CodexPack.v1.1.0`, especially `06-tool-contracts.md`, `12-validation-matrix.csv`, and shared-foundation guidance.
- Static audit of `src/CanDoItAll.Mcp.SshOps` and `src/CanDoItAll.Mcp.Core`.
- Live verification against target `rpi3-test`.

## P0 findings

### 1. Compose tools are not compatible with the actual Raspberry Pi target configuration

Proof:

- Live call on 2026-03-21: `compose_validate` against `/home/pi/candoitall/stacks/qa-validation/docker-compose.yml` returned `valid=false` with stderr starting `docker: 'compose' is not a docker command`.
- Live call on 2026-03-21: `dangerous_raw_exec` proved the host has `docker-compose version 1.25.5` but not the `docker compose` plugin.
- Live call on 2026-03-21: `compose_apply` failed with the same root cause and never reached the stack workflow.

Code evidence:

- All compose and PostgreSQL helpers rely only on `BuildComposeCommand(target, ...)` in `src/CanDoItAll.Mcp.SshOps/Coordination/TargetCoordinator.cs`.
- There is no fallback or capability detection in `TargetCoordinator.Compose.cs`.

Impact:

- `compose_validate`
- `compose_apply`
- `compose_ps`
- `compose_logs`
- `compose_exec`
- `compose_down`
- `stack_rollback`
- `postgres_ready`

### 2. Tool failures collapse to generic `status="failed"` instead of contract-specific statuses

Proof:

- Live call on 2026-03-21: `compose_apply` returned `error.code="ValidationFailed"` and `status="failed"` instead of a contract-level status such as `validation_error`, `compose_invalid`, or `policy_blocked`.
- Live call on 2026-03-21: path/policy/host-key/auth failures would all currently be surfaced through the same generic failure status path.

Code evidence:

- `src/CanDoItAll.Mcp.SshOps/Tools/SshOpsTools.cs` catches `ToolInvocationException` and always emits `status: "failed"`.

Impact:

- The public MCP contract is less actionable than the pack promises.
- Validation-matrix outcomes such as `path_not_allowed`, `target_locked`, `policy_blocked`, and `host_key_mismatch` are not distinguishable in the envelope status.

### 3. `compose_exec` has no centralized whitelist / denylist policy

Proof:

- The pack explicitly requires a safe structured command policy for `compose_exec`.
- Current implementation accepts any argument array and forwards it to Docker.

Code evidence:

- `src/CanDoItAll.Mcp.SshOps/Coordination/TargetCoordinator.Compose.cs` only checks `command.Length > 0` and `AllowComposeExec`.
- There is no command policy helper anywhere in `src/CanDoItAll.Mcp.SshOps`.

Impact:

- A client can request shell interpreters or other sensitive commands through `compose_exec`.
- This violates the pack architecture rule that the tool layer centralizes allowed command patterns.

### 4. Remote file writes are not safe for the pack’s intended root-owned paths

Proof:

- The pack and examples target roots such as `/opt/candoitall` and `/etc/traefik`.
- Current transport uploads directly through SFTP as the SSH user with no sudo-capable file install path.

Code evidence:

- `src/CanDoItAll.Mcp.SshOps/Transport/SshNetTransport.cs` writes with `client.UploadFile(stream, remotePath, canOverride: true)`.
- `UploadBytesAsync` has no sudo mode and no fallback for permission-denied writes.
- `EnsureDirectoryAsync` shells out to `mkdir -p` but does not validate success and does not auto-recover when the path requires elevation.

Impact:

- `fs_apply_bundle`
- revision manifest uploads
- backup metadata uploads
- remote job script creation

This is a release blocker for the pack’s `/opt` and `/etc` scenarios even if the current Raspberry Pi target happens to use `/home/pi/...`.

### 5. `host_bootstrap_prepare` does not prepare ownership for later writes under elevated roots

Proof:

- The pack expects bootstrap to prepare a reusable deployment layout.
- The current bootstrap path only runs `mkdir -p` and optionally Docker install/systemd enable/network create.

Code evidence:

- `src/CanDoItAll.Mcp.SshOps/Coordination/TargetCoordinator.Targets.cs` creates base directories but never `chown`s them to the deployment user.
- `mode` is carried in the result but not used to alter behavior.

Impact:

- Even after bootstrap, later file and remote-job workflows can still fail on root-owned directories.

### 6. There is no SshOps automated validation project at all

Proof:

- Repo scan on 2026-03-21 found no files under `tests` matching `SshOps`.

Impact:

- None of the P0 scenarios from the SshOps validation matrix are automated.
- Regression risk is high for contract shape, compose compatibility, permission handling, and remote-job behavior.

## P1 findings

### 7. `target_audit` is incomplete relative to the pack

Proof:

- Live call on 2026-03-21 returned OS, sudo, docker, ports, disk, directories, and base tools, but no compose version on the Raspberry Pi even though `docker-compose` exists.
- The pack expects readiness signals around compose, required networks, and broader deployment prerequisites.

Code evidence:

- `GetDockerAuditAsync` only attempts the configured compose command.
- There is no audit of `target.Docker.RequiredNetworks`.
- There is no firewall-oriented readiness signal.

### 8. `compose_ps` never returns degraded health

Proof:

- The validation matrix expects degraded output when a service is unhealthy.

Code evidence:

- `ComposePsAsync` always returns `status: "success"` after parsing.
- No logic maps service health or failed state to a degraded envelope.

### 9. `compose_apply` accepts `postWaitPolicy` but does not enforce it

Proof:

- The returned warning currently says the policy is only advisory.

Code evidence:

- `ComposeApplyAsync` in `TargetCoordinator.Compose.cs` never waits on service health or HTTP readiness.

Impact:

- Public API advertises a coordination hook that is not actually performed by the server.

### 10. `stack_rollback` ignores `strategy` and assumes a fixed stack layout

Proof:

- The method always loads the latest revision and assumes compose lives at `{StacksRoot}/{stackName}/docker-compose.yml`.

Code evidence:

- `strategy` is unused in `StackRollbackAsync`.
- The compose path is synthesized instead of restored from revision metadata.

Impact:

- Rollback is only partially implemented and tightly coupled to one layout convention.

### 11. Remote HTTP probes report `durationMs = 0`

Proof:

- Live call on 2026-03-21: `http_probe(origin="remote", url="http://127.0.0.1")` returned `durationMs: 0`.

Code evidence:

- `ProbeRemoteAsync` in `TargetCoordinator.Validation.cs` hard-codes `DurationMs` to `0`.

Impact:

- Remote validation telemetry is inaccurate.

### 12. `ipfs_status` can report the gateway reachable even on non-success status codes

Proof:

- `ProbeRemoteGatewayAsync` treats any parseable HTTP status string as reachable.

Code evidence:

- `return int.TryParse(result.StandardOutput.Trim(), out _);`

Impact:

- A `404`, `500`, or other failure can still be reported as a healthy gateway.

### 13. Shared observability primitives are registered but not actually used by SshOps

Proof:

- `FileLogStore` and ring-buffer related abstractions exist in `CanDoItAll.Mcp.Core`.
- The SshOps project registers `FileLogStore` but never injects or appends to it.

Code evidence:

- Search results on 2026-03-21 show `FileLogStore` referenced only in `RuntimeConfiguration.cs` registration helpers, not in the runtime flow.

Impact:

- The server claims shared observability alignment but does not use the shared file log store in practice.

## P2 findings

### 14. Several config knobs are present but not wired into behavior

Examples:

- `SecurityOptions.DenyPasswordAuthentication`
- `SecurityOptions.RequireHostKeyPinningInProduction`
- `RemoteJobsOptions.Root`
- `RemoteJobsOptions.RetentionDays`
- `RevisionOptions.KeepLast`
- `DockerOptions.DefaultLoggingDriver`
- `ValidationOptions.PublicAppHost`

Impact:

- The configuration model over-promises relative to the implementation.

### 15. `RemotePathGuard` is lexical only and does not close the symlink-bypass gap the pack calls out

Proof:

- The guard normalizes strings and checks prefix membership.
- It never resolves a real path on the remote host before read/write.

Impact:

- A hostile symlink inside an allowed root could still redirect operations outside the intended boundary.

## Live behavior snapshot on 2026-03-21

Working on `rpi3-test`:

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
- `http_probe`
- `http_wait`
- `cert_check`
- `ipfs_status`
- `ipfs_private_validate`
- `operation_status`
- `operation_wait`
- `operation_logs`
- `dangerous_raw_exec`

Confirmed broken or blocked on `rpi3-test` before repair:

- `compose_validate`
- `compose_apply`
- All compose-dependent flows downstream of the compose command mismatch

Not yet fully proven live before repair because of upstream blockers, but statically incomplete:

- `compose_exec` command policy
- `compose_ps` degraded status
- `stack_rollback` strategy handling
- root-owned path write flow
