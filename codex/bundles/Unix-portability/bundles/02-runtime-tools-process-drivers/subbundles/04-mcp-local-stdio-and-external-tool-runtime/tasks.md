# B04 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## B04-T01 — Authorize resolved executable identity

- [ ] Resolve first through B01, then validate exact capability-owned names/paths/signatures. Remove policy/resolver suffix and case drift.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T02 — Reuse process lifecycle and environment semantics

- [ ] Launch local stdio MCP through the authoritative primitive/owned registry, with bounded startup, stream lifecycle, timeout, cancellation, and cleanup.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T03 — Route secret bindings through runtime resolution

- [ ] Persist names/references only; resolve values immediately before launch; clear/avoid retaining values after process setup; receipts contain approved names.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T04 — Replace global Playwright cache discovery

- [ ] Install or locate the pinned MCP package under a controlled versioned application tool root with integrity/version evidence and atomic setup.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T05 — Harden MCP setup validation

- [ ] Report missing runtime, package, executable, working directory, secret, permission, and unsupported platform separately with deterministic remediation.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T06 — Refactor external process tools

- [ ] Remove or wrap LocalExternalProcessRunner so it cannot diverge from B01 timeout, output, cancellation, tree kill, environment, and receipt behavior.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T07 — Redact and bound outputs

- [ ] Apply sentinel-aware redaction before JSON-parse errors, non-zero-exit diagnostics, receipts, logs, or agent context. Preserve enough bounded evidence to debug.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T08 — Prove governed end-to-end paths

- [ ] Run a deterministic local stdio MCP and an external JSON tool on Windows/Linux/macOS with approval, workspace containment, secret binding, timeout, cancellation, invalid output, and cleanup.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B04-T09 — Issue MCP/tool gate R3a

- [ ] Security/runtime reviewers approve executable identity, secret handling, output, and lifecycle before plugins.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
