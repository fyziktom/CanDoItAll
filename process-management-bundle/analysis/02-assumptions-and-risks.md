# Assumptions And Risks

## Working Assumptions

- The first implementation merge will create `CanDoItAll.Modules.Processes` inside the main `CanDoItAll` solution rather than inside a separate repo.
- `CanDoItAll.AgentFramework` remains a future runtime adapter seam, not a compile-time dependency during the first process-module merge.
- Process roles must remain stable when concrete executors change, so the canonical process model will store role requirements and snapshots, not runtime-specific agent objects as the source of truth.
- The existing shared-component stack in `CanDoItAll.Components.BaseLib` and `CanDoItAll.Components.CanvasLib` is the default UI foundation for future process pages.
- `CanDoItAll.IPFS` will be used later through an evidence-storage abstraction instead of being welded directly into the first phase.
- The process MCP can be local stdio over repo-local configuration and the active database profile; it does not need a new remote tokenized agent API in this phase.

## Critical Path Risks

- Dual registries remain the highest architecture risk:
  CRM-HR, Workspace, and AgentFramework already overlap on agent- or provider-adjacent concepts.
- If role requirements are modeled as direct executor bindings too early, the process design will become brittle and staffing changes will rewrite business logic.
- If decision records, trust state, refusal outcomes, and operating modes are treated as “later logging details,” future enterprise governance will require destructive domain rewrites.
- If post-phase repair bundles are skipped, later phases will accumulate technical debt on top of unstable foundations.
- If seeding is postponed until after UI and runtime work starts, validation will drift toward tiny synthetic happy paths and hide real process complexity.
- If the new MCP bypasses `ProcessesService` and re-implements process reads or mutations, the repo will gain a second process contract that drifts from the canonical module.
- If reinstall and Codex config updates are partial, the MCP may build correctly but remain unusable until someone manually repairs `.vscode\mcp.json`, `config.toml`, or the install manifest.

## Validation Risks

- UI work is inherently high-risk because canvas authoring, overlays, dense management surfaces, and process-run views can easily regress spacing, clipping, layering, and component consistency.
- Browser-proof quality can collapse into “the route opened” unless the future execution agent logs route, viewport, Playwright actions, screenshot paths, and actual visual findings.
- Cross-repo convergence work can produce false confidence if it is reasoned about only from legacy bundle prose instead of current repo evidence.
- The current IPFS repo could still reveal integration friction once code-level client adoption is attempted, so the bundle must keep the first-wave contract at the abstraction seam.
- Local MCP proof can still be weak if it stops at unit tests and does not prove real stdio transport startup, tool invocation, and reinstall-script output.

## Reopen Triggers

- Reopen phase 00 if current `CanDoItAll` or `CanDoItAll.AgentFramework` commits materially change provider, agent, capability, or workspace-runtime ownership before execution begins.
- Reopen the relevant foundation phase if implementation introduces any second durable registry for provider profiles, business role templates, AI identities, or capability proof state.
- Reopen the authoring UI phase if future Playwright or screenshot review shows raw layout wrappers, clipped overlays, oversized chrome, or component-library bypasses.
- Reopen runtime foundations if later phases reveal that decision records, artifact trust metadata, refusal outcomes, or operating-mode context are not attributable to specific process runs and steps.
- Reopen the seed baseline if test or Playwright scenarios require ad hoc fixtures outside the planned seed packs.
- Reopen phase07 if process-MCP access depends on a web host being manually started, a hidden hard-coded database profile, or any settings values that the reinstall script does not provision or preserve.
