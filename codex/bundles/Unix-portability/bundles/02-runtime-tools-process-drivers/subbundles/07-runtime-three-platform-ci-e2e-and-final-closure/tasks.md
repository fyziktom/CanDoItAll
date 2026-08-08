# B07 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## B07-T01 — Extend active CI runtime matrix

- [ ] Add focused actual-host jobs for process/executable/environment, Workbench runtime nodes, Manager, MCP/external tools, plugins, and process drivers on Windows, Ubuntu, and macOS.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T02 — Run Workbench browser proof

- [ ] Capture capability-aware runtime actions, headless states, missing dependencies, foreign paths, terminal/elevation unavailability, and successful direct execution.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T03 — Run MCP/external tool E2E

- [ ] Execute a deterministic local stdio MCP and governed external tool per claimed profile with approval, secret binding, workspace containment, timeout, invalid output, and cleanup.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T04 — Run Manager lifecycle/recovery E2E

- [ ] Launch dotnet watch/Tailwind, restart Manager, reconcile registry/discovery, stop only owned processes, inject PID/metadata/watcher faults, and prove no leak/foreign kill.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T05 — Run plugin/FileTools/Docker matrix

- [ ] Separate supported interactive/desktop/Docker profiles from headless/unavailable profiles and preserve truthful diagnostics.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T06 — Run representative process-domain scenario

- [ ] Use a process with special tools and review/recovery. Prove success or exact missing-capability behavior, receipts, evidence, no authority regression, and no escalation loop.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T07 — Run full Windows regression and core C4 recheck

- [ ] All core path/storage/security/headless gates remain green with runtime features enabled and disabled.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T08 — Perform failure injection and security scan

- [ ] Cover child leaks, cancellation, secret output, executable substitution, path/symlink escape, missing native service, permission denial, cache corruption, and external dependency drift.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T09 — Publish final support/limitation matrix

- [ ] Record exact OS/profile/RID/dependency versions, desktop/headless distinctions, known limitations, operator remediation, rollback, and evidence links.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B07-T10 — Issue Final Gate R4

- [ ] Only after all P0 requirements are Solved and independent architecture/security/runtime/QA/operations review is GO may the program be marked complete.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
