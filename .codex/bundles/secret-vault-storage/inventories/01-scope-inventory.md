# Scope Inventory

| Area | Files | Notes |
| --- | --- | --- |
| Secret catalog and vault | `src/CanDoItAll.Modules.Security/*` | Add vault contracts/providers, update `SecretService`, keep metadata catalog. |
| Storage/runtime resolution | `src/CanDoItAll.Modules.Security\StorageSecretResolver.cs`, storage drivers in infrastructure | Preserve existing `IStorageSecretResolver` contract while resolving through the vault. |
| Agent provider credentials | `src/CanDoItAll.Modules.AgentFramework\Providers\Credentials\SecretStoreAgentProviderCredentialResolver.cs` | Stop long-lived process environment promotion for vault-backed records. |
| Agent settings | `src/CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`, `src/CanDoItAll.AgentFramework.Models\Editors\EditorModels.cs`, agent UI components | Add allowed secret references and editor persistence. |
| Workflow HTTP executor | `src/CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`, `src/CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutors.cs` | Add secret header binding and execute-time value resolution. |
| Workflow UI | `src/CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`, `WorkflowExecutorCanvasCatalog.cs` | Offer secret selection for HTTP fetch setup and inspector. |
| BaseLib | `src/CanDoItAll.Components.BaseLib\Components\Forms\Password.razor`, `CopyButton.razor`, sandbox inputs page | Add reusable time-bound reveal/copy control. |
| Settings | `src/CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` | Replace raw password field with BaseLib secret field and add copy name/value actions. |
| Project structure | `src/CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\*.razor`, project-structure page partials | Add search/create picker dialog for secret references. |
| Tests | `tests/CanDoItAll.Tests.Unit`, `tests/CanDoItAll.Tests.Components`, `tests/CanDoItAll.Tests.Playwright` | Add focused contract and UI tests where local patterns exist. |
| Docs | `docs/secure-configuration.md`, module READMEs if needed | Document provider behavior and runtime reference rules. |
