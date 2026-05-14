# Current State

## Observations

- `CanDoItAll.Modules.Security` already owns secret metadata through `SecretRecord`, `SecretReference`, `SecretService`, and `StorageSecretResolver`, so the catalog should be improved rather than replaced.
- `SecretRecord.EncryptedPayload` currently stores the protected payload directly in the database. Protection is done by `DataProtectionSecretProtector`, which is ASP.NET Core Data Protection, not Windows DPAPI.
- `SecretService.GetAsync` decrypts and returns the full secret value into `SecretEditorModel.SecretValue`. The settings page then keeps that value in component state for the whole edit session.
- `SecretStoreAgentProviderCredentialResolver` currently reads `SecretRecord.EncryptedPayload`, decrypts it, and calls `AgentProviderEnvironmentCredential.PromoteProcessValue`, which extends secret lifetime beyond the single provider call.
- `WorkflowHttpExecutorSettings` stores only raw header key/value strings. `WorkflowCanvasEditor` exposes a `Headers JSON` textarea, so users must either paste raw secrets or depend on external environment/config values.
- BaseLib has a simple `Password` component and a robust `CopyButton`; it does not yet have a secret field that combines copy, timed reveal, and auto-hide.
- Project/resource data models already have `LinkedSecretIdsJson` in migrations/resources, but the project-structure canvas does not expose a first-class secret reference picker/create dialog in the inspected surfaces.
- The solution targets `net10.0`. DPAPI usage requires `System.Security.Cryptography.ProtectedData` and must remain Windows-gated.
- Radzen was not found in source search, so existing BaseLib/Tailwind component patterns apply.

## Relevant Files

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\StorageSecretResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Providers\Credentials\SecretStoreAgentProviderCredentialResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Editors\EditorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\Password.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\CopyButton.razor`
- `C:\repositories\CanDoItAll\docs\secure-configuration.md`
