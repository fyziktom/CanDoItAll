# SB024 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

The gate is not satisfied by DI registration alone, by a workflow candidate appearing in a launch plan, or by the process mock catalog being seedable. The proof must show actual dispatch through a workflow-backed process role and actual direct-agent process execution using deterministic runtime services.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- workflow executor composition is no longer registered;
- process workflow roles no longer resolve to workflow candidates;
- process dispatch no longer routes workflow-backed assignments through `ProcessWorkflowRunCoordinator`;
- workflow execution no longer persists `ProcessWorkflowRunLink`;
- workflow run state no longer maps to process step completion;
- workflow-run evidence is no longer projected as process artifacts;
- deterministic process mock agents cannot be seeded;
- direct-agent process dispatch no longer creates successful MAF execution run records;
- either focused runtime test relies on bundle fixtures, sleeps, test-server shortcuts, direct database mutation, or raw SQL shortcuts.

## Semantic Positive Proof

`bundle://proof/SB024/transcripts/focused-maf-workflow-direct-agent-runtime-tests.txt` proves both focused runtime tests pass against current application services and database state.

## Anti-Stub Proof

`bundle://proof/SB024/transcripts/anti-stub-maf-workflow-direct-agent-runtime-tests.txt` proves the workflow-backed route and deterministic direct-agent route use real app/runtime services rather than report-only or mutation-shortcut proof.

## Raw-Note Closure

- RN-004 remains partially open: SB024 proves MAF workflow and deterministic direct-agent runtime compatibility, but representative `.NET app` create/modify scenario proof remains planned by SB025-SB027.
- RN-007 is partially closed further: SB024 proves current process dispatch is compatible with MAF workflow-backed roles and direct-agent routes. Runtime host, registry, selector, DI registration, manager command, scheduler, and workflow-driver roadmap items remain planned by SB037-SB042 and SB050-SB054.

## Production Behavior Artifact Matrix

No new production signals were introduced in SB022-SB024. Existing MAF runtime registration, workflow process executor bridge, process workflow run links, process artifact projection, direct-agent execution runs, and process mock deterministic provider behavior are covered by focused integration tests and source assertions.
