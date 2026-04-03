# File references

## Existing files to inspect first

- `src/CanDoItAll.Web/Program.cs`
- `src/CanDoItAll.Web/Composition/ModuleAssemblies.cs`
- `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/CanDoItAll.Modules.Projects/ProjectModels.cs`
- `src/CanDoItAll.Modules.Resources/ResourceModels.cs`

## New or changed files expected

- `src/CanDoItAll.Modules.CrmHr/CrmHrModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.CrmHr/CrmHrSchemaInitializer.cs`
- `src/CanDoItAll.Modules.CrmHr/Domain/PartyModels.cs`
- `src/CanDoItAll.Modules.CrmHr/Domain/CrmModels.cs`
- `src/CanDoItAll.Modules.CrmHr/Domain/HrModels.cs`
- `src/CanDoItAll.Modules.CrmHr/Domain/AiAgentModels.cs`
- `src/CanDoItAll.Modules.CrmHr/Domain/ProjectPartyIntegrationModels.cs`
- `src/CanDoItAll.Modules.CrmHr/Services/PartyDirectoryService.cs`
- `src/CanDoItAll.Modules.CrmHr/Services/CrmService.cs`
- `src/CanDoItAll.Modules.CrmHr/Services/HrService.cs`
- `src/CanDoItAll.Modules.CrmHr/Services/AiAgentService.cs`
- `src/CanDoItAll.Modules.CrmHr/Services/ProjectPartyIntegrationService.cs`

## Test files to add or update

- `tests/CanDoItAll.Tests.Integration/CrmHrSchemaIntegrationTests.cs`
