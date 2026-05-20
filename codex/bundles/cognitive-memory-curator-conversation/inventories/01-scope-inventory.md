# Scope Inventory

## Backend Contracts And Services

| Surface | Existing path | Expected change |
| --- | --- | --- |
| Cognitive Memory advanced contracts | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedContracts.cs` | Add curator conversation mode/action/result contracts. |
| Cognitive Memory advanced entities | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedEntities.cs` | Add or reuse entities to persist curator sessions, turns, and captured improvement items. |
| Cognitive Memory advanced services | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs` | Add curator conversation service with shared runtime result and capture pipeline. |
| DI registration | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs` | Register curator service. |
| EF configuration | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedEntityConfigurations.cs` | Configure any new persisted records. |
| JSON context | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Common\CognitiveMemoryJson.cs` | Add generated JSON metadata payloads if needed. |

## Runtime Integrations

| Surface | Existing path | Expected change |
| --- | --- | --- |
| Agent workspace service | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs` | Use existing agent chat/provider test APIs; avoid modifying unless necessary. |
| Cognitive Memory settings | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemorySettingsContracts.cs` | Use default provider/default agent for mode defaults. |
| Voice service | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Voice\AgentVoiceService.cs` | Reuse; avoid changes unless required by curator flow. |

## UI

| Surface | Existing path | Expected change |
| --- | --- | --- |
| Page tab shell | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor` | Add Curator tab. |
| Page state/actions | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs` | Add curator state, send/voice handlers, status, and refresh integration. |
| Component tab | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components` | Add curator tab component or extend existing components. |
| Page CSS | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.css` | Add bounded layout styles only where BaseLib components are insufficient. |

## Tests

| Surface | Existing path | Expected change |
| --- | --- | --- |
| Unit tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit` | Add curator service tests. |
| Component tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components` | Add Curator tab rendering/control tests. |
| Playwright tests | `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright` | Add or update browser proof if practical in test suite; otherwise record manual Playwright evidence. |
