# Target Solution

The documentation should present one current contributor path:

- `README.md` is the first-stop public entry point: what the product is, required local services, quick start with PostgreSQL and Qdrant, setup scripts, validation commands, and project family map.
- `docs\README.md` remains the documentation index and points to runtime setup, API control plane, MCP transition notes, shared UI guidance, process operation, and Codex skills.
- `docs\development-runtime.md` contains the deeper local runtime setup and troubleshooting details that would overload the root README.
- Per-project `README.md` files document the local purpose and validation command for each project directory, especially modules introduced or refactored recently.
- Retired MCP guidance remains in transition notes only. Current setup uses HTTP APIs for processes/project-structure and active MCP sidecars for code analytics, components, DotNetWatch, Mermaid, SSH operations, and local runtime helpers.

No runtime code or project structure should change. This is a documentation alignment pass, not a module refactor.
