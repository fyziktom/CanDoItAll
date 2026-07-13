# Implementation Prompt

Implement the selected subbundle only.

Before editing, reopen:

- `bundle://README.md`
- `bundle://plan/01-phase-plan.md`
- the selected `bundle://subbundles/SBxx.../README.md`
- `bundle://traceability/01-requirement-traceability.md`
- `bundle://bundle-checklists.xlsx`

Use the existing repo patterns. Keep the change small and responsibility-focused. Do not add public APIs, interfaces, or new projects unless the subbundle explicitly requires them and dependency direction is proven.

For critical subbundles, capture proof under `proof/SBxx/`:

- failing-first or current-state transcript when behavior changes
- passing command transcript
- source assertions proving production code, not fixtures, enforces the behavior
- changed-file hashes
- anti-stub audit output
- semantic invariants with shallow-pass trap, adversarial negative proof, semantic positive proof, and raw-note literal closure

Stop and mark the subbundle blocked if the progression gate cannot honestly pass.
