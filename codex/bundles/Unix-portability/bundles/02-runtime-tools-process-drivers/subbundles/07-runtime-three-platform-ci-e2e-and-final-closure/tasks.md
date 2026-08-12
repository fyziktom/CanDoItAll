# B07 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## B07-T01 — Extend active CI runtime matrix

- [x] Add focused actual-host jobs for process/executable/environment, Workbench runtime nodes, Manager, MCP/external tools, plugins, and process drivers on Windows, Ubuntu, and macOS.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T02 — Run Workbench browser proof

- [x] Capture capability-aware runtime actions, headless states, missing dependencies, foreign paths, terminal/elevation unavailability, and successful direct execution.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T03 — Run MCP/external tool E2E

- [x] Execute the retained deterministic local stdio MCP and governed external-tool integration slice on locally available hosts.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T04 — Run Manager lifecycle/recovery E2E

- [x] Run the focused Manager discovery, lifecycle, watcher, recovery, ownership, and interruption coverage.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T05 — Run plugin/FileTools/Docker matrix

- [x] Separate supported interactive/desktop/Docker profiles from headless/unavailable profiles and preserve truthful diagnostics.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T06 — Run representative process-domain scenario

- [x] Cover representative special-tool Process execution, missing-capability behavior, receipts, recovery, and authority boundaries.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T07 — Run full Windows regression and core C4 recheck

- [x] Preserve the prior Core C4 proof and keep the focused runtime slice separate from the ordinary stable suite.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T08 — Perform failure injection and security scan

- [x] Retain focused failure-injection and security cases for cancellation, ownership, path, permission, cache, dependency, and disclosure faults.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T09 — Publish final support/limitation matrix

- [x] Record proved local profiles, configured hosted profiles, desktop/headless distinctions, known limitations, rollback, and evidence links.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T10 — Issue Final Gate R4

- [ ] Run the configured hosted Windows/Ubuntu/macOS aggregate and issue independent Final Gate R4.
- [x] Keep local completion distinct from the deferred hosted/R4 claim.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has local evidence and an explicit hosted status.
- [x] Focused local validation passes.
- [x] Source references/findings/ADRs/traceability are current for local implementation.
- [x] Retained local artifacts are redacted.
- [ ] Independent Final Gate R4 reviewers record GO after hosted evidence exists.
- [x] Handoff identifies hosted execution and R4 as the only remaining closure step.
