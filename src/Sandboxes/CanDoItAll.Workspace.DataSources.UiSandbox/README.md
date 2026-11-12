# Data Sources scenario host

Run `dotnet watch --project src/Sandboxes/CanDoItAll.Workspace.DataSources.UiSandbox`
from the repository root. This backend-free host renders the same profile and transfer
components as production, with real BaseLib styles, icons and modal behavior.

Scenarios cover empty, loading, locked, unavailable, current, pending restart and schema
states, plus delayed writes, acknowledged writes followed by failed reads, uncertain writes
and partial transfer groups. Reopen keeps the operation ledger; changing scenario resets it.
Release and observation controls are explicit scenario controls, not production recovery.
No scenario creates a database, starts a process or contacts a provider.

The source and published browser tests validate raw numeric input, Unicode edits, caret and
focus, explicit group selection, keyboard dialog behavior and closure during a held command.
Owner and production browser tests provide the separate real database proof.
