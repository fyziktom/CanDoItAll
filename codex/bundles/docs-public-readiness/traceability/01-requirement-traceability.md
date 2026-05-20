# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `N001` / `REQ-001`, `REQ-005` | `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md`, `inventories/01-scope-inventory.md` | `subbundles/01-01-doc-inventory-and-target-structure`, `subbundles/03-03-project-readme-coverage` | Documentation diff review and project README coverage check. | New/refactored modules missing docs are explicitly inventoried. |
| `N002` / `REQ-002` | `architecture/01-target-solution.md`, `plan/01-phase-plan.md` | `subbundles/02-02-runtime-installation-and-script-docs` | README/runtime docs match Docker Compose, appsettings, and launch settings. | PostgreSQL-first and Qdrant setup must be visible from root README. |
| `N003` / `REQ-003` | `inputs/01-source-artifacts.md`, `requirements/01-normalized-requirements.md` | `subbundles/02-02-runtime-installation-and-script-docs` | Root README and docs index mention all three scripts with purpose and safe usage boundaries. | Commands must point at real script paths. |
| `N004` / `REQ-004` | `inventories/01-scope-inventory.md` | `subbundles/03-03-project-readme-coverage` | PowerShell coverage check reports `MissingReadmes=0`. | Applies to tracked `.csproj` directories under `src`, `tests`, and `tools`. |
| `N005` / `REQ-006` | `architecture/01-target-solution.md`, `reviews/01-execution-report.md` | `subbundles/04-04-validation-and-closure` | Search/review confirms retired MCP setup commands are not active guidance. | Historical transition docs may remain. |
