# B03 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## B03-T01 — Create an owned launched-process registry

- [x] Persist non-secret PID/start identity/executable/argv hash/workspace/user/parent/lease state for Manager-launched processes and reconcile it on restart.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T02 — Isolate Windows WMI

- [x] Move ManagementObjectSearcher and System.Management calls into a Windows leaf adapter selected by composition; neutral Manager contracts do not reference WMI.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T03 — Implement Linux recovery discovery

- [x] Use `/proc` or another bounded mechanism to obtain start time, owner, executable, command, and parent where permitted. Missing evidence must not authorize termination.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T04 — Implement macOS recovery discovery

- [x] Use a tested bounded adapter (native API or strictly parsed command fixture) with locale/permission/race handling and safe fallback.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T05 — Strengthen ownership proof

- [x] Require PID plus start identity and additional registry/user/executable/command/workspace evidence. Reject name-only, substring-only, or ambiguous matches.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T06 — Unify launch/stop lifecycle

- [x] Use B01 execution primitives for dotnet watch, Tailwind/npm, tuning, and related processes; implement graceful stop then bounded force kill.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T07 — Make watcher pipelines convergent

- [x] Reuse Core watcher hint/rescan principles, generation/fingerprint output, duplicate suppression, overflow recovery, and polling fallback without merging domain-specific supervisor logic.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T08 — Fix path/comparer semantics

- [x] Remove global case-insensitive path sets and use Core C4 logical/physical rules for project references, roots, output, ignored segments, and process signatures.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T09 — Inject process and watcher failures

- [x] Test PID reuse, process exit race, unreadable command line, permission denied, orphan registry, duplicate starts, watcher overflow, rename storm, shutdown interruption, and output convergence.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T10 — Issue Gate R2

- [x] No later MCP/plugin/process adaptation begins until process ownership and Manager lifecycle are independently approved.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies the next eligible subbundle or conditional stop.
