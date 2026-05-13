# Secret Vault Contract And DPAPI Foundation

## Status

- `Completed`

## Objective

- Add the vault abstraction, provider options, DPAPI Windows provider, explicit future-provider stubs, and in-memory test provider.

## Covered Inputs

- `N001`, `N002`, `N003`, `N004`, `N005`
- `R001`, `R002`, `R003`, `R004`

## Prerequisites

- Bundle readiness gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\CanDoItAll.Modules.Security.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`

## Deliverables

- `ISecretVault`, `SecretVaultOptions`, typed provider ids, and factory.
- `DpapiSecretVault`, `DataProtectionFileVault`, unsupported provider stubs, and `InMemorySecretVault`.
- DI registration that selects the default vault from options.
- Unit tests for Windows DPAPI round trip, provider factory selection, unsupported provider behavior, and in-memory vault.

## Dependency Impact

- `SB02`, `SB03`, and `SB04` depend on this boundary. If vault behavior is wrong, all runtime and UI proof is untrustworthy.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add the vault contracts and options in `CanDoItAll.Modules.Security`.
2. Implement DPAPI with `DataProtectionScope.CurrentUser`, application/key entropy, and per-key file persistence.
3. Implement `DataProtectionFileVault` as explicit fallback and unsupported stubs for the named future providers.
4. Register `ISecretVault` through options/factory in `AddSecurityModule`.
5. Add focused unit tests.

## Scope Exceptions

- MAUI, macOS Keychain, Linux Secret Service, Azure Key Vault, and HashiCorp Vault are not implemented beyond explicit stubs.

## Do Not Do

- Do not change UI in this subbundle.
- Do not migrate `SecretRecord` persistence yet.
- Do not silently fall back from an explicitly requested unsupported provider.

## Acceptance Checklist

- [x] The default provider uses DPAPI on Windows.
- [x] Unsupported provider stubs throw explicit errors.
- [x] `InMemorySecretVault` supports set/get/delete for tests.
- [x] Provider selection is strongly typed and not magic-string driven at call sites.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SecretVault"`
- `dotnet build src\CanDoItAll.Modules.Security\CanDoItAll.Modules.Security.csproj`

## Proof Captured

- `dotnet build src\CanDoItAll.Modules.Security\CanDoItAll.Modules.Security.csproj`: passed.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "SecretVault"`: passed, 5/5.

## Browser Validation Logging

- N/A. Backend contract only.

## Progression Gate

- Passed. The vault abstraction compiles, DPAPI works on Windows, and unsupported providers fail predictably.

## Suggested Agent Prompt

```text
Implement SB01 only. Add the vault contracts/providers and tests, preserve explicit unsupported providers, and update the execution report with command proof before moving to SB02.
```
