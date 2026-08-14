# B00 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## B00-T01 — Verify accepted core handoff

- [x] Confirm exact immutable anchors, local gate evidence, deferred hosted/macOS proof, support matrix, migrations, open limitations, and workspace state. Stop if the provisional handoff is incomplete or overclaims support.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T02 — Rebase all runtime source references

- [x] Compare prepared commit and Core C4 HEAD; update renamed files/projects, new process/tool surfaces, and requirement ownership.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T03 — Generate full runtime execution inventory

- [x] Find every ProcessStartInfo, Process, shell/script, terminal, executable resolver, environment binder, watcher, WMI/proc, MCP stdio, external tool, Docker/FileTools, and process-driver call path.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T04 — Map authoritative ownership

- [x] For each surface record plan compiler, execution primitive, lifecycle owner, process registry, cancellation/kill, capability probe, receipt/evidence, UI presentation, recovery, and domain failure owner.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T05 — Characterize current behavior on all OSes

- [x] Run existing process-host, Workbench, Manager, MCP, plugin, and process-driver tests; add no implementation yet. Capture process trees, output, environment, and failure modes.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T06 — Review external package/native dependencies

- [x] Record FileTools package, Docker, node/npm/npx, Playwright MCP, PowerShell, Python/Conda, terminal, WMI, procfs/libproc/ps, Keychain/Secret Service interactions, versions, and support evidence.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T07 — Apply split triggers

- [x] If implementation exceeds 60 production files, crosses more than 8 project ownership boundaries, needs external package source changes, or cannot preserve independent gates, create child execution bundles before edits.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B00-T08 — Issue Gate R0

- [x] Require no unclassified P0/P1 runtime surface and approve the B01–B07 ownership/dependency graph. Independent Gate R0 GO recorded.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies the next eligible subbundle or conditional stop.
