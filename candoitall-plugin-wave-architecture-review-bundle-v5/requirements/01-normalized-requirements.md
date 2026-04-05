# Normalized Requirements

- `R-001` Determine whether the current post-refactor codebase is strong enough to support the next plugin wave. -> analysis/04-plugin-wave-readiness.md
- `R-002` Identify all architectural weaknesses that would materially limit email, LinkedIn, and custom API plugins. -> analysis/03-detailed-findings.md
- `R-003` Respect the product direction that node remains the universal carrier and that X/Y plus semantic markers stay canonical data. -> architecture/01-target-solution.md; architecture/02-node-carrier-and-facet-model.md
- `R-004` Produce an execution-grade bundle for Codex using the same bundle style already present in the repository. -> README.md; subbundles/*
- `R-005` Preserve the already-repaired CRM/HR canonical ownership improvements unless a stronger mechanism replaces them. -> analysis/05-fixed-and-improved-areas.md; subbundles/04-plugin-platform-and-cross-module-seams/README.md
- `R-006` Provide honest limitations for runtime validation in this environment. -> analysis/02-assumptions-and-risks.md; reviews/01-execution-report.md
- `R-007` Give exact source references, acceptance criteria, and proof expectations so another agent can execute the refactor safely. -> subbundles/*; traceability/01-requirement-traceability.md
