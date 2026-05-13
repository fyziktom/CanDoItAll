# Plugin Shop And Package Contracts

## Status

- `Ready`

## Objective

- Define remote shop/catalog/package/install contract and trust metadata.

## Success Criteria

- Remote shop/catalog/package metadata contract exists.
- Local app can model shop sources and available remote plugins as metadata.
- Install state records manifest snapshots and trust metadata.
- No arbitrary unsigned dynamic code loading is implemented.

## Covered Inputs

- `R008`
- `R009`
- `R022`
- `R027`
- `R028`
- `R033`
- `R035`
- `F011`
- `F012`

## Prerequisites

- `SB14`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\ModuleAssemblies.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ApiEndpointRouteBuilderExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj`

## Deliverables

- Plugin shop source model and DTOs.
- Plugin package manifest contract with compatibility/hash/signature fields.
- Remote catalog client abstraction with failure-safe behavior.
- Install-state extension for package metadata/trust decisions.
- Tests for unreachable shop, incompatible package, signature metadata validation, and metadata-only display.

## Dependency Impact

- Future remote shop and OAuth2 provider bundles depend on this seam staying safe and non-breaking.

## Validation Depth

- `Future-facing contract foundation`

## Implementation Steps

1. Define PluginPackageManifestV1 with plugin id, package id, version, app compatibility, capabilities, dependencies, hash, signature, source URL, and advisory fields.
2. Define shop source configuration and remote catalog client abstraction.
3. Add local persistence for shop source and remote catalog snapshot if needed.
4. Add API/UI support to display remote available plugins as metadata-only or installable only when bundled/local policy allows.
5. Add compatibility checks for min app version and required capabilities.
6. Add audit records for install/update/enable/disable metadata actions.
7. Add tests for offline shop, invalid metadata, incompatible app version, and no-code-loading guarantee.

## Scope Exceptions

- Actual external assembly download/loading is out of scope.
- Payments/licensing are out of scope.

## Do Not Do

- Do not call AssemblyLoadContext.LoadFromAssemblyPath for remote packages in this subbundle.
- Do not execute remote renderer components.
- Do not treat missing signatures as trusted.

## Acceptance Checklist

- [ ] Remote shop/catalog/package metadata contract exists.
- [ ] Local app can model shop sources and available remote plugins as metadata.
- [ ] Install state records manifest snapshots and trust metadata.
- [ ] No arbitrary unsigned dynamic code loading is implemented.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "PluginShop|PluginPackage"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "PluginShop"`
- If UI changed, browser screenshot showing shop metadata/offline state.

## Browser Validation Logging

- If shop UI is added: capture catalog with bundled plus remote metadata and offline/error state.

## Progression Gate

- Passed only when shop support is metadata/package-contract-only and cannot execute untrusted code.

## Suggested Agent Prompt

```text
Implement SB15 only.

Work outcome-first:
- Read this subbundle README, the root README, and reviews/01-execution-report.md.
- Verify prerequisites and exact source references before editing.
- Preserve the listed scope boundaries.
- Make the smallest correct change set.
- Capture required proof.
- Update reviews/01-execution-report.md.
- Stop if the progression gate cannot honestly pass.
```
