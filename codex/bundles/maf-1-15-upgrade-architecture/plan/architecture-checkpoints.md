# Architecture Checkpoints

## A1 — Baseline and Fixture Gate

Must be true before package edits:

- branch drift classified;
- full MAF/Harness/AG-UI/declarative search complete;
- package graph captured;
- current build/test failures classified;
- 1.13 chat, approval, handoff, attachment, provider-history, and checkpoint fixtures captured where applicable;
- file-tool inventory captured;
- state-store rollback snapshot documented.

Failure action: stop. Do not update packages.

## A2 — Approval Security and State Gate

Must be true before handoff/session cleanup:

- 1.15 binding is proven active for every provider path;
- parity mixed-call behavior is explicit;
- decision admission is bound to the complete current server-held pending snapshot;
- stable request/call IDs and atomic session/snapshot persistence are proven;
- random ID fallback removed;
- native 1.15 restart continuation works;
- forged/substituted/replayed/cross-session tests pass;
- legacy 1.13/incompatible state has a tested drain/reissue outcome;
- attachment scrub preserves binding state.

Failure action: stop mutation-capable rollout.

## A3 — Runtime Semantics Gate

Must be true before file/A2A closure:

- terminal handoff output is authoritative on full streaming runtime;
- intermediate activity remains visible but not machine output;
- tool/result and reasoning/text ordering pass;
- history and returned response contract agree;
- handoff depth remains enforced;
- chat sessions and active workflow checkpoints have explicit cross-version outcomes;
- session persistence failures are diagnosable;
- finalizer semantics remain correct.

Failure action: do not remove merge/session workarounds or deploy.

## A4 — Final Closure Gate

Must be true:

- file/capability security regression passes;
- A2A smoke passes;
- package graph has one intended release train;
- optional feature register complete;
- warning suppressions narrowed and justified;
- full build/test and real provider validation pass;
- canary and rollback rehearsal pass;
- legacy approval backlog is drained or reissued and no reconstruction bridge exists;
- execution report maps every requirement to proof;
- no hidden architecture exception remains.
