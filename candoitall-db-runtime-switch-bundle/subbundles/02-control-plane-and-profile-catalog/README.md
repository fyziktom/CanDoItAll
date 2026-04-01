# 02 Control Plane and Profile Catalog

## Status

- `Ready`

## Objective

- Introduce the app-level control plane that stores known database profiles, the active/last-used profile, control-plane secrets, and legacy-discovery metadata outside the selected application database.

## Covered Inputs

- `RQ-001` database profiles outside the selected DB
- `RQ-002` startup precedence rules
- `RQ-010` SQLite profile source modeling
- `RQ-011` PostgreSQL profile metadata modeling
- `RQ-017` persisted DataProtection keys and control-plane secret protection
- `RQ-018` explicit override compatibility
- Raw notes `N-01`, `N-03`, `N-12`, `N-13`, `N-14`

## Prerequisites

- `subbundles/01-foundation-baseline-and-guardrails` must be completed or blocked with the required fixture scaffolding in place.

## Exact Source References

- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Security/SecurityModels.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Program.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workspace/Pages/SettingsPage.razor`
- `/mnt/data/work/CanDoItAll-toolbox-repair/README.md`

## Deliverables

- Control-plane options and root-path resolution outside the selected DB.
- Persisted DataProtection key-ring configuration for control-plane secret protection.
- Database profile catalog CRUD storage and active-profile state storage.
- Profile models for SQLite and PostgreSQL source descriptors, including source kind and storage-root metadata.
- Runtime override resolution/locking behavior for explicit `Database:*` config.
- Legacy workspace discovery that can register or onboard the existing default SQLite workspace when the catalog is empty.
- Application/service APIs that later subbundles can consume for listing, validating, and resolving database profiles.

## Dependency Impact

- Subbundles 03–08 rely on this phase for the source of truth about what database exists, which one is active, and how credentials are decrypted.
- If this subbundle stores profile data inside the selected DB or fails to persist keys, downstream runtime switching and secrets will be fundamentally broken.
- The startup modal and Settings Data Sources UI in subbundle 07 are impossible to build honestly without this catalog.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add control-plane option classes and a root resolver that default to a LocalAppData-based location and support test overrides.
2. Persist the DataProtection key ring under the control-plane root and add a dedicated protector/service for control-plane secrets.
3. Define the database profile contract for SQLite and PostgreSQL, including source kind, storage descriptor, display fields, fingerprint, and audit metadata.
4. Implement catalog persistence plus active-profile persistence in files or another control-plane store outside the selected DB.
5. Implement override resolution so explicit runtime config creates a locked/ephemeral active profile path without breaking existing harnesses.
6. Implement legacy workspace discovery for the current content-root `.artifacts/workspace/candoitall.db` layout and register it when the catalog is empty.
7. Add unit and integration tests for catalog CRUD, encryption/decryption, override precedence, and legacy discovery.

## Scope Exceptions

- This subbundle does **not** yet switch the active DbContext at runtime; it only creates the control-plane source of truth.
- This subbundle does **not** yet expose end-user UI beyond any service contracts needed later.
- Create-database and clone execution flows may remain for later phases, but the metadata model must support them now.

## Do Not Do

- Do not store the catalog in `Workspace_Settings`, `Workspace_ProviderProfiles`, or any selected-app-database table.
- Do not store PostgreSQL passwords or IPFS credentials in plain text.
- Do not silently disable explicit runtime overrides used by existing tests/headless startup.

## Acceptance Checklist

- Profile catalog data persists outside the selected DB and is readable before any app DB connection is opened.
- DataProtection keys persist under the control-plane root and control-plane secrets round-trip across restart semantics in tests.
- The system can represent at least managed SQLite, external/imported SQLite, and PostgreSQL profile descriptors.
- Explicit config/env overrides still resolve to an active profile and can intentionally lock the UI later.
- Legacy default SQLite workspace detection is implemented and covered by tests.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~DatabaseProfile|FullyQualifiedName~ControlPlane|FullyQualifiedName~DataProtection|FullyQualifiedName~Override"`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~DatabaseProfile|FullyQualifiedName~Legacy|FullyQualifiedName~ControlPlane"`
- Record the control-plane root path behavior, override behavior, and legacy-discovery test names in the execution report.
- If cross-restart encryption proof cannot be executed, mark the subbundle `Blocked`.

## Browser Validation Logging

- `N/A` — no end-user browser-visible behavior should be claimed complete in this subbundle.
- If a read-only diagnostics page or log output is temporarily used during implementation, do not treat it as product proof; keep browser analytics `N/A` for this phase.

## Progression Gate

- The active-profile catalog, key-ring persistence, and override rules must be proven before subbundle 03 may resolve providers dynamically.
- The execution report must show successful tests for catalog persistence and control-plane secret protection.

## Suggested Agent Prompt

```text
Implement subbundle 02 only.

Build the app-level control plane:
- database profile catalog
- active-profile persistence
- persisted DataProtection keys
- encrypted control-plane secrets
- override compatibility
- legacy default-workspace discovery

Do not implement runtime switching or UI yet.
Run the listed unit/integration tests and record evidence honestly.
```
