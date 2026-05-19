# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Execute P0 maintainability phase. | `requirements/01-normalized-requirements.md` | all subbundles | Targeted tests/build and docs update. | User asked to prepare, execute, validate/test. |
| Split oversized surfaces. | `subbundles/01-refactor-oversized-surfaces` | `01-refactor-oversized-surfaces` | Build, component test, and browser proof. | Advanced/API/Recall/Page/ReviewUi surfaces split; broad older-service decomposition moves to beta hardening. |
| Projection rebuild explicit path. | `subbundles/02-projection-rebuild-and-scheduled-automation` | `02-projection-rebuild-and-scheduled-automation` | Unit/integration tests plus adapter-backed projection proof. | Must not make projection canonical. |
| Scheduled automation execution. | `subbundles/02-projection-rebuild-and-scheduled-automation` | `02-projection-rebuild-and-scheduled-automation` | Unit/integration/component/browser tests. | Must produce explicit run summaries; P0 intentionally keeps execution UI/API-triggered. |
| Agent context DTO/policy. | `subbundles/03-agent-context-policy-and-dtos` | `03-agent-context-policy-and-dtos` | Unit tests. | Process-critical paths must fail predictably. |
| Docs/roadmap update. | `subbundles/04-docs-validation-and-closure` | `04-docs-validation-and-closure` | Docs diff and bundle closure. | Must be based on final source state. |
