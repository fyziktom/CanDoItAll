# Source Artifacts

| Artifact | Evidence |
| --- | --- |
| User raw request | `inputs/00-original-request.md` |
| Microsoft Learn DPAPI guidance | `ProtectedData` is Windows-only; `DataProtectionScope.CurrentUser` binds decryption to the same Windows user. Source: `microsoft_docs_search` result for `System.Security.Cryptography.ProtectedData` and `DataProtectionScope`. |
| Current secret storage | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs` currently stores `SecretRecord.EncryptedPayload` via `DataProtectionSecretProtector`. |
| Current runtime provider credential resolver | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Providers\Credentials\SecretStoreAgentProviderCredentialResolver.cs` resolves DB secrets and promotes them into process environment values. |
| Current HTTP workflow executor | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutors.cs` has `HttpFetchWorkflowExecutor` with raw header settings and no secret-reference binding. |
| Current workflow HTTP UI | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor` exposes HTTP headers JSON but no secret selector. |
| Current BaseLib password component | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\Password.razor` renders a simple password input with no timed reveal or copy actions. |
| Existing BaseLib copy component | `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\CopyButton.razor` supports copied-state clipboard actions and should be reused. |
| Current settings secret editor | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor` uses an `InputText type="password"` for raw secret edits. |
| Existing project/resource secret reference model | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\ResourceModels.cs` has `LinkedSecretIdsJson`; project structure needs a comparable reference-only dialog rather than raw value embedding. |
| CodeAnalytics snapshot | `snap-20260513004458-f2d9cb04`, scoped to Security, Infrastructure, AgentFramework, Processes, Projects, BaseLib, Web, and unit tests. |
