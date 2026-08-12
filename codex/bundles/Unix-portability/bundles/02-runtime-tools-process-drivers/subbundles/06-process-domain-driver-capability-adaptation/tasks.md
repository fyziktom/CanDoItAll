# B06 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## B06-T01 — Model required host capabilities

- [x] Let process strategies/special tools declare capabilities such as direct execution, Docker, Python, Node, pwsh script, MCP, desktop, or terminal without branching on OS.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T02 — Keep host facts outside process semantics

- [x] Host adapters expose availability and execution ports; Processes owns eligibility, alternate strategy, recovery, escalation, evidence, and failure interpretation.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T03 — Validate templates/plans before side effects

- [x] Compile or launch-check required capabilities and produce deterministic missing-capability diagnostics with safe repair/alternate strategy.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T04 — Preserve authority and approvals

- [x] Host capability presence never grants workspace/project scope, mutation, tool access, approval, or process authority. Re-run canonical authority and per-run workspace tests.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T05 — Adapt special/domain drivers

- [x] Review every driver/tool that starts processes, opens files, runs Docker/Python/Node/PowerShell/MCP, or consumes host paths. Route through B01–B05 capabilities.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T06 — Normalize receipts/evidence

- [x] Serialize logical paths, strategy/driver/capability IDs, tested platform profile, and bounded diagnostics; omit secrets and unnecessary host absolute paths.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T07 — Define platform layer semantics

- [x] Document that ProcessDriverLayer.Platform means a process strategy package constrained by host capabilities, not a generic operating-system service layer.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T08 — Test alternate and unavailable paths

- [x] For each special tool, prove success on supported profiles and planned fail/alternate behavior elsewhere without accidental escalation loops.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T09 — Issue process architecture gate R3

- [x] Independent process/MAF/security review confirms ownership, authority, capability, receipts, and recovery.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies B07 as locally eligible while actual macOS/hosted R4 evidence remains deferred.
