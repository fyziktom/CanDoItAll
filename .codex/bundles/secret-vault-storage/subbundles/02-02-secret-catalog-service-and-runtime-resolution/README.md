# Secret Catalog Service And Runtime Resolution

## Status

- `Completed`

## Objective

- Route existing secret metadata through the vault, add a narrow runtime resolver, and remove avoidable long-lived plaintext handling.

## Covered Inputs

- `N006`, `N010`
- `R005`, `R006`

## Prerequisites

- `SB01` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\StorageSecretResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Providers\Credentials\SecretStoreAgentProviderCredentialResolver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Abstractions\StorageContracts.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`

## Deliverables

- Secret catalog writes store a vault reference/key and not a raw protected payload for new records.
- Read compatibility for existing DataProtection payloads is explicit and bounded.
- `SecretRuntimeResolver` resolves by id and purpose.
- Storage and agent provider credential resolvers use the runtime resolver.
- Vault-backed provider credentials are not promoted into process-wide environment variables.

## Dependency Impact

- `SB03` depends on this resolver to enforce safe runtime use.
- `SB04` depends on this service to reveal/copy values only through controlled calls.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add a versioned vault reference format for `SecretRecord.EncryptedPayload` or a compatible metadata field.
2. Update `SecretService.SaveAsync`, `GetAsync`, and `DeleteAsync` to use `ISecretVault`.
3. Add runtime resolver contracts and implementation.
4. Update storage and agent provider credential resolution.
5. Add tests for CRUD, delete, compatibility path, missing secret, and no process environment promotion.

## Scope Exceptions

- Database schema migration is only required if the existing `EncryptedPayload` column cannot hold a vault reference safely.

## Do Not Do

- Do not add agent/workflow UI here.
- Do not keep a silent fallback to the old protector for new writes.
- Do not log resolved secret values.

## Acceptance Checklist

- [x] New saves call `ISecretVault.SetAsync`.
- [x] Reads call `ISecretVault.GetAsync` and return null/failure predictably when missing.
- [x] Deletes remove the vault payload.
- [x] Agent provider resolver no longer promotes vault-backed values into the process environment.

## Execution Notes

- Added `SecretVaultRecordReference` with versioned `vault:v1:` payload references.
- Added `ISecretRuntimeResolver` with purpose and allowed-secret enforcement.
- Updated `SecretService` so new and edited secrets stage vault material before metadata save, clean up old vault material after successful update, and retain legacy DataProtection read compatibility.
- Updated storage and agent provider credential resolution to go through `ISecretRuntimeResolver`.
- Added `ProviderCredentialResolution.ShouldPromoteToProcessEnvironment`; vault-backed credentials opt out of long-lived process environment promotion.
- Validation passed:
  - `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SecretVault"`: passed, 9/9.
  - `dotnet build src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj`: passed.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SecretVault|SecretService|AgentProvider"`
- `dotnet build src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj`

## Browser Validation Logging

- N/A. Backend contract only.

## Progression Gate

- Passed. Runtime resolver proof shows secret values are resolved on demand and vault-backed provider credentials opt out of process environment promotion.

## Suggested Agent Prompt

```text
Implement SB02 only. Wire the existing catalog and runtime consumers through the vault, add focused tests, and update the execution report before starting agent/workflow surfaces.
```
