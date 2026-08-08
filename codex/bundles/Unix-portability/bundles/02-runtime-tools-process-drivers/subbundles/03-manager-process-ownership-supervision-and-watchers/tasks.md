# B03 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## B03-T01 — Create an owned launched-process registry

- [ ] Persist non-secret PID/start identity/executable/argv hash/workspace/user/parent/lease state for Manager-launched processes and reconcile it on restart.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T02 — Isolate Windows WMI

- [ ] Move ManagementObjectSearcher and System.Management calls into a Windows leaf adapter selected by composition; neutral Manager contracts do not reference WMI.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T03 — Implement Linux recovery discovery

- [ ] Use `/proc` or another bounded mechanism to obtain start time, owner, executable, command, and parent where permitted. Missing evidence must not authorize termination.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T04 — Implement macOS recovery discovery

- [ ] Use a tested bounded adapter (native API or strictly parsed command fixture) with locale/permission/race handling and safe fallback.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T05 — Strengthen ownership proof

- [ ] Require PID plus start identity and additional registry/user/executable/command/workspace evidence. Reject name-only, substring-only, or ambiguous matches.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T06 — Unify launch/stop lifecycle

- [ ] Use B01 execution primitives for dotnet watch, Tailwind/npm, tuning, and related processes; implement graceful stop then bounded force kill.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T07 — Make watcher pipelines convergent

- [ ] Reuse Core watcher hint/rescan principles, generation/fingerprint output, duplicate suppression, overflow recovery, and polling fallback without merging domain-specific supervisor logic.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T08 — Fix path/comparer semantics

- [ ] Remove global case-insensitive path sets and use Core C4 logical/physical rules for project references, roots, output, ignored segments, and process signatures.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T09 — Inject process and watcher failures

- [ ] Test PID reuse, process exit race, unreadable command line, permission denied, orphan registry, duplicate starts, watcher overflow, rename storm, shutdown interruption, and output convergence.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B03-T10 — Issue Gate R2

- [ ] No later MCP/plugin/process adaptation begins until process ownership and Manager lifecycle are independently approved.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
