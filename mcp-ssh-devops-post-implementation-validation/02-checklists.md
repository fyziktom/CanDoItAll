# Checklists

## Repair checklist

- [x] Add compose-command compatibility handling so the server can use `docker compose` or `docker-compose` safely.
- [x] Emit contract-specific failure statuses instead of the current generic `failed`.
- [x] Add a centralized `compose_exec` command policy with safe allow/deny behavior.
- [x] Harden remote directory creation and file upload for elevated paths used by the pack.
- [x] Make bootstrap create a deployment layout that remains writable for later server operations.
- [x] Improve `target_audit` so compose readiness and required Docker networks are surfaced honestly.
- [x] Make `compose_ps` return degraded status when service state/health is unhealthy.
- [x] Repair `ipfs_status` gateway reachability semantics.
- [x] Stop advertising `postWaitPolicy` as if it were implemented, or implement it.
- [x] Add a scripted validation harness for the main validation matrix scenarios.

## Remote validation checklist

- [x] `targets_list` proves the configured Raspberry Pi target is visible.
- [x] `target_test` proves SSH connectivity, identity, and host key validation.
- [x] `target_audit` proves OS, sudo, Docker, compose readiness, ports, directories, and base tools.
- [x] `fs_apply_bundle` can create a scratch validation stack on the Raspberry Pi.
- [x] `fs_backup_path` and `fs_restore_backup` can round-trip a test file.
- [x] `docker_network_ensure` and `docker_volume_ensure` are idempotent on the Raspberry Pi.
- [x] `compose_validate` succeeds on the scratch stack.
- [x] `compose_apply` can start the scratch stack.
- [x] `compose_ps` reports healthy service state for the scratch stack.
- [x] `compose_logs` returns recent logs with redaction still enabled.
- [x] `compose_exec` allows safe commands and blocks disallowed shell patterns.
- [x] `postgres_ready` succeeds against a scratch PostgreSQL service.
- [x] `compose_down` stops the scratch stack cleanly.
- [x] `stack_rollback` can restore a known-good revision after a controlled bad change.
- [x] `http_probe`, `http_wait`, and `cert_check` are verified against the Raspberry Pi services.
- [x] `ipfs_status` and `ipfs_private_validate` are verified against the Raspberry Pi services.
- [x] `host_bootstrap_prepare`, `operation_status`, `operation_wait`, `operation_logs`, and `operation_cancel` are validated through a real detached remote job path.
- [x] `dangerous_raw_exec` still works only when explicitly enabled.

## Release gate checklist

- [x] P0 contract blockers are fixed in code, not only documented.
- [x] The live Raspberry Pi validation passes end to end.
- [x] The new validation artifacts in this folder are updated to the repaired behavior.
- [x] The implementation no longer depends on a target-specific compose-command accident.
- [x] Root-owned path scenarios from the pack are no longer silently broken.
