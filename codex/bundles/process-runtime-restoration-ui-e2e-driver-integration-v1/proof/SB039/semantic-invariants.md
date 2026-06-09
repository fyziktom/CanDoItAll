# SB039 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

This gate is not satisfied by proving that scheduler plans can be saved, or by proving workflow assignments can run inside an already-started process. The proof must show process starts can be initiated from scheduler/manual workflow-origin sources through the normal process service path, with source identity validation, without creating a generic driver runtime hook.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- scheduled process launch bypasses `ProcessesService.StartRunAsync`;
- workflow-origin process starts can omit source identity or requester audit text;
- the process trigger wrapper references workflow runtime managers, Quartz, hosted services, process workflow coordinators, driver hosts, driver registries, driver selectors, driver DI, or manager commands;
- trigger-start tests only check non-empty output instead of persisted runtime rows;
- scheduler/workflow trigger proof depends on transient `codex/bundles/<bundle-name>` paths;
- UI or mobile/small-screen proof is added for this runtime-only phase.

## Semantic Positive Proof

`bundle://proof/SB039/transcripts/focused-scheduler-workflow-launch-tests.txt` proves:

- the architecture guard passes;
- the scheduler target launcher starts a real process run and records `SchedulerPlan` audit text;
- the workflow-origin trigger path starts a real process run, creates normal start/dispatch outbox records, and creates no workflow links or execution runs;
- the negative workflow-trigger request is rejected.

## Anti-Stub Proof

`bundle://proof/SB039/transcripts/anti-stub-scheduler-workflow-trigger-negative-proof.txt` reruns the source-identity rejection test and the architecture guard. A stub implementation that returns a fabricated run id, skips persisted runtime rows, ignores source validation, or adds forbidden runtime hooks would fail this proof.

## Raw-Note Closure

- RN-007 remains partially solved: SB039 proves scheduler process targets and workflow-origin manual/test starts are ready without generic driver runtime hooks. Runtime host, registry, selector, DI registration, and manager-command decisions remain planned by SB040-SB042 and docs/final gates.
- RN-009 remains partially solved: SB001-SB039 now have separate source-backed gate rows; remaining phases still need execution.

## Production Behavior Artifact Matrix

The new production signal is the typed process trigger-start request. It does not introduce a new hosted runner or automatic workflow-to-process command. It gives scheduler/manual workflow-origin callers a validated path into the existing `StartRunAsync` runtime creation behavior.
