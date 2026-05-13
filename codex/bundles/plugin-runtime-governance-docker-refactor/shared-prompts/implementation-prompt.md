# Implementation Prompt

```text
Implement only the assigned subbundle from C:\repositories\CanDoItAll\codex\bundles\plugin-runtime-governance-docker-refactor.

Rules:
- Do not widen scope beyond the subbundle.
- Preserve generic plugin runtime boundaries. Docker is a sample pressure test, not a core-specific architecture.
- Keep install/enable state separate from runtime grants.
- Do not expose raw IServiceProvider, raw shell, raw PowerShell, raw IWorkspaceCommandExecutionService, or unrestricted filesystem access to plugins.
- Use strongly typed ids, enums, options, and records. Avoid magic strings for command, capability, grant, and recipe identifiers.
- Fail explicitly when a capability, grant, connection, recipe, or policy is missing.
- Add targeted tests proportional to the subbundle risk.
- Update reviews/01-execution-report.md with commands, proof, gate status, and browser analytics when applicable.
- Stop and report a blocker if the subbundle progression gate cannot honestly pass.
```
