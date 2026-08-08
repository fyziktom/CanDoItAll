# B00 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## B00-T01 — Verify Core C4 handoff

- [ ] Confirm exact passing commit, active CI, support matrix, migrations, open limitations, and no uncommitted operator work. Stop if the handoff is incomplete.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T02 — Rebase all runtime source references

- [ ] Compare prepared commit and Core C4 HEAD; update renamed files/projects, new process/tool surfaces, and requirement ownership.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T03 — Generate full runtime execution inventory

- [ ] Find every ProcessStartInfo, Process, shell/script, terminal, executable resolver, environment binder, watcher, WMI/proc, MCP stdio, external tool, Docker/FileTools, and process-driver call path.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T04 — Map authoritative ownership

- [ ] For each surface record plan compiler, execution primitive, lifecycle owner, process registry, cancellation/kill, capability probe, receipt/evidence, UI presentation, recovery, and domain failure owner.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T05 — Characterize current behavior on all OSes

- [ ] Run existing process-host, Workbench, Manager, MCP, plugin, and process-driver tests; add no implementation yet. Capture process trees, output, environment, and failure modes.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T06 — Review external package/native dependencies

- [ ] Record FileTools package, Docker, node/npm/npx, Playwright MCP, PowerShell, Python/Conda, terminal, WMI, procfs/libproc/ps, Keychain/Secret Service interactions, versions, and support evidence.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T07 — Apply split triggers

- [ ] If implementation exceeds 60 production files, crosses more than 8 project ownership boundaries, needs external package source changes, or cannot preserve independent gates, create child execution bundles before edits.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T08 — Issue Gate R0

- [ ] Require no unclassified P0/P1 runtime surface and approve the B01–B07 ownership/dependency graph.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
