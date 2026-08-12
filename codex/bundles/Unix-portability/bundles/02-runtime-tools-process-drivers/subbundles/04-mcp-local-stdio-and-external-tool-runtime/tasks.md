# B04 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## B04-T01 — Authorize resolved executable identity

- [x] Resolve first through B01, then validate exact capability-owned names/paths/signatures. Remove policy/resolver suffix and case drift.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T02 — Reuse process lifecycle and environment semantics

- [x] Launch local stdio MCP through the authoritative primitive/owned registry, with bounded startup, stream lifecycle, timeout, cancellation, and cleanup.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T03 — Route secret bindings through runtime resolution

- [x] Persist names/references only; resolve values immediately before launch; clear/avoid retaining values after process setup; receipts contain approved names.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T04 — Replace global Playwright cache discovery

- [x] Install or locate the pinned MCP package under a controlled versioned application tool root with integrity/version evidence and atomic setup.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T05 — Harden MCP setup validation

- [x] Report missing runtime, package, executable, working directory, secret, permission, and unsupported platform separately with deterministic remediation.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T06 — Refactor external process tools

- [x] Remove or wrap LocalExternalProcessRunner so it cannot diverge from B01 timeout, output, cancellation, tree kill, environment, and receipt behavior.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T07 — Redact and bound outputs

- [x] Apply sentinel-aware redaction before JSON-parse errors, non-zero-exit diagnostics, receipts, logs, or agent context. Preserve enough bounded evidence to debug.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T08 — Prove governed end-to-end paths

- [x] Run deterministic local stdio MCP and external JSON tool proof on Windows/Linux with compositional approval, workspace containment, secret binding, timeout, caller cancellation, invalid output, and cleanup. Actual macOS remains operator-deferred.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T09 — Issue MCP/tool gate R3a

- [x] Security/runtime reviewers approve executable identity, secret handling, output, and lifecycle before plugins.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies B05 as the next eligible subbundle.
