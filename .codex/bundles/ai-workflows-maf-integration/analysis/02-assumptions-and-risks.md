# Assumptions And Risks

## Working Assumptions

- The implementation agent can use `C:\repositories\agent-framework` as a local source reference without fetching external docs.
- Workflows belong to the AgentFramework product area, but their definition, run, settings, and test models are distinct from agents and processes.
- The initial workflow execution environment can be MAF in-process execution if subbundle 01 proves it supports required parallelism, streaming events, external requests, cancellation, and checkpoint semantics.
- MAF declarative YAML support is useful reference material, but the CanDoItAll canonical model should remain strongly typed unless architecture review accepts a declarative-first design.
- UI work should reuse existing Blazor module patterns, CanvasLib, and project component conventions rather than introducing a new frontend stack.

## Critical Path Risks

- If the wrapper model leaks raw MAF runtime types into persistence or APIs, future MAF package changes can force database or API breaking changes.
- If workflow runs are treated as process runs, the process layer will absorb lower-level workflow concerns and become harder to evolve.
- If process role executor kind remains string-based, adding workflows will spread fragile branching and make future executor kinds risky.
- If MAF workflow instances are reused incorrectly, executor ownership and `AllowConcurrent` constraints can cause unsafe concurrency or subtle runtime failures.
- If human-in-loop and checkpoints are not modeled as durable workflow runtime state, browser refreshes, restarts, or resumed runs can lose pending requests.

## Validation Risks

- Unit tests alone will not prove streaming run behavior; subbundle 02 must include runtime smoke/integration proof with events, cancellation, and resume.
- Build success alone will not prove UI usability; UI subbundles require large-screen and narrower-width browser proof with screenshots and review notes.
- API endpoint tests alone will not prove process integration; subbundle 06 must launch or simulate a process role assignment resolved to a workflow run.
- Architecture review cannot be a verbal note; each review must document findings, accepted tradeoffs, follow-up edits, and whether the next phase may proceed.

## Reopen Triggers

- Reopen subbundle 01 if any later subbundle needs to add a new workflow primitive not represented by the wrapper model.
- Reopen subbundle 02 if UI or process integration requires run states, event types, checkpoint states, or external request flows that the runtime manager cannot represent.
- Reopen subbundle 03 if canvas or API work needs workflow definition fields not covered by catalog/settings/test models.
- Reopen subbundle 06 if process integration requires changing process runtime semantics instead of only adding a typed workflow executor option.
- Reopen the full bundle if MAF package version or source behavior differs materially from `Microsoft.Agents.AI.Workflows` version `1.3.0` observed in the current wrapper project.
