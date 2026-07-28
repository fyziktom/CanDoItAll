# DotNetWatch Development Integration

DotNetWatch is a development-sidecar supplied by the sibling
[CanDoItAll.Mcp repository](https://github.com/fyziktom/CanDoItAll.Mcp). Its persistent
backend can keep watched applications, logs, and runtime operations alive when an MCP
stdio client reconnects. It is development tooling, not part of the CanDoItAll web
application runtime.

## Install Or Refresh

From this repository root, run:

```powershell
.\tools\Reinstall-CanDoItAllMcps.ps1
```

The resetup script builds the sibling MCP projects, prepares the DotNetWatch shadow
artifact and Windows tray application, and updates supported local MCP configuration.
It requires `CanDoItAll.Mcp` and `CanDoItAll.CodeAnalysis` as siblings. The default
skill-sync path also requires `CanDoItAll.SharedInfo`; pass `-SharedInfoRepoRoot` when
it lives elsewhere or `-SkipSkillSync` when intentionally omitting skill
synchronization.

The repository-specific runtime settings are in
`CanDoItAll.Mcp.DotNetWatch.settings.json`. MCP source, backend behavior, tray behavior,
and integration tests remain owned by `CanDoItAll.Mcp`.

## Operational Rules

- Use the MCP status and log operations before starting another watched application.
- Treat backend and session identifiers as runtime data; do not commit generated state,
  logs, process ids, or shadow artifacts.
- Keep raw logs for diagnosis and use the bounded agent-oriented log view for normal
  iteration.
- Distinguish a healthy watch session from browser static-asset caching before
  restarting the backend.
- Use a separate artifacts path when rebuilding the live MCP server itself on Windows,
  because the persistent backend may hold its normal output assemblies open.

## Validation

The stable repository gate excludes quarantined live-process cases. See
[Testing](testing.md) for the current command and quarantine policy. Run the
environment-dependent DotNetWatch integration suite from the sibling MCP repository
when changing the backend or wrapper.
