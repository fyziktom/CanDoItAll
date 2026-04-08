# Stop conditions

Do not claim bundle12 complete if any of the following is true:

- the current repo still fails the phase10 gate,
- the current repo still fails the phase11 gate,
- `ProjectStructureAssemblyService.LoadAsync(...)` still mutates persistence,
- `ProjectStructureProjectionMaintenanceService` is missing,
- unknown-manifest shared editor proof tests are missing,
- there is no Quartz integration,
- there are no hosted workers,
- there is no durable internal message plane,
- there is no plugin ingress inbox/cursor boundary,
- execution observability records are still missing.
