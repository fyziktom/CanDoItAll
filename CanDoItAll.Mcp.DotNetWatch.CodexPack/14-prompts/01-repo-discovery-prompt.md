# Repo discovery prompt

Inspect the `CanDoItAll` repository and identify the concrete integration points needed by the MCP server.

## Tasks
1. Find the solution file.
2. Find the primary startup project for the local development app.
3. Find or infer the preferred health endpoint and exposed URLs.
4. Find test projects relevant to the main app.
5. Detect whether the solution uses `Directory.Packages.props` or per-project package references.
6. Detect whether there are existing utilities for process execution, logging, health checks, or CLI orchestration that can be reused.
7. Find directories that should be excluded from `dotnet watch`, such as `.mcp-state`, `playwright-report`, `TestResults`, and coverage folders.

## Output format
Produce:
- a concise table of discovered paths
- a list of assumptions that are still unresolved
- the proposed location of the new server project and its test projects
- a recommended initial settings file for this repo

## Constraints
- Do not implement anything yet.
- Do not guess silently. If something is missing, state the gap explicitly.
- Prefer reading the repo structure over inventing conventions.
