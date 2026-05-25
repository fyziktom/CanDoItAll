# Proof manifest SB04

## Status

Complete.

## Commands

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\codex\bundles\db-remove-sqlite-followup-bundle-v1\scripts\sqlite_residue_audit.ps1`
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter FullyQualifiedName~SettingsPageDataSourcesTests -v:minimal`

## Evidence files

- `evidence/SB04/sqlite-residue-audit.log`
- `evidence/SB04/dotnet-test-unit.log`
- `evidence/SB04/dotnet-test-components-data-sources.log`

## Notes

The residue audit script now searches `src`, `tests`, and `CanDoItAll.slnx` correctly on Windows. Unit tests cover raw legacy catalog quarantine, retained PostgreSQL catalog behavior, and generic unsupported-provider handling. Component tests cover the PostgreSQL-only Data Sources UI.
