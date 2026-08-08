# B02 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## B02-T01 — Extract a pure runtime-node plan compiler

- [ ] Compile typed node metadata into executable, argv, environment, working directory, target, capability requirements, and display projection without starting a process.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T02 — Use direct execution as primary authority

- [ ] Run .NET, Docker, Python, Node/Tailwind, and other typed environments through B01 primitives. Never invoke a shell merely to display a terminal.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T03 — Make scripts explicit

- [ ] Model PowerShell and optional POSIX shell as distinct script kinds with executable dependency, policy, arguments, and side-effect/approval rules.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T04 — Implement OS-specific Python layout

- [ ] Resolve `Scripts/python.exe` on Windows and `bin/python` on Unix/macOS; invoke directly. Capability-gate Conda and avoid activation as execution authority.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T05 — Separate terminal presentation

- [ ] Create optional Workbench-owned adapters for Windows terminal/PowerShell, macOS terminal presentation, and configured Linux terminal. Headless execution does not require them.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T06 — Separate elevation capability

- [ ] Retain governed Windows runas where intended; default Linux/macOS to unavailable. Add no implicit sudo/pkexec/osascript path.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T07 — Migrate legacy command metadata carefully

- [ ] Recognize bounded static cmd/PowerShell wrappers for migration to typed fields. Dynamic/encoded/unknown shell content stays explicit and requires operator repair.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T08 — Use core path authority

- [ ] Resolve project/script/environment paths through the Core C4 logical/physical contracts and preserve agent-selected versus operator-selected authority.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T09 — Update capability-aware UX

- [ ] Replace PowerShell-only labels/messages with Run, Open terminal, Elevated launch, dependency missing, unsupported, headless, and foreign-path states.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T10 — Run browser and actual-host proof

- [ ] Test direct run, optional terminal, script policy, Python layout, Docker plan, cancellation, and unavailable capability states.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
