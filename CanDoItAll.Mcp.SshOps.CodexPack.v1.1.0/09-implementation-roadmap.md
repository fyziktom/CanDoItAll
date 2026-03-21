# Implementation roadmap

## Field-validated host gate

Before any physical-host deployment work continues past discovery, record the actual target facts instead of relying on operator description:

- distribution name and version,
- CPU architecture,
- glibc / OpenSSL / ICU baseline,
- Docker Engine / Compose availability,
- systemd availability,
- sudo model,
- free disk,
- usable ports for SSH, HTTP(S), and IPFS.

If the target diverges from the primary Ubuntu plus Docker baseline, branch immediately into one of two validated lanes:

- `standard-host`: supported Ubuntu with Docker/Compose and containerized Traefik, PostgreSQL, and Kubo.
- `legacy-arm-host`: low-power or legacy Linux target where Docker is unavailable or disallowed, using native systemd services, framework-dependent publish, self-signed TLS for local validation, in-memory app mode, and a private single-node or controlled-peer IPFS setup.

Any field failure discovered during validation must feed back into the roadmap, prompts, and checklists before the work is considered closed.

## Streams

The roadmap is split into two streams:

1. `shared-foundation`
2. `ssh-ops`

The SSH stream must not progress past scaffolding until the shared foundation stream is closed and DotNetWatch regression is green.

## Phase 0 - Repo discovery and current-state audit

### Goal
Understand the `CanDoItAll` solution, audit the real state of `CanDoItAll.Mcp.DotNetWatch`, and confirm the extraction candidates for shared MCP primitives.

### Deliverables
- solution discovery report,
- DotNetWatch audit,
- shared candidate inventory,
- dependency boundary notes,
- target host capability audit template.

### Exit criteria
- shared extraction candidates are confirmed,
- server-specific components are explicitly excluded from extraction,
- names and locations of the shared projects are fixed.

## Phase 1 - Create shared MCP foundation

### Goal
Create:

- `src/CanDoItAll.Mcp.Core`
- `src/CanDoItAll.Mcp.LocalRuntime`

### Deliverables
`CanDoItAll.Mcp.Core`:
- common contracts,
- response envelope,
- error model,
- correlation / operation / server identity helpers,
- mutation gate,
- log abstractions,
- log persistence,
- secret redaction,
- generic async operation primitives,
- shared HTTP/TLS probe helpers.

`CanDoItAll.Mcp.LocalRuntime`:
- process supervisor,
- command runner,
- process-tree cleanup,
- managed process wrappers,
- stale process registry.

### Exit criteria
- both projects build,
- both projects have baseline tests,
- no server-specific domain behavior leaks into shared code.

## Phase 2 - Refactor DotNetWatch onto the shared foundation

### Goal
Move `CanDoItAll.Mcp.DotNetWatch` onto the new shared primitives.

### Deliverables
- references to `CanDoItAll.Mcp.Core`,
- references to `CanDoItAll.Mcp.LocalRuntime`,
- duplicate helpers removed or reduced to thin adapters,
- public tool contracts preserved.

### Exit criteria
- DotNetWatch builds,
- no contract regression is introduced,
- app lifecycle, wait flow, log flow, and cleanup behavior remain stable.

## Phase 3 - DotNetWatch regression gate

### Goal
Catch regressions before any SSH implementation work continues.

### Deliverables
- regression checklist report,
- contract snapshot comparison,
- log / operation / wait smoke coverage,
- explicit remaining debt list.

### Exit criteria
- regression gate is green,
- no blocker remains in the shared foundation.

## Phase 4 - SSH Ops server skeleton

### Goal
Build the minimum `CanDoItAll.Mcp.SshOps` host on the shared foundation.

### Deliverables
- `Program.cs`,
- options binding,
- basic logging,
- references to shared projects,
- `targets_list`,
- `target_test` placeholder.

### Exit criteria
- the server starts as a stdio MCP server,
- tool registration is stable,
- stdout is clean,
- shared response envelope and error model are used.

## Phase 5 - SSH transport and security baseline

### Goal
Implement SSH transport, host key verification, and secret resolution.

### Deliverables
- `ISshTransport`,
- `SshNetTransport`,
- `HostKeyVerifier`,
- `SecretResolver`,
- remote-root policy,
- target catalog.

### Exit criteria
- `target_test` works,
- host key mismatch produces a hard failure,
- secrets are not logged.

## Phase 6 - Remote files and detached operations

### Goal
Support file deployment and long-running remote jobs.

### Deliverables
- `RemoteFileService`,
- `fs_apply_bundle`,
- `fs_read_text`,
- `fs_backup_path`,
- `RemoteJobRunner`,
- `operation_*` tools.

### Exit criteria
- long-running operations return `operationId`,
- detached logs remain readable,
- restart of the MCP server does not destroy operation visibility.

## Phase 7 - Deployment lanes

### Goal
Support the deployment shape that matches the audited host.

### Standard-host deliverables
- `docker_network_ensure`,
- `docker_volume_ensure`,
- `compose_validate`,
- `compose_apply`,
- `compose_ps`,
- `compose_logs`,
- `compose_down`.

### Legacy-arm-host deliverables
- documented native-service bundle layout,
- framework-dependent publish path,
- Traefik file-provider configuration,
- IPFS native service bootstrap,
- app runtime compatibility checks,
- in-memory app configuration profile.

### Exit criteria
- the selected lane is explicit in the validation record,
- the chosen deployment path is reproducible,
- validation proves that the lane decision was correct for the host.

## Phase 8 - Traefik and TLS

### Goal
Provide HTTPS exposure for both deployment lanes.

### Deliverables
- `TraefikService`,
- `http_probe`,
- `http_wait`,
- `cert_check`,
- Docker examples for the standard-host lane,
- file-provider and self-signed examples for the legacy-arm-host lane.

### Exit criteria
- a demo app is reachable through Traefik,
- TLS identity matches the selected validation mode,
- the dashboard is not publicly exposed without protection.

## Phase 9 - Data services and IPFS

### Goal
Validate PostgreSQL and private IPFS where appropriate.

### Deliverables
- `postgres_ready`,
- `ipfs_status`,
- `ipfs_private_validate`,
- standard-host example with PostgreSQL and Kubo,
- legacy-arm-host guidance for private IPFS and in-memory app mode.

### Exit criteria
- private swarm validation detects wrong bootstrap configuration,
- the app can use the selected persistence profile,
- public IPFS bootstrap peers are excluded.

## Phase 10 - Rollback, validations, and hardening

### Goal
Close the operational loop.

### Deliverables
- `stack_rollback`,
- validation orchestrator,
- redaction tests,
- guardrail checks,
- compatibility notes,
- real-host failure analysis template.

### Exit criteria
- rollback works where supported,
- smoke coverage exists for both validation lanes,
- runbook and compatibility notes are updated from field findings.

## Phase 11 - QA closure

### Goal
Run strict review over the shared foundation and the SSH server.

### Deliverables
- self-review report,
- remediation fixes,
- updated docs,
- final approval note,
- Playwright browser proof artifacts for the field validation run.

### Exit criteria
- checklists are green,
- known risks are explicit,
- field validation evidence is attached,
- the package is internally consistent.
