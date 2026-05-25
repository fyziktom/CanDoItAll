# Proof manifest SB02

## Status

Complete.

## Commands

- `dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter FullyQualifiedName~SettingsPageDataSourcesTests -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Settings_data_sources_locked_mode_is_visible_in_responsive_layout|FullyQualifiedName~Snapshot_actions_are_not_rendered_on_data_sources_page|FullyQualifiedName~Snapshot_actions_remain_absent_in_responsive_layout" -v:minimal`

## Evidence files

- `evidence/SB02/dotnet-test-components-data-sources.log`
- `evidence/SB02/dotnet-test-playwright-data-sources-stable.log`
- `evidence/SB02/db-switch-no-snapshot-actions-desktop.png`
- `evidence/SB02/db-switch-no-snapshot-actions-responsive.png`

## Notes

Working directory: `C:\repositories\CanDoItAll`.
The Data Sources panel now renders the PostgreSQL runtime path and removes retired provider forms, unsupported legacy-profile UI, snapshot actions, and persisted InMemory profile creation controls. Component and browser tests assert the retired provider text/test IDs and snapshot controls are absent.
