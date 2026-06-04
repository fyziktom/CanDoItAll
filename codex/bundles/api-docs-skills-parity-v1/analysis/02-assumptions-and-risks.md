# Assumptions And Risks

## Assumptions

- The C# route registrations and DTO types are the source of truth unless implementation finds an API route that is dead or intentionally hidden.
- Exact route coverage in docs/skills is a proxy for drift detection, not a complete quality measure; subbundles must still review prose examples and DTO semantics.
- Active skill copies under `C:\Users\lucys\.codex\skills` must be updated after repo skill edits because agents may use the active root.
- Project and Plugin API skill coverage is not automatically required, but the bundle must record a decision because those surfaces expose HTTP routes.

## Critical Path Risks

- SB01 is the primary critical path; if the source route or DTO inventory is wrong, every downstream docs, skills, and guardrail repair can be wrong.
- SB02 blocks claims about current API contract coverage because OpenAPI/tests may not expose routes that source registers.
- SB03 blocks skill wording that says agents can perform process/project-structure actions directly; missing tools must be implemented or documented as HTTP-only.
- SB05 depends on SB04 and SB03 decisions so skills do not preserve obsolete route assumptions.

## Validation Risks

- Integration tests may require local test database/runtime configuration; if blocked, record the exact blocker and add a focused source-level proof, but do not mark behavioral closure complete.
- The workbook route coverage booleans are exact-route text checks and can undercount useful docs; use them as drift signals, not as the only review.
- Adding runtime tools affects security boundaries and approval policies; tests must include policy constants and approval behavior, not only descriptor creation.
- Browser validation is usually not applicable, but any Blazor UI change must add browser proof.

## Reopen Triggers

- A route count changes after SB01 without regenerating the workbook.
- OpenAPI output disagrees with source route registrations after SB02.
- A skill claims direct agent-tool support for an operation that SB03 did not implement or explicitly mark HTTP-only.
- Repo skill and active local skill hashes differ after SB05.
- Final validation finds any raw request element not traced to a subbundle, proof artifact, or explicit exception.
