# B02 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## B02-T01 — Extract a pure runtime-node plan compiler

- [x] Compile typed node metadata into executable, argv, environment, working directory, target, capability requirements, and display projection without starting a process.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T02 — Use direct execution as primary authority

- [x] Run .NET, Docker, Python, Node/Tailwind, and other typed environments through B01 primitives. Never invoke a shell merely to display a terminal.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T03 — Make scripts explicit

- [x] Model PowerShell and optional POSIX shell as distinct script kinds with executable dependency, policy, arguments, and side-effect/approval rules.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T04 — Implement OS-specific Python layout

- [x] Resolve `Scripts/python.exe` on Windows and `bin/python` on Unix/macOS; invoke directly. Capability-gate Conda and avoid activation as execution authority.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T05 — Separate terminal presentation

- [x] Create optional Workbench-owned adapters for Windows terminal/PowerShell, macOS terminal presentation, and configured Linux terminal. Headless execution does not require them.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T06 — Separate elevation capability

- [x] Retain governed Windows runas where intended; default Linux/macOS to unavailable. Add no implicit sudo/pkexec/osascript path.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T07 — Migrate legacy command metadata carefully

- [x] Recognize bounded static cmd/PowerShell wrappers for migration to typed fields. Dynamic/encoded/unknown shell content stays explicit and requires operator repair.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T08 — Use core path authority

- [x] Resolve project/script/environment paths through the Core C4 logical/physical contracts and preserve agent-selected versus operator-selected authority.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T09 — Update capability-aware UX

- [x] Replace PowerShell-only labels/messages with Run, Open terminal, Elevated launch, dependency missing, unsupported, headless, and foreign-path states.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B02-T10 — Run browser and actual-host proof

- [x] Test direct run, optional terminal, script policy, Python layout, Docker plan, cancellation, and unavailable capability states.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies B03 as the next eligible subbundle.
