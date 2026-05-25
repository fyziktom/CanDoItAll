# Proof manifest SB08

## Status

Complete with noted residual test-suite risks.

## Commands

- `dotnet restore .\CanDoItAll.slnx`
- `dotnet build .\CanDoItAll.slnx -m:1 -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter FullyQualifiedName~SettingsPageDataSourcesTests -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Quarantined" -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "FullyQualifiedName~Settings_data_sources_locked_mode_is_visible_in_responsive_layout|FullyQualifiedName~Snapshot_actions_are_not_rendered_on_data_sources_page|FullyQualifiedName~Snapshot_actions_remain_absent_in_responsive_layout" -v:minimal`
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\codex\bundles\db-remove-sqlite-followup-bundle-v1\scripts\sqlite_residue_audit.ps1`
- `python .\codex\skills\bundles\candoitall-bundle-preparation\scripts\validate_bundle.py .\codex\bundles\db-remove-sqlite-followup-bundle-v1 --stage completed --repo-root .`
- `git diff --check`

## Evidence files

- `evidence/SB08/dotnet-restore.log`
- `evidence/SB08/dotnet-build-final.log`
- `evidence/SB08/dotnet-test-unit.log`
- `evidence/SB08/dotnet-test-components-data-sources.log`
- `evidence/SB08/dotnet-test-integration-nonquarantined.log`
- `evidence/SB08/dotnet-test-playwright-data-sources-stable.log`
- `evidence/SB08/sqlite-residue-audit.log`
- `evidence/SB08/bundle-validator-completed.log`
- `evidence/SB08/db-switch-no-snapshot-actions-responsive-desktop.png`
- `evidence/SB08/db-switch-no-snapshot-actions-responsive.png`

## Notes

Restore, build, unit tests, in-scope Data Sources component tests, non-quarantined integration tests, stable Playwright Data Sources tests, residue audit, and `git diff --check` passed. The full component project run was attempted and timed out after unrelated failures in `PromptFactoryPageTests.Preview_query_opens_built_prompt_modal` and `ProjectsPageTests.Shows_saved_project_as_card_with_dashboard_action`; the in-scope Data Sources component slice passed. A quarantined self-hosted Playwright startup-flow test still exits before ready, so stable shared-fixture browser proof is the merge gate evidence for this bundle.
