# Implementation Prompt

You are implementing this bundle on `maf-processes-refactor`.

Do not start by coding. First re-read:
- inputs/source-artifacts.md
- analysis/01-current-state-review.md
- plan/01-phase-plan.md
- the current branch source files listed in each subbundle.

Hard rules:
- No broad Core runtime extraction.
- No generic driver registry/selector/host/DI/manager/scheduler/workflow hook.
- No shell, package restore, Office/Graph calls, workspace/storage writes, process mutation, claim/transition/finalizer/retry/provider repair.
- No UI/media drift.
- Critical gates require semantic adequacy proof, source assertions, anti-stub audit, changed-file hashes and real test transcripts.

Close each subbundle in order and do not proceed past a gate until the gate is green.
