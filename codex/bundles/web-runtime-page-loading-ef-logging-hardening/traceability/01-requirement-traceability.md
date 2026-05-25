# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `REQ-PROC-001` Processes page initial load is slow. | `requirements/01-normalized-requirements.md` | `subbundles/02-sb02-processes-lazy-loading` | Component tests covering initial load and tab/dialog deferred loading. | Initial visible page should not fetch hidden-tab data. |
| `REQ-PROJ-001` Project-structure add-node appears late. | `requirements/01-normalized-requirements.md` | `subbundles/03-sb03-project-structure-mutation-latency` | Component test proving node appears and normal create path avoids full reload. | Preserve persisted move/link behavior. |
| `REQ-WF-001` Workflows page loads templates/catalog too early. | `requirements/01-normalized-requirements.md` | `subbundles/04-sb04-workflows-template-loading` | Component tests proving no init seed/catalog call and tab-triggered component loading. | Background warmup remains out of page-init path. |
| `REQ-EF-001` EF console output must be configurable and default off. | `requirements/01-normalized-requirements.md` | `subbundles/05-sb05-ef-console-logging-option-and-final-validation` | Unit tests for option defaults/binding plus web build/startup. | Option belongs under `Database`. |
