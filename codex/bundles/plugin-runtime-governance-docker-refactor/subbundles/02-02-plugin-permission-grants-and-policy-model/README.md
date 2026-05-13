# SB02 Plugin Permission Grants And Policy Model

## Status

- `Ready`

## Objective

- Add the core domain, persistence, and policy model that separates manifest-declared plugin capabilities from explicit user-granted runtime permissions.

## Success Criteria

- Installed/enabled plugins are still denied runtime access until grants exist.
- Grant evaluation is strongly typed, testable, and shared by validation and runtime.
- Denied capability access fails predictably with actionable reason codes.

## Covered Inputs

- `N002`: find weak points.
- `N008`: keep plugins generic.
- `N009`: explicit user control over files, PowerShell, and other tools.
- Requirements `R001` through `R005`.

## Prerequisites

- SB01 audit gate is closed.
- Current plugin installation record shape is understood.
- Migration strategy for PostgreSQL and SQLite is chosen before persistence edits.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginManifestContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginExecutionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions\PluginConnectionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Catalog\PluginCatalogServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins\Persistence\PluginInstallationRecord.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\20260513182504_AddPluginsModule.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\20260513182435_AddPluginsModule.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginManifestTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\PluginCapabilityFacadeTests.cs

## Deliverables

- Strongly typed grant identifiers, capability grant state, grant scope, risk level, and denial reason contracts.
- Plugin grant persistence records and EF configurations separate from installation records.
- Grant evaluator service that checks manifest declaration, installation state, enabled state, connection state, grant state, and app policy.
- Capability context factory/proxy design where undeclared or ungranted capabilities fail explicitly.
- Unit and integration tests for installed-but-denied, declared-but-ungranted, granted, revoked, and unsupported capability cases.

## Dependency Impact

- SB03 host-tool recipes must consume this grant evaluator.
- SB04 permission UI/API persists and displays the grants introduced here.
- SB05 workflow bridge must use the same evaluator for validation and execution.
- Weak proof here invalidates all downstream safety claims.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add strongly typed grant domain contracts in the plugin abstraction or module boundary that best preserves dependency direction.
2. Add EF records/configuration/migrations for grants without expanding `PluginInstallationRecord` into a catch-all record.
3. Add application service methods for reading grant snapshots and evaluating decisions.
4. Add denied-capability proxy or result contract for `IPluginCapabilityContext`.
5. Add tests proving install/enable is not consent.
6. Add architecture guardrails that block raw service/provider exposure if missing.
7. Update execution report with commands and gate proof.

## Scope Exceptions

- No UI controls in this subbundle.
- No host-command recipes in this subbundle.
- No Docker sample plugin in this subbundle.

## Do Not Do

- Do not add Docker-specific grant concepts to core plugin abstractions.
- Do not use stringly typed permission names where enums or strongly typed ids are feasible.
- Do not use request-body actor strings as trusted audit identity.
- Do not silently return empty data for denied capabilities.

## Acceptance Checklist

- Grant persistence is separate from installation persistence.
- Grant evaluator requires both declaration and persisted approval.
- Tests cover missing declaration, missing grant, denied grant, revoked grant, disabled plugin, and successful grant.
- Denial reason codes are stable and actionable.
- EF records include indexes and concurrency where user-mutated state is persisted.

## Proof Required

- Targeted unit test command and result.
- Targeted integration test command and result for grant persistence.
- Migration files for PostgreSQL and SQLite when persistence is changed.
- Execution report row updated with SB02 closure decision.

## Browser Validation Logging

- `N/A`: this subbundle has no browser-visible implementation.

## Progression Gate

- SB03, SB04, and SB05 may start only after tests prove installed/enabled plugins cannot use capabilities without explicit grants.

## Suggested Agent Prompt

```text
Implement SB02 only.
Add the plugin grant and policy foundation with strongly typed contracts, EF persistence, evaluator tests, and denied-capability behavior. Do not implement UI, host command recipes, workflow bridge, or Docker sample code.
```
