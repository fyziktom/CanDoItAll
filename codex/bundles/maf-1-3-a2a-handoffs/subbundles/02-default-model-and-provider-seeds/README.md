# Default Model And Provider Seeds

## Status

- `Completed`

## Objective

Make `gpt-5.4-mini` the default OpenAI model across provider seeds, runtime fallbacks, provider adapter defaults, UI defaults, and tests.

## Covered Inputs

- `NOTE-02`
- `REQ-02`

## Prerequisites

- Subbundle 01 has identified any package/API breakage.
- Provider availability assumption for `gpt-5.4-mini` is documented.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\Seeds\ManagedSeedProviderFallbacks.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Providers\ProviderExecution.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ManagedSeedProviderFallbacksTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\AgentProviderModelParameterPolicyTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Deliverables

- `ManagedSeedProviderFallbacks.OpenAiDefaultModel` changed to `gpt-5.4-mini`.
- OpenAI suggested model ordering updated with `gpt-5.4-mini` first.
- Workspace provider adapter default changed to `gpt-5.4-mini`.
- UI/test defaults updated where they represent current OpenAI defaults.
- Historical migrations left untouched unless build/tests prove they are active seed sources.

## Dependency Impact

- Tool/profile and process tests depend on the default model being consistent.
- Provider health failures must now report `gpt-5.4-mini` clearly so operators know the model access issue.

## Validation Depth

- Focused seed/provider regression.

## Implementation Steps

1. Replace active `gpt-5-mini` defaults with `gpt-5.4-mini`.
2. Update suggested model lists without removing useful older choices.
3. Update tests that assert current defaults.
4. Search for remaining active `gpt-5-mini` references and classify each as historical, fixture-specific, or a defect.

## Scope Exceptions

- Do not edit EF migration snapshots only to change historical default strings.
- Do not force non-OpenAI providers to use OpenAI model names.

## Do Not Do

- Do not introduce a second default-model constant.
- Do not hard-code `gpt-5.4-mini` in new places when existing constants are available.

## Acceptance Checklist

- Active OpenAI defaults resolve to `gpt-5.4-mini`.
- Seeded agents inherit `gpt-5.4-mini`.
- Provider editor defaults show `gpt-5.4-mini` for OpenAI.
- Tests no longer assert `gpt-5-mini` as the current default.

## Proof Required

- `git ls-files '*.cs' '*.razor' '*.csproj' | Where-Object { $_ -notmatch 'Migrations/' -and $_ -notmatch '^codex/' -and $_ -notmatch '^\.codex-temp/' } | ForEach-Object { Select-String -Path $_ -Pattern 'gpt-5-mini' }`: passed; no active references remain.
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter ManagedSeedProviderFallbacksTests --no-restore -m:1`: passed; 15 tests.
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter AgentProviderModelParameterPolicyTests --no-restore -m:1`: passed; 10 tests.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter AgentFrameworkWorkspaceSeedIntegrationTests --no-restore -m:1`: passed; 19 tests.

## Browser Validation Logging

- N/A unless provider editor UI text or behavior changes.

## Progression Gate

- Downstream tool/process work may continue once active defaults are consistent and seed tests pass or known package issues are recorded.

## Suggested Agent Prompt

```text
Implement subbundle 02 only: migrate active OpenAI defaults to gpt-5.4-mini using existing constants and update focused tests. Do not edit historical migrations.
```
