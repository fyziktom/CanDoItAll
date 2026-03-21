# Implementation Prompts

These prompts are organized so each one covers a coherent function area from the SshOps pack.

## Prompt 1: Foundation and contract repair

You are a senior C# engineer working inside the `CanDoItAll` repo.
Analyze `CanDoItAll.Mcp.SshOps.CodexPack.v1.1.0` against `src/CanDoItAll.Mcp.SshOps` and `src/CanDoItAll.Mcp.Core`.
Repair public MCP envelope behavior so tool failures return meaningful contract-level statuses instead of generic `failed`.
Preserve the shared `CanDoItAll.Mcp.Core.Contracts.McpToolEnvelope<T>` shape.
Do not invent a second envelope type.
Add or update tests where practical.

## Prompt 2: Remote paths, bootstrap, and write safety

You are a senior C# engineer.
Repair the SshOps remote write path so `fs_apply_bundle`, revision uploads, backup metadata uploads, and remote job script creation can work for pack-intended elevated roots such as `/opt/candoitall` and `/etc/traefik`.
Also repair `host_bootstrap_prepare` so prepared directories remain usable by later tools.
Keep the implementation safe for the current Raspberry Pi target and do not break the `/home/pi/candoitall` flow.
Add validation or tests where practical.

## Prompt 3: Compose, rollback, and PostgreSQL

You are a senior C# engineer.
Repair the compose stack toolchain in `CanDoItAll.Mcp.SshOps`.
The server must handle hosts that provide `docker compose` and hosts that only provide `docker-compose`.
Cover `compose_validate`, `compose_apply`, `compose_ps`, `compose_logs`, `compose_exec`, `compose_down`, `stack_rollback`, and `postgres_ready`.
Add a safe command policy for `compose_exec`.
Prefer fixes in shared coordinator helpers instead of duplicating compose logic per tool.

## Prompt 4: HTTP, TLS, and IPFS validation

You are a senior C# engineer.
Repair the validation tools in `CanDoItAll.Mcp.SshOps` so remote HTTP timing is honest, IPFS gateway reachability is semantically correct, and certificate/IPFS validation behavior matches the pack as closely as practical.
Keep the shared `HttpProbeService` and TLS helpers in `CanDoItAll.Mcp.Core` as the foundation.

## Prompt 5: Remote Raspberry Pi validation

You are a senior QA-oriented C# engineer.
Create a validation harness or integration coverage that exercises the repaired SshOps implementation directly against target `rpi3-test` using the repository settings and environment variables.
Validate:

- target discovery and SSH connectivity
- audit
- file bundle, backup, and restore
- docker network and volume ensure
- compose validate/apply/ps/logs/exec/down
- PostgreSQL readiness
- rollback
- HTTP/TLS validation
- IPFS validation
- detached operation status/wait/logs/cancel
- dangerous raw exec

Use a scratch stack under `/home/pi/candoitall/stacks` and clean up after the run.
