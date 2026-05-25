# Shared Implementation Prompt

Implement the selected subbundle in production code.

Rules:
- Keep the process core generic.
- Do not hardcode software-delivery-only behavior into generic process services.
- Prefer typed persisted data over keyword heuristics.
- Add failing-first or red-team tests before production changes when feasible.
- Update proof manifests and semantic invariants.
- Add source assertions with repo paths.
- Run focused tests after each subbundle.
