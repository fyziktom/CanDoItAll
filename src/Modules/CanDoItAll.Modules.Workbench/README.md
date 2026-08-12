# CanDoItAll.Modules.Workbench

## Purpose

Product module for workbench views, projections, canvas state, and user workspace orchestration.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Workbench.csproj](CanDoItAll.Modules.Workbench.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

Processes.Application owns process-run root semantics through `ProcessRunArtifactRootPolicy`. Workbench consumes its typed resolution when projecting current-run managed roots, collapses artifact evidence under `artifacts/.../process-runs/{runId}` to the run artifact folder, and collapses generated or external-delivery output persisted under `output/.../process-runs/{runId}/{productRoot}` to the product folder. Wrong-run, dated receipt, absolute, traversal, or otherwise unanchored paths are ignored instead of mirroring noisy artifact subtrees. Raw `external-target/...` aliases remain Processes grounding metadata; Workbench projects the managed output root that records the run-owned delivery evidence.

### Runtime node execution and terminal presentation

Project Structure runtime nodes compile to typed executable, argument, environment,
working-directory, and target values. Direct execution uses the owned workspace process
host and does not require a terminal. PowerShell and POSIX shell nodes remain explicit
script modes; they are not fallbacks for .NET, Docker, Python, Node, or other ordinary
runtime nodes.

Interactive terminal presentation is optional. Windows enables its PowerShell
presentation adapter by default. Linux and macOS require an explicit executable and
argument prefix under `Workbench:RuntimePresentation`; a headless host can leave these
values empty without preventing startup or direct execution. For example:

```json
{
  "Workbench": {
    "RuntimePresentation": {
      "EnableWindowsTerminal": true,
      "LinuxTerminalExecutable": "/usr/bin/x-terminal-emulator",
      "LinuxTerminalArgumentPrefix": ["-e"],
      "MacOsTerminalExecutable": "",
      "MacOsTerminalArgumentPrefix": []
    }
  }
}
```

The configured prefix must make the selected terminal treat the following typed
runtime executable and arguments as its child command. Linux/macOS elevation remains
unavailable by default; Workbench does not add `sudo`, `pkexec`, AppleScript, or a
password-prompt fallback. Windows elevated launch is a separate, explicit `runas`
capability.

### Project Structure Agent Invocation Snapshot

The ready Project Structure chat-context provider publishes a typed
`ProjectStructureInvocationSnapshot` copied from the surface already loaded by the UI.
It retains no component, tracked entity, service, or mutable domain object. The
snapshot is bounded to 512 nodes and 1,024 links, includes explicit coverage and
omissions, carries database-profile generation plus deterministic fingerprints, and
expires after five minutes.

`ProjectStructureReadRequest.Source` is a typed three-way policy:

- `ContextDefault` selects the invocation snapshot only for eligible interactive
  Project Structure chat and otherwise selects canonical current data.
- `InvocationSnapshot` requires that exact held snapshot and fails closed on
  context/scope/project/profile/freshness/fingerprint/coverage mismatch.
- `CanonicalCurrent` performs the canonical service read.

There is no silent snapshot-to-database fallback. Governed process execution and
non-Project Structure contexts use canonical data. Snapshot reads are read-only
context; all mutations still pass through current canonical authorization and
concurrency checks.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Agent execution activity and runtime snapshots: `docs/architecture/internal-communication.md`
- Agent runtime tool surface: `docs/agent-runtime-tool-surface.md`
