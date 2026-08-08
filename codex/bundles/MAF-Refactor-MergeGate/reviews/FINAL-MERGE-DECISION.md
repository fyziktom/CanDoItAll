# Final merge decision

## Decision

**MERGE READY**

The validated SB00-SB09 worktree closes MRG-001 through MRG-011. No production blocker, test failure, project dependency cycle, forbidden source-kind ownership, premature ordinary-conversation activation, stub, or whitespace defect remains.

This decision is bound to:

- branch `maf-refactor`;
- HEAD `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`;
- development and merge base `26da0c55861e5d4e6ca325e561f3f4612aa93266`;
- the source/test hashes in `proof/SB09/changed-file-hashes.txt`;
- final CodeAnalytics snapshot `snap-20260808170209-7c01e0e0`;
- the dirty worktree recorded in `proof/SB09/worktree-state.txt`.

Because no commit was requested, HEAD does not yet contain the validated changes. The source state is merge-ready; an intentional stage/commit is still required before Git can merge or push that state.

## Gate evidence

| Gate | Result | Evidence |
|---|---|---|
| Clean Release build | Pass | `dotnet clean` followed by from-clean solution build; 0 warnings, 0 errors |
| Focused blockers | Pass | 122/122 |
| Exact finding progression | Pass | 12/12 |
| Full Unit | Pass | 5,297 passed, 0 failed, 0 skipped |
| Full Integration | Pass | Six non-overlapping class shards: 723 passed, 0 failed, 1 explicitly environment-gated skip |
| Full Components | Pass | 954 passed, 0 failed, 0 skipped |
| Required smokes | Pass | Canvas/Gantt, mixed approval, workflow LLM, profile switch, runtime state, approval continuation, process lease |
| Original/follow-up/final guards | Pass | Structure, architecture, cutover, dependencies, source ownership, merge blockers |
| CodeAnalytics | Pass | 12 projects, 872 documents, no blocking error, no error diagnostic, no project cycle |
| Anti-stub/non-activation | Pass | No new production partial/stub and no ordinary-conversation product consumer/registration |

## Integration execution note

The monolithic Integration command exceeded 15 minutes without reporting a test failure. Static discovery identified 120 classes; six deterministic, non-overlapping class filters then covered every class exactly once. Runtime data expansion produced 724 outcomes: 723 passed and one live-Ollama skip. This is complete suite coverage, not a reduced filter set.

The skipped test is decorated with an explicit gate requiring `CANDOITALL_RUN_LIVE_OLLAMA_VALIDATION=true`, a running local Ollama endpoint, and a preinstalled model catalog. It never downloads models and is unrelated to the merge-gate implementation.

## Architecture disposition

- The canonical authority parser distinguishes absent legacy metadata from malformed current metadata and fails closed.
- Source-authority policies are module-owned and composed through DI.
- Effective tool-policy context reaches MAF denial, diagnostics, and approval behavior.
- Durable process-lease cleanup resolves the effective run scope.
- File conversation CAS coordinates across store instances and bounds coordinator state.
- Ordinary turn compensation, active-turn fencing, and capacity admission are durable and tested.
- LLM attempt usage is validated, checked, accumulated, and projected through typed failures.
- Ordinary conversations remain a tested opt-in foundation with no production activation.

CodeAnalytics reports only non-blocking size/complexity advisories in cohesive owners, duplicate compiler-generated attribute-name warnings, and expected factory-registration collector ambiguity. Executable guards and tests cover the facts the collector cannot infer.

## Operational follow-up

1. Review and intentionally stage the complete worktree.
2. Commit it on `maf-refactor`.
3. Push and run repository CI with timeouts/sharding suitable for the observed Integration and Components durations.
4. Run the opt-in live Ollama validation only in a provisioned environment.
