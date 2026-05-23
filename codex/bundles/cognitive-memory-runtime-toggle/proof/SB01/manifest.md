# SB01 Proof Manifest

## Status

- Result: `Passed`
- Scope: persisted runtime setting, API contract, settings UI, and provider migrations.

## Source Assertions

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs` line 67 adds `CognitiveMemoryAutomationSettings.IsEnabled`; line 86 defaults it to `true`; line 106 adds the update contract flag.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsEntities.cs` line 19 persists `IsEnabled` with a default value.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsEntityConfigurations.cs` line 15 maps the column as required with default `true`.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsServices.cs` lines 54 and 78 save and load the value.
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs` line 245 exposes optional API input; `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs` line 110 preserves current state when the field is omitted.
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemorySettingsTab.razor` line 16 renders `data-testid="cognitive-memory-usage-enabled"`.
- PostgreSQL and SQLite migrations add `IsEnabled` to `CognitiveMemory_AutomationSettings` with default `true`.

## Semantic Contract

- Semantic invariants: `bundle://proof/SB01/semantic-invariants.md`.
- Passing transcript: `bundle://proof/SB03/transcripts/tests-passing.md`.
- Failing-first: N/A process. The failure was supplied as an observed runtime log, and this subbundle changes persistence/API/UI infrastructure rather than a standalone failing executable path.
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.md`.

## Changed-File Hashes

- `E0B2C9702DD556C3ED4F38D14774450D25B30C4735455A9BDD521BAC902A6511` `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs`
- `953E0A0F8ADB593E69ED81F5B117E6AC7666A4AB6FCB5399251A131276B0FF9D` `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalSettingsTests.cs`

## Validation

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~AgentContextContributionTests|FullyQualifiedName~CognitiveMemoryOperationalSettingsTests|FullyQualifiedName~CognitiveMemoryOperationalServicesTests"` passed.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~CognitiveMemoryPageTests"` passed.
- `dotnet build CanDoItAll.slnx --no-restore` passed with 0 warnings and 0 errors.

## Changed Files

- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsEntities.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsEntityConfigurations.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsServices.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.razor.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.SettingsAndSources.cs`
- `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemorySettingsTab.razor`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.SettingsEndpoints.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs`
- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260522125709_AddCognitiveMemoryRuntimeUsageSetting.cs`
- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260522125709_AddCognitiveMemoryRuntimeUsageSetting.Designer.cs`
- `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs`
- `repo://src/CanDoItAll.Migrations.Sqlite/Migrations/20260522125754_AddCognitiveMemoryRuntimeUsageSetting.cs`
- `repo://src/CanDoItAll.Migrations.Sqlite/Migrations/20260522125754_AddCognitiveMemoryRuntimeUsageSetting.Designer.cs`
- `repo://src/CanDoItAll.Migrations.Sqlite/Migrations/AppDbContextModelSnapshot.cs`
- `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalSettingsTests.cs`
