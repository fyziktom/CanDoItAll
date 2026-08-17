# Preparation review

## Verdict

Ready for phased execution.

## Completeness

- [x] Raw request saved and interpreted.
- [x] Current source, projects, callers, DI, routes, persistence, usage, UI, and tests inspected.
- [x] SharedInfo standards resolved at an exact commit.
- [x] Fresh scoped CodeAnalytics evidence recorded.
- [x] Requirements and non-goals normalized.
- [x] Findings and risks assigned.
- [x] Exact target boundary/dependency map recorded.
- [x] Pattern selection and rejected alternatives recorded.
- [x] Testability and partial-class policies recorded.
- [x] Data/cost/migration semantics recorded.
- [x] UI composition and component reuse contract recorded.
- [x] Eleven dependency-ordered subbundles prepared.
- [x] Every requirement has an owner and proof target.
- [x] Focused tests, expected discovery, invalidation, checkpoints, and one-shot broad gate recorded.
- [x] Final Playwright MCP Agent/Simple Chat main/floating and cost-scope closure is mandatory.
- [x] No product/test implementation performed during preparation.

## Critical reviewer conclusions

- A direct merge into Modules.AgentFramework is rejected.
- The current core project is not actually light by responsibility; Core and Application require separate target projects.
- Runtime and Persistence must separate despite both currently living in Persistence.
- Cross-store read composition is the smallest correct cost architecture.
- New invocation pricing provenance is required for trustworthy future costs.
- Legacy cost cannot be made exact and must remain explicitly unpriced.
- Component transport failure is documented and retried at the owning UI phases.

## Activation

Execute SB01 only. Do not start SB02 until CP0 accepts the actual execution baseline and project/data contract.

