# 10 — MCP Stdio Path and Configuration Fixes


## Problem

MCP stdio integration tests hardcode a Windows repo root and Debug assembly path.

## Tasks

1. Remove hardcoded `C:epositories\CanDoItAll` constants.
2. Resolve repo root from `AppContext.BaseDirectory`, a test helper, or MSBuild-provided path.
3. Resolve MCP server assembly path from the current test configuration or project reference output.
4. Avoid hardcoded Debug/Release assumptions.
5. Add tests or assertions that paths exist before launching.
6. Categorize stdio process tests as `Integration` and possibly `LiveProcess` if they spawn external processes.

## Acceptance criteria

- Tests run on non-Windows environments where supported.
- Tests run in Release/no-build.
- Failure messages identify missing build artifacts clearly.

