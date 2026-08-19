# Merge-readiness requirements

## P0

### MR-001 — legacy persisted plans remain safe

A persisted process plan produced before host-capability sealing must either:

1. verify under its declared legacy hash algorithm and migrate transactionally to the current format without weakening requirements; or
2. enter a typed non-executable `NeedsRecompile`/`NeedsMigration` state with an operator-safe remediation.

It must never be accepted as capability-free because newly added JSON fields were absent.

### MR-002 — source graph is reproducible

A clean checkout must produce the same package-mode graph and capability claims. Direct sibling-source mode must require explicit operator opt-in and a verified FileTools contract/source anchor. Directory presence alone must not produce a `Validated` implementation claim.

### MR-003 — process ownership covers descendants

Stopping, timing out, disposing, or recovering an owned process must leave no owned descendant alive, even when the root exits before the force phase. Exact process identity and PID-reuse protection remain mandatory.

## P1

### MR-004 — local MCP supports required bidirectional control messages

The client must answer peer `ping`, explicitly reject unsupported peer requests, continue waiting for the original response, and enforce bounded line/message processing.

### MR-005 — Docker recipe contracts are strict

Malformed values must fail validation instead of defaulting. Pull, port, log, name, environment, and argument contracts must remain bounded and shell-free.

### MR-006 — path authority is centralized

A successful workspace/managed-file resolution must be contained under the configured root and unable to traverse a symbolic link or reparse point.

### MR-007 — executable authority is host-realistic

Unix resolution must prove current-process execution access. Windows `PATHEXT` and all candidates must use bounded, non-path extension syntax. Every resolved executable retains canonical identity and fingerprint proof.

### MR-008 — validation cannot consume stale binaries

Focused validation must use a source/build stamp matching the current commit, dependency mode, configuration, and selected assembly hashes. Bare method-name selectors are forbidden.

### MR-009 — canonical records agree

Execution report, gate log, requirements, source manifest, runtime/ownership inventories, support matrix, exact anchors, checksums, and final handoffs must describe the same candidate and residuals.

### MR-010 — local Docker stack is clean-checkout reproducible

A fresh checkout can create a disposable test secret, validate Compose, start PostgreSQL and the app, observe health/readiness, and remove all disposable resources without committing the secret.

## Nonfunctional constraints

- No service locator.
- No hidden shell invocation.
- No silent security fallback.
- No full-path or secret leakage in normal diagnostics/evidence.
- No full suite outside scheduled checkpoints.
- No macOS verified claim before actual-host evidence.
- No source comments outside English.
