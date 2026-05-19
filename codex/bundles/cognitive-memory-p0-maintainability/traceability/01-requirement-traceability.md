# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Execute P0 maintainability phase. | `requirements/01-normalized-requirements.md` | all subbundles | Targeted tests/build and docs update. | User asked to prepare, execute, validate/test. |
| Split oversized surfaces. | `subbundles/01-refactor-oversized-surfaces` | `01-refactor-oversized-surfaces` | Build and targeted tests. | Advanced/API/Recall/Page as risk allows. |
| Projection rebuild explicit path. | `subbundles/02-projection-rebuild-and-scheduled-automation` | `02-projection-rebuild-and-scheduled-automation` | Unit/integration tests. | Must not make projection canonical. |
| Scheduled automation execution. | `subbundles/02-projection-rebuild-and-scheduled-automation` | `02-projection-rebuild-and-scheduled-automation` | Unit/integration tests. | Must produce explicit run summaries. |
| Agent context DTO/policy. | `subbundles/03-agent-context-policy-and-dtos` | `03-agent-context-policy-and-dtos` | Unit tests. | Process-critical paths must fail predictably. |
| Docs/roadmap update. | `subbundles/04-docs-validation-and-closure` | `04-docs-validation-and-closure` | Docs diff and bundle closure. | Must be based on final source state. |
