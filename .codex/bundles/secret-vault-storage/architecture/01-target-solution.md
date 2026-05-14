# Target Solution

## Architecture

- `CanDoItAll.Modules.Security` owns the vault abstraction:
  - `ISecretVault` with `SetAsync`, `GetAsync`, and `DeleteAsync`.
  - `SecretVaultOptions` with provider, application name, and vault path.
  - `SecretVaultProviderKind` or equivalent typed provider selector.
  - `SecretVaultFactory` that selects DPAPI on Windows for `Auto`, platform stubs on explicitly requested future providers, and a data-protection file fallback only when intentionally selected.
- `DpapiSecretVault` persists protected values under a per-user application vault path. Each value is protected with `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)` and application/key entropy.
- Existing `SecretRecord` remains metadata/catalog state. Its payload column should store a vault key or versioned vault reference instead of the raw encrypted value for new writes.
- `SecretRuntimeResolver` is the narrow runtime boundary for action execution. It accepts a secret id/reference and purpose, fetches the current value from the vault, and returns it only to the caller performing the action.
- `SecretStoreAgentProviderCredentialResolver`, storage secret resolution, and workflow HTTP execution use the runtime resolver. They must not promote values into process environment variables unless a target API absolutely requires an environment variable and the lifetime is explicitly bounded.
- Agent editor models gain allowed secret references. Runtime checks enforce that agents can only request selected records.
- `WorkflowHttpExecutorSettings` gains strongly typed secret header binding settings, for example a selected secret id and destination header scheme/name. The HTTP executor applies the value to the request and never serializes it into result payloads.
- BaseLib gains `SecretField` or an enhanced `Password`-backed component with copy actions and a `Show for 30s` control.
- Project-structure UI uses a shared secret picker/create dialog. The selected node metadata records secret id/name/kind/purpose only.

## Boundaries

- UI components render metadata and controlled reveal/copy affordances; they never own persistence or authorization.
- `Modules.Security` owns vault and catalog behavior.
- Agent/workflow/process/project modules own references and authorization decisions, but they call `SecretRuntimeResolver` to fetch values.
- Infrastructure logging/redaction remains a separate cross-cutting concern and must not be weakened.

## Microsoft Learn Grounding

- `ProtectedData` is a Windows DPAPI wrapper and is Windows-only on .NET.
- `DataProtectionScope.CurrentUser` means only the same Windows user context can unprotect the data. `LocalMachine` is broader and not the default for local desktop users.
