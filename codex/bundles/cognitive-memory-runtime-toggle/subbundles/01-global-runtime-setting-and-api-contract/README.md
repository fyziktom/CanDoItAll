# Global Runtime Setting And API Contract

## Status

- `Completed`

## Objective

Add the persisted runtime setting and expose it through the existing Cognitive Memory settings API and UI.

## Success Criteria

- `CognitiveMemoryAutomationSettings` and update/record types include an enabled flag with default `true`.
- API PUT/GET round-trip the setting.
- Settings page renders and saves a runtime enabled/disabled control.
- PostgreSQL and SQLite migrations add the backing column with safe existing-row default.

## Covered Inputs

- `N003`, `N004`, `N007`
- Requirements: `R001`, `R002`, `R007`

## Prerequisites

- Prepared bundle validation passed or failures acknowledged in `reviews/01-execution-report.md`.

## Exact Source References

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsEntities.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsServices.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsEntityConfigurations.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.razor.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.SettingsAndSources.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemorySettingsTab.razor`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs`
- `repo://src/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj`
- `repo://src/CanDoItAll.Migrations.Sqlite/CanDoItAll.Migrations.Sqlite.csproj`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalSettingsTests.cs`

## Deliverables

- Settings contract change.
- Settings UI/API round-trip change.
- EF migrations for both providers.
- Settings persistence test update.

## Dependency Impact

- SB02 depends on this subbundle because every integration guard reads this setting.
- If this setting is not persisted or defaults incorrectly, downstream disabled-mode proof is invalid.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add `IsEnabled` to settings contracts with default enabled.
2. Add the persisted record property and mapping in settings service.
3. Add API request property and pass it into update builder.
4. Add UI backing field and render the enabled/disabled control in the settings tab.
5. Generate PostgreSQL and SQLite migrations.
6. Update targeted settings tests.

## Scope Exceptions

- Do not disable the settings/status/database management endpoints; they are required to operate the toggle.

## Do Not Do

- Do not reuse `CognitiveMemoryModelAccessMode.Disabled` as the global flag.
- Do not move settings to startup-only configuration.
- Do not refactor unrelated Cognitive Memory pages.

## Acceptance Checklist

- [x] New setting defaults to enabled for new and existing rows.
- [x] UI and API can save disabled and enabled values.
- [x] Migrations exist for PostgreSQL and SQLite.
- [x] Settings tests pass.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- Command transcript for targeted settings tests.
- Source assertion showing settings contract, API/UI, service mapping, and migrations.
- Changed-file hashes.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure.

## Production Behavior Artifact Matrix

| Signal/state | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| `CognitiveMemoryAutomationSettings.IsEnabled` | Settings service/API/UI | SB02 runtime guards | Persisted to DB, read per call | Pending SB02 disabled tests. |

## Browser Validation Logging

- Target route: `/cognitive-memory?projectId={projectId}` settings tab if an app host is available.
- Required viewport: large desktop. Narrow width only if layout changes beyond inserting one compact control.
- Actions/assertions: navigate to settings, verify the enabled/disabled control exists and save remains available.
- Screenshots: `proof/SB01/browser-settings-toggle.png` if browser proof is captured.
- Review questions: control label is clear; no card nesting or overlap; existing settings layout remains scannable.

## Progression Gate

- SB02 may start only after the setting is available from `ICognitiveMemoryAutomationSettingsService.GetAsync`.

## Suggested Agent Prompt

```text
Implement SB01 only. Add the persisted Cognitive Memory usage flag, expose it through API/UI, create both EF migrations, update settings tests, and capture proof before starting runtime guards.
```
