# Execution order

## Phase A — Evidence and context foundation

1. `SB00-current-state-characterization`
2. `SB01-canonical-context-contracts`
3. `SB02-turn-context-capture-and-authority-resolution`
4. `SB03-floating-conversation-affinity-and-transitions`
5. `SB04-project-structure-gantt-observation`
6. `SB05-context-foundation-checkpoint`

## Phase B — Scope and construction integrity

7. `SB06-workspace-execution-scope-and-services-factory`
8. `SB07-service-locator-and-parallel-graph-removal`
9. `SB08-scope-and-composition-checkpoint`

## Phase C — Runtime port split

10. `SB09-agent-runtime-port-split`
11. `SB10-maf-adapter-decomposition`
12. `SB11-runtime-split-checkpoint`

## Phase D — Dependency direction and process ownership

13. `SB12-maf-dependency-graph-repair`
14. `SB13-process-semantics-and-recovery-extraction`
15. `SB14-process-boundary-checkpoint`

## Phase E — Continuation and lightweight inference

16. `SB15-versioned-runtime-state-and-continuation`
17. `SB16-lightweight-llm-invocation-foundation`

## Phase F — Cross-cutting stabilization and release

18. `SB17-cross-cutting-cutover-stabilization-and-bugfixing`
19. `SB18-final-cleanup-and-release-gate`

## Unlock rules

A checkpoint or stabilization decision must state:

- `Unlocked` / `Ready for cleanup`;
- `Blocked`;
- `Unlocked with bounded follow-up` / `Ready with named compatibility readers retained`.

No phase may be unlocked with an authority, source-of-truth, dependency, scope, persistence, single-path, or testability blocker. A passing build alone is not an unlock.
