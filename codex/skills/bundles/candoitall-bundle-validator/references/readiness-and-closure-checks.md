# Readiness And Closure Checks

## Readiness Gate

Confirm all of these before execution starts:

- raw inputs and source artifacts are preserved
- traceability maps every input to a bundle destination and owning subbundle
- `plan/01-phase-plan.md` contains a usable mermaid dependency map
- critical foundations and phase gates are explicit
- every subbundle has prerequisites, dependency impact, validation depth, and progression gate sections
- every subbundle declares a `Standard`, `Behavioral`, or `Governed` proof tier
- each UI subbundle records its primary surface, supporting content, stats treatment, list/editor organization, textarea/dialog sizing rationale, first-viewport target, and scroll owner
- relevant normal-state and open-overlay screenshot reviews are planned
- affected compound controls have a proof case inside representative narrow grid, card, rail, or dialog columns, including on a wide desktop page
- readiness validation passes with `scripts/validate_bundle.py --stage prepared`

For a compatible non-canonical shape, replace the structural script result with a recorded manual semantic readiness gate.

## Final Closure Gate

Confirm all of these before the bundle is finished:

- every executed subbundle is `Completed` or honestly `Blocked`
- `## Subbundle Gate Results` and `## Browser Validation Analytics` are populated and no longer pending
- each UI execution report records the compact composition review, first-viewport and scroll-owner findings, and inspected open-overlay screenshot findings
- affected compound controls were inspected in realistic constrained containers and shown to respond to containing width rather than viewport width alone
- raw note closure rows are populated and no longer pending
- the root `README.md` validation summary matches reality
- final validation passes with `scripts/validate_bundle.py --stage completed`, including proof-depth checks for completed critical subbundles
- any proof gap that matters to user-visible behavior has reopened the affected subbundle instead of being hidden in residual risks
- Governed artifacts are required only from Governed subbundles; Behavioral proof still includes realistic positive and meaningful negative evidence
