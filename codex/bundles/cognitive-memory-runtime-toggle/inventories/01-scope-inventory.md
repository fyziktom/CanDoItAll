# Scope Inventory

| Area | Files | Action |
| --- | --- | --- |
| Settings contracts | `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsContracts.cs` | Add `IsEnabled` and disabled metadata constants. |
| Settings persistence | `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsEntities.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Settings/CognitiveMemorySettingsServices.cs`, migrations | Persist and map setting. |
| API | `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs`, `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs` | Accept setting on save. |
| UI | `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.razor.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/CognitiveMemoryPage.SettingsAndSources.cs`, `repo://src/CanDoItAll.Modules.CognitiveMemory/Pages/Components/CognitiveMemorySettingsTab.razor` | Load/save/render toggle. |
| Agent integration | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` | Skip contributor when disabled. |
| Workflow integration | `repo://src/CanDoItAll.Modules.CognitiveMemory/Advanced/CognitiveMemoryMafIntegration.cs` | Skip recall/probe/learning proposal executors when disabled. |
| Automation integration | `repo://src/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs` | Skip runner when disabled. |
| Tests | `repo://tests/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalSettingsTests.cs`, `repo://tests/CanDoItAll.Tests.Unit/CognitiveMemoryOperationalServicesTests.cs` | Add/adjust targeted coverage. |
