# SB01 CodeAnalytics after implementation

## Capture

| Fact | Value |
| --- | --- |
| Snapshot | `snap-20260824213007-c65710b4` |
| Solution | `C:\\repositories\\CanDoItAll\\CanDoItAll.slnx` |
| Captured UTC | `2026-08-24T21:30:07Z` |
| Force refresh | `true` |
| Scoped product projects | 12 |
| Scoped source documents | 677 |
| Modules | 32 |
| Dependency edges | 4,632 |
| Direct product `ProjectReference` edges | 24 |
| Project-level cycles | 0 |
| Other reported cycles | 2 module-level, 1 type-level |
| Blocking analyzer errors | No |

The after snapshot adds exactly `CanDoItAll.SharedProviders.Abstractions` to the SB01 baseline.
The new project contributes ten source/project documents and has no package or project reference.
The sole new product edge is `CanDoItAll.Web -> CanDoItAll.SharedProviders.Abstractions`.

## Cycle comparison

| Level | Before | After | Decision |
| --- | --- | --- | --- |
| Project | none | none | Pass |
| Module | Infrastructure Persistence/ControlPlane; AgentFramework Hosting/module | unchanged | Pre-existing, not widened |
| Type | image-generation provider/builder nesting | unchanged | Pre-existing, not widened |

The duplicate embedded/generated type-name diagnostics are the same non-blocking warnings
classified in SB00. All twelve requested projects and 677 source documents loaded; inventory and
dependency queries returned usable results.

## Gate result

`PASS_SB01`: the implemented graph exactly matches the authorized delta, Abstractions remains
inward and implementation-neutral, no cycle was added, and no classified baseline cycle changed.
