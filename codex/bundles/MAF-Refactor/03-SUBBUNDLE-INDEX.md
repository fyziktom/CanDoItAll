# Subbundle index

| # | ID | Phase | Depends on | Checkpoint |
|---:|---|---|---|---|
| 1 | `SB00-current-state-characterization` — Current-state characterization and dependency baseline | A — Evidence and context foundation | None | No |
| 2 | `SB01-canonical-context-contracts` — Canonical context, transition, authority, and runtime-state contracts | A — Evidence and context foundation | `SB00-current-state-characterization` | No |
| 3 | `SB02-turn-context-capture-and-authority-resolution` — Turn-context capture and canonical authority resolution | A — Evidence and context foundation | `SB01-canonical-context-contracts` | No |
| 4 | `SB03-floating-conversation-affinity-and-transitions` — Floating conversation affinity, context epochs, and transitions | A — Evidence and context foundation | `SB02-turn-context-capture-and-authority-resolution` | No |
| 5 | `SB04-project-structure-gantt-observation` — Project Structure and Gantt observation contributors | A — Evidence and context foundation | `SB03-floating-conversation-affinity-and-transitions` | No |
| 6 | `SB05-context-foundation-checkpoint` — Checkpoint: context, affinity, Gantt, and authority foundation | A — Evidence and context foundation | `SB03-floating-conversation-affinity-and-transitions`, `SB04-project-structure-gantt-observation` | Yes |
| 7 | `SB06-workspace-execution-scope-and-services-factory` — Workspace execution scope and scope-bound services factory | B — Scope and construction integrity | `SB05-context-foundation-checkpoint` | No |
| 8 | `SB07-service-locator-and-parallel-graph-removal` — Remove service location, fallbacks, and mixed manual/DI runtime graphs | B — Scope and construction integrity | `SB06-workspace-execution-scope-and-services-factory` | No |
| 9 | `SB08-scope-and-composition-checkpoint` — Checkpoint: scope identity and composition integrity | B — Scope and construction integrity | `SB06-workspace-execution-scope-and-services-factory`, `SB07-service-locator-and-parallel-graph-removal` | Yes |
| 10 | `SB09-agent-runtime-port-split` — Split the broad agent runtime into SDK-free narrow ports | C — Runtime port split | `SB08-scope-and-composition-checkpoint` | No |
| 11 | `SB10-maf-adapter-decomposition` — Decompose MAF implementation behind the narrow ports | C — Runtime port split | `SB09-agent-runtime-port-split` | No |
| 12 | `SB11-runtime-split-checkpoint` — Checkpoint: runtime ports and MAF adapter decomposition | C — Runtime port split | `SB09-agent-runtime-port-split`, `SB10-maf-adapter-decomposition` | Yes |
| 13 | `SB12-maf-dependency-graph-repair` — Repair MAF compile-time dependency direction | D — Dependency direction and process ownership | `SB11-runtime-split-checkpoint` | No |
| 14 | `SB13-process-semantics-and-recovery-extraction` — Extract process semantics, provider policy, and artifact recovery from MAF/generic runtime | D — Dependency direction and process ownership | `SB12-maf-dependency-graph-repair` | No |
| 15 | `SB14-process-boundary-checkpoint` — Checkpoint: MAF dependency direction and process ownership | D — Dependency direction and process ownership | `SB12-maf-dependency-graph-repair`, `SB13-process-semantics-and-recovery-extraction` | Yes |
| 16 | `SB15-versioned-runtime-state-and-continuation` — Versioned runtime state, per-proposal continuation, and context compatibility | E — Continuation and runtime-state compatibility | `SB14-process-boundary-checkpoint` | No |
| 17 | `SB16-lightweight-llm-invocation-foundation` — Provider-backed lightweight LLM invocation and future ordinary-chat foundation | E — Continuation and lightweight inference | `SB11-runtime-split-checkpoint`, `SB15-versioned-runtime-state-and-continuation` | No |
| 18 | `SB17-cross-cutting-cutover-stabilization-and-bugfixing` — Cross-cutting cutover stabilization, fault injection, and owner-boundary bugfixing | F — Cross-cutting stabilization and release | `SB15-versioned-runtime-state-and-continuation`, `SB16-lightweight-llm-invocation-foundation` | Yes |
| 19 | `SB18-final-cleanup-and-release-gate` — Final cleanup, deletion, full validation, and release architecture gate | F — Cross-cutting stabilization and release | `SB17-cross-cutting-cutover-stabilization-and-bugfixing` | Yes |

Each subbundle contains a README, Claude Code execution prompt, proof manifest template, and durable handoff template.
