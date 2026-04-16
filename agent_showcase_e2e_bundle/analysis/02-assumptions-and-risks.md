# Assumptions And Risks

## Assumptions

- The “simple calculator” showcase means a small Blazor SSR application with at least add, subtract, multiply, and divide behavior and enough deliverables for design, implementation, and QA handoff.
- The requested database is intended to be used in place, not cloned, so showcase entities must use distinct names and identifiers to avoid colliding with existing user data.
- UI-capable agents should reuse existing agent capability and metadata structures rather than introducing a separate CRM-HR-only tool-binding store.
- Browser proof may use existing Playwright test infrastructure plus targeted MCP/browser sessions when interactive verification is needed.

## Critical Path Risks

- If CRM-HR keeps its own agent registry semantics, the showcase will fail later when process runtime tries to source agents through CRM-HR and sees a different inventory than the dedicated agent workspace.
- If the template-driven provisioning path is weak or incomplete, the live showcase will be forced back onto hardcoded seeding logic, which violates the request and creates future maintenance debt.
- If project-structure/process-runtime progress updates are only partially wired, the live run could appear successful in process detail views while leaving the project canvas stale, which would be a false pass.

## Validation Risks

- Clipboard behavior in component tests may need JS interop stubbing rather than true OS clipboard verification.
- The requested database may already contain partial showcase entities from previous runs, so idempotency and duplicate detection matter.
- A full end-to-end showcase can surface latent bugs in multiple modules. Time spent on live-run fixes is expected and should be recorded as part of execution, not treated as scope creep.

## Reopen Triggers

- Reopen subbundle 01 if any later process or CRM-HR search flow still sees a different agent count than the dedicated Agents module.
- Reopen subbundle 02 if any live run shows the Processes workspace or database dialog regressing in browser containment or discoverability.
- Reopen subbundle 03 if the showcase provisioning path requires adding hardcoded process definitions, roles, or agent lists outside the template-driven projection path.
- Reopen subbundle 04 if the final live run leaves unfinished handoffs, missing artifacts, stale project-structure progress, or unreviewed browser proof.
