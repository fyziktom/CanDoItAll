# B06 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## B06-T01 — Model required host capabilities

- [ ] Let process strategies/special tools declare capabilities such as direct execution, Docker, Python, Node, pwsh script, MCP, desktop, or terminal without branching on OS.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T02 — Keep host facts outside process semantics

- [ ] Host adapters expose availability and execution ports; Processes owns eligibility, alternate strategy, recovery, escalation, evidence, and failure interpretation.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T03 — Validate templates/plans before side effects

- [ ] Compile or launch-check required capabilities and produce deterministic missing-capability diagnostics with safe repair/alternate strategy.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T04 — Preserve authority and approvals

- [ ] Host capability presence never grants workspace/project scope, mutation, tool access, approval, or process authority. Re-run canonical authority and per-run workspace tests.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T05 — Adapt special/domain drivers

- [ ] Review every driver/tool that starts processes, opens files, runs Docker/Python/Node/PowerShell/MCP, or consumes host paths. Route through B01–B05 capabilities.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T06 — Normalize receipts/evidence

- [ ] Serialize logical paths, strategy/driver/capability IDs, tested platform profile, and bounded diagnostics; omit secrets and unnecessary host absolute paths.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T07 — Define platform layer semantics

- [ ] Document that ProcessDriverLayer.Platform means a process strategy package constrained by host capabilities, not a generic operating-system service layer.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T08 — Test alternate and unavailable paths

- [ ] For each special tool, prove success on supported profiles and planned fail/alternate behavior elsewhere without accidental escalation loops.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B06-T09 — Issue process architecture gate R3

- [ ] Independent process/MAF/security review confirms ownership, authority, capability, receipts, and recovery.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
