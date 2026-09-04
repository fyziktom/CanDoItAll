# Architecture checkpoints

## Checkpoint A — after SB02

- one typed workspace-state owner exists;
- current URL output is unchanged;
- direct EF and multi-source overview orchestration left Razor;
- `IAgentsOverviewQuery` is cohesive and directly testable;
- no new route or partial was added.

Failure invalidates SB03–SB07.

## Checkpoint B — after SB03

- catalog component consumes state/emits intents;
- catalog component has no feature DI and no dialogs/chat/mutations;
- page owns selected IDs and detail/team/chat host actions;
- controller owns catalog load/repair/mutations, not navigation;
- no duplicate old/new catalog state machine remains.

Failure invalidates SB04–SB07.

## Checkpoint C — after SB05

- stable details section/session is public;
- dialog external I/O goes only through editor controller;
- dialog has no forbidden direct dependencies;
- controller owns save canonicalization and command workflows;
- controller is not a forwarding service bag;
- existing error/result semantics remain.

Failure invalidates SB06–SB07.

## Checkpoint D — after SB06

- behavior tests use public seams;
- target test files contain no private reflection/uninitialized service workaround;
- direct controller/state tests exist;
- durable dependency guard checks forbidden categories only;
- no test freezes file/member/count/source syntax.

Failure invalidates closure.

## Final architecture gate

Use `reviews/csharp-architecture-gate.md`. Closure is blocked by hidden dependencies,
duplicate state ownership, controller/service-bag growth, new partial/project references,
or sandbox construction requiring the full production runtime for the target components.
