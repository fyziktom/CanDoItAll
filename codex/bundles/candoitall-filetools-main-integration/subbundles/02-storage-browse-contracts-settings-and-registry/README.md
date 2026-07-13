# SB02 Storage Browse Contracts Settings And Registry

## Status

- `Ready`

## Objective

- Add a provider-native, bounded, typed Storage browsing foundation without changing FileTools or coupling Infrastructure to it.

## Covered Inputs

- N002-N004, N008, N013-N015; R002-R004, R007, R009, R026-R036, R040.

## Prerequisites

- SB01 Completed; current package/source/snapshot proof trusted.

## Exact Source References

- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Models/StorageModels.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/StorageDriverRegistry.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/Storage/StorageJson.cs`
- `repo://src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/StorageJsonTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/StorageCatalogServiceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/StorageAccessServiceTests.cs`
- `bundle://architecture/00-csharp-current-state-inventory.md`
- `bundle://architecture/10-performance-and-scale.md`
- `bundle://analysis/03-dotnet-performance-audit.md`

## Deliverables

- `IStorageBrowseDriver` and deterministic `IStorageBrowseDriverRegistry` separate from `IStorageDriver`.
- Typed native request/page/entry/cursor/error/capability records sufficient for root/path/shallow browse; optional search/stat are capability-explicit.
- Typed validated inspection/metadata/time/concurrency/search/retained-state budgets and truthful ordering/paging modes from `bundle://architecture/10-performance-and-scale.md`.
- Validated `StorageBrowseCacheSettings` nested in provider configuration with missing config -> Disabled.
- Duplicate provider kind and unknown/unsupported operations fail explicitly; no last-wins/default fallback.
- Declarative DI registration and direct unit/JSON compatibility tests.

## Dependency Impact

- SB03/SB04 implement the contract; SB06 maps it to FileTools; an incorrect model invalidates all downstream providers/UI.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical native contract foundation.

## Implementation Steps

1. Characterize existing provider-config JSON and registry behavior.
2. Finalize typed contract/capability/settings names against `architecture/05-storage-filetools-contract-map.md`.
3. Implement records with constructor/options validation and bounded non-sentinel defaults; require providers to report inspected/returned/partial facts.
4. Implement registry that detects duplicates at construction/startup.
5. Register contracts without FileTools reference.
6. Add positive and adversarial tests: malformed cursor/settings, oversize page, duplicate/unknown/unsupported provider, invalid/unbounded budgets, unsupported global order, and cancellation/completeness facts.
7. Run affected build/tests and focused CodeAnalytics/source audit.

## C# Architecture Impact

- Local contract/registry/settings extraction; no new project.

## Boundary Ownership

- Infrastructure Storage owns native facts. FileTools mapping is forbidden here.

## Dependency Direction

- No new package/project reference. Source audit must find no `CanDoItAll.FileTools` symbol.

## Pattern Decision

- PSR-01 typed registry/sidecar; no giant switch or service locator.

## Testability Contract

- Tests instantiate records/registry/fake drivers directly without Web, EF, or existing broad services.

## Partial Class Policy

- No partial classes.

## Architecture Proof Required

- Responsibility map, exact tests, snapshot/dependency result, forbidden-reference and duplicate-registration assertions.

## Scope Exceptions

- No provider implementation, host cache, FileTools adapter, endpoint, or UI.

## Do Not Do

- Do not extend `IStorageDriver`, put policy in `MetadataJson`, use strings for modes/capabilities, or silently normalize invalid enabled settings.

## Acceptance Checklist

- [ ] Contracts are bounded/typed/capability-honest.
- [ ] A bounded returned page cannot conceal unbounded provider work or retained state.
- [ ] Legacy JSON becomes Disabled.
- [ ] Invalid settings and duplicate/unknown providers fail predictably.
- [ ] Infrastructure remains FileTools-free and acyclic.
- [ ] Existing storage tests still pass.

## Proof Required

- Behavioral semantic positive/negative records, exact test/build commands, source assertions, and downstream fake-provider check.

## Browser Validation Logging

- N/A; no browser-visible behavior.

## Progression Gate

- SB03/SB04 may enter when two distinct fake provider shapes implement the contract without unsupported members or fallback.

## Reopen Triggers

- Provider implementation requiring leaked SDK/FileTools types, unbounded response/work/state, magic operation string, or false ordering/search capability reopens SB02 and invalidates SB03-SB18.

## Suggested Agent Prompt

```text
Implement only the native typed Storage browse contracts/settings/registry. Preserve IStorageDriver and Infrastructure independence. Prove deterministic validation and meaningful unsupported/duplicate negatives before allowing provider work.
```
