# Readiness And Closure Checks

## Readiness Gate

Confirm all of these before execution starts:

- raw inputs and source artifacts are preserved
- traceability maps every input to a bundle destination and owning subbundle
- `plan/01-phase-plan.md` contains a usable mermaid dependency map
- critical foundations and phase gates are explicit
- every subbundle has prerequisites, dependency impact, validation depth, and progression gate sections
- readiness validation passes with `scripts/validate_bundle.py --stage prepared`

## Final Closure Gate

Confirm all of these before the bundle is finished:

- every executed subbundle is `Completed` or honestly `Blocked`
- `## Subbundle Gate Results` and `## Browser Validation Analytics` are populated and no longer pending
- raw note closure rows are populated and no longer pending
- the root `README.md` validation summary matches reality
- final validation passes with `scripts/validate_bundle.py --stage completed`, including proof-depth checks for completed critical subbundles
- any proof gap that matters to user-visible behavior has reopened the affected subbundle instead of being hidden in residual risks
