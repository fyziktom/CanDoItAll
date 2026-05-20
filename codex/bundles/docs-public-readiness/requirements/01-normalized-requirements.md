# Normalized Requirements

| Id | Requirement | Source note | Owning subbundle | Proof |
| --- | --- | --- | --- | --- |
| `REQ-001` | Update the root and docs indexes so a new contributor can understand the current modular architecture and find setup, API, MCP, skill, and project-level docs. | `N001`, `N005` | `01-doc-inventory-and-target-structure` | Markdown diff review plus source references in updated docs. |
| `REQ-002` | Document PostgreSQL-first local development, Docker Compose setup, native PostgreSQL setup, Qdrant ports/configuration, and runtime readiness checks in the main README. | `N002` | `02-runtime-installation-and-script-docs` | README commands match `docker-compose.yml`, appsettings, launch settings, and scripts. |
| `REQ-003` | Document `tools\Install-CanDoItAllWebApp.ps1`, `tools\Reinstall-CanDoItAllMcps.ps1`, and `codex\scripts\install-candoitall-skills.ps1` in public-facing setup docs. | `N003` | `02-runtime-installation-and-script-docs` | README and docs index mention all three scripts with purpose and safe usage boundaries. |
| `REQ-004` | Add missing project READMEs for every tracked `.csproj` directory lacking one. | `N004` | `03-project-readme-coverage` | Coverage check reports `MissingReadmes=0`. |
| `REQ-005` | Ensure new/refactored modules have current descriptions of responsibility, integration points, and validation commands. | `N001`, `N004` | `03-project-readme-coverage` | New READMEs cover Cognitive Memory, Scheduler Planner, Plugins, Voice, Charts, Mermaid, document tools, bundled plugins, and Mermaid MCP tests. |
| `REQ-006` | Keep retired Processes and ProjectStructure MCP guidance out of active setup instructions. | `N005` | `04-validation-and-closure` | Search/review confirms active setup points to HTTP APIs and current MCP sidecars only. |
