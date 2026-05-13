# Normalized Requirements

| Requirement | Description | Source | Owner |
| --- | --- | --- | --- |
| `R001` | Define `ISecretVault`, `SecretVaultOptions`, provider identifiers, and creation/DI wiring so code depends on a vault boundary rather than a local protector. | `N001`, `N003` | `SB01` |
| `R002` | Implement a Windows DPAPI vault using `ProtectedData` with `DataProtectionScope.CurrentUser`, scoped entropy, per-secret file persistence, and explicit Windows-only guardrails. | `N002`, `N004` | `SB01` |
| `R003` | Add unsupported provider stubs for MAUI SecureStorage, macOS Keychain, Linux Secret Service, Azure Key Vault, and HashiCorp Vault, plus `InMemorySecretVault` for tests. | `N004`, `N005` | `SB01` |
| `R004` | Keep a cross-platform file vault fallback available under an explicit provider path and document the security tradeoff. | `N004` | `SB01` |
| `R005` | Update `SecretService`, storage credential resolution, and provider credential resolution to use vault keys and avoid raw-value persistence outside the vault. | `N006`, `N010` | `SB02` |
| `R006` | Add a runtime-resolution service that resolves a secret only for a declared purpose and returns values only for the immediate action. | `N006`, `N010` | `SB02` |
| `R007` | Add agent settings for allowed secret requests and enforce them before agent/tool runtime code can resolve stored secrets. | `N007`, `N008` | `SB03` |
| `R008` | Extend workflow HTTP fetch settings and UI so users can select a stored secret for authorization/API-key headers without typing raw headers. | `N007`, `N009` | `SB03` |
| `R009` | Prepare process/project-structure runtime references so nodes can carry secret references by metadata only. | `N006`, `N012` | `SB03` |
| `R010` | Add BaseLib secret editing/display component with copy actions and `show for 30s` auto-hide behavior. | `N011` | `SB04` |
| `R011` | Add a secret picker/create dialog for project structure and reuse it from selection flows instead of storing raw values. | `N012` | `SB04` |
| `R012` | Update documentation and capture build, test, and browser proof. | `N013` | `SB05` |
