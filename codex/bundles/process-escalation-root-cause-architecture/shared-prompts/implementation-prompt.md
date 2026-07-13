# Implementation Prompt

Implement only the assigned subbundle from `codex/bundles/process-escalation-root-cause-architecture`.

Before editing:

- Read the root README, `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md`, all architecture files, `plan/01-phase-plan.md`, and the assigned subbundle README.
- Confirm prerequisites and progression gate from earlier subbundles.
- Run or add characterization tests first when the subbundle asks for them.

Hard constraints:

- Keep generic process runtime, dispatcher, projection, and common MAF workspace code domain-neutral.
- Do not add .NET, Blazor, Calculator, Tetris, screenshot, or Playwright-specific behavior to generic layers.
- Do not use prompt text as the only enforcement mechanism for tools, MCPs, skills, suppressions, or proof gates.
- Do not add silent fallback. Recovery must record a typed failure category and decision.
- Use strongly typed contracts, enums, value objects, or descriptors instead of new magic-string protocols.
- Avoid heavy per-step object graph construction; prefer reusable catalogs plus immutable per-step context.

Implementation style:

- Make the smallest correct change for the subbundle.
- Extract top-level types or collaborators when they improve testability; do not hide growth in new partial files.
- Keep domain-specific .NET/software-delivery behavior in process drivers, templates, or module composition.
- Update `reviews/01-execution-report.md` with commands, tests, proof, and gate decisions.

Stop conditions:

- A required prerequisite is not met.
- The change would require domain behavior in generic runtime.
- A blocked run still cannot expose typed diagnostics after SB01.
- A readiness rule can only be enforced by prompt wording after SB02.
