# Validation strategy

## 1. Principle

Validation must prove two things at the same time:

1. the MCP server controls the remote host safely and deterministically,
2. Codex receives a stable, readable, repeatable tool surface.

This is not only about unit tests. The package requires layered validation:

- static configuration validation,
- unit tests,
- contract tests,
- integration tests,
- end-to-end validation against a real host,
- negative and failure-injection scenarios,
- release gates.

## 2. Real-host calibration gate

Before mutating a real host, record the actual target facts instead of trusting the operator description:

- distribution name and version,
- CPU architecture,
- glibc / OpenSSL / ICU baseline,
- Docker Engine / Compose availability,
- systemd availability,
- sudo behavior,
- free disk,
- usable ports,
- whether containers are allowed on that host at all.

The validation record must choose one lane:

- `standard-host`: Ubuntu plus Docker/Compose, containerized Traefik/PostgreSQL/Kubo, ACME-capable TLS.
- `legacy-arm-host`: constrained or legacy Linux target, native systemd services, framework-dependent app publish, self-signed TLS for local-only validation, in-memory app configuration, and private IPFS without public bootstrap peers.

If the host forces a lane switch during validation, the failure analysis and the updated plan/prompts/checklists become mandatory release artifacts.

## 3. Test layers

### 3.1 Unit
Cover:
- host key matching,
- path guard rules,
- secret redaction,
- operation state handling,
- timeout and retry policy,
- DTO to command mapping.

Requirements:
- fast,
- no network,
- no real SSH,
- no Docker daemon.

### 3.2 Contract
Cover:
- request and response shapes,
- required fields,
- standard error codes,
- `operationId` behavior,
- stability of `status`, `summary`, and `nextSteps`.

### 3.3 Integration
Cover:
- fake `ISshTransport`,
- detached job runner behavior,
- locking,
- rollback orchestration,
- bundle upload/apply flow,
- wait and log flow.

### 3.4 End-to-end
Cover the selected real-host lane and prove:
- host bootstrap,
- Traefik deployment,
- HTTPS reachability,
- certificate validation for the selected lane,
- private IPFS behavior,
- app health,
- reconnect and wait behavior for long-running operations,
- browser proof from a client machine with Playwright screenshots and at least one real UI navigation step.

## 4. Mandatory negative scenarios

Always test:
- wrong SSH credential,
- wrong host key fingerprint,
- path traversal attempt,
- public exposure of IPFS API,
- public exposure of PostgreSQL where PostgreSQL is part of the lane,
- occupied ports,
- missing runtime compatibility on the target,
- wrong IPFS swarm key,
- public bootstrap peers left enabled,
- rollback to a non-existent revision.

## 5. Evidence artifacts

Every real-host validation run must capture:

- target identifier,
- timestamp,
- git revision,
- sanitized target configuration,
- operation journal,
- relevant remote logs,
- probe results,
- certificate summary,
- IPFS private validation summary,
- browser screenshots,
- failure analysis if the lane changed,
- list of pack docs updated because of field findings.

## 6. Release gates

Release is not ready unless all of the following are true:

- no blocker remains in the threat model,
- no critical secret leak exists in logs,
- unit, contract, and integration suites are green,
- at least one green real-host deploy exists,
- rollback validation is green where supported,
- host key mismatch handling is green,
- IPFS private validation is green,
- browser proof is green,
- known risks are documented,
- plan, prompts, and checklists have been updated if field validation disproved an assumption.

## 7. Exit criteria for Codex implementation

Codex may treat the work as done only when:

- the code builds on .NET 10,
- every tool has a documented request and response example,
- configuration validation runs on startup,
- the selected real-host validation lane passed end to end,
- the QA checklist is green,
- the browser proof artifacts are attached,
- the closing self-review explains any remaining limitations.
