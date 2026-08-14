# Core portability prepared findings

These findings are preparation evidence, not a substitute for the mandatory local scan.

## F-001 — P0: Configuration paths

**Paths:** `src/App/CanDoItAll.Web/appsettings.Development.json`; `src/App/CanDoItAll.Web/Properties/launchSettings.json`

**Current observation:** Development workspace and control-plane roots are encoded as %LOCALAPPDATA%\CanDoItAll\...

**Risk:** Linux and macOS receive unresolved or semantically invalid roots before deeper portability work can even be exercised.

**Required direction:** Introduce portable root defaults and explicit legacy Windows token compatibility; remove host-specific roots from shared development configuration.

**Confidence:** `Verified`

## F-002 — P0: Logical path normalization

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`; `src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStoragePathPolicy.cs`; `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathPolicy.cs`

**Current observation:** Infrastructure policies only replace Path.AltDirectorySeparatorChar while the MAF policy explicitly converts backslash. These produce different meanings on Unix.

**Risk:** A persisted logical locator can resolve differently depending on the caller, causing inaccessible artifacts, duplicate records, or containment defects.

**Required direction:** Define one logical-path contract with '/' serialization and field-specific legacy backslash readers; keep physical path parsing separate.

**Confidence:** `Verified`

## F-003 — P0: Path containment

**Paths:** `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimePathResolver.cs`

**Current observation:** Containment and root de-duplication use OrdinalIgnoreCase on every operating system.

**Risk:** Case-sensitive filesystems can treat distinct paths as equivalent in policy while the OS treats them as different.

**Required direction:** Use logical ordinal semantics and root-specific physical filesystem semantics; add actual Linux/macOS characterization.

**Confidence:** `Verified`

## F-004 — P1: External path aliases

**Paths:** `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathPolicy.cs`

**Current observation:** External-target aliases are shaped around Windows drive letters.

**Risk:** Unix absolute roots cannot be represented consistently and migration between hosts becomes ambiguous.

**Required direction:** Version a platform-neutral alias format that identifies an allowed root without embedding drive-letter assumptions.

**Confidence:** `Verified`

## F-005 — P1: Path taxonomy

**Paths:** `src/Foundation/CanDoItAll.Infrastructure`; `src/MAF`; `src/Modules/CanDoItAll.Modules.Workbench`

**Current observation:** Routes, logical locators, physical paths, executable paths, and command text are often represented as plain strings with local normalization.

**Risk:** A broad separator replacement can corrupt URLs, Unix filenames containing backslash, or intentionally host-bound paths.

**Required direction:** Inventory and classify every persisted and runtime path-bearing field before implementation; apply transformations only by field type.

**Confidence:** `High`

## F-006 — P1: Filesystem case semantics

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/Storage`; `tools/App/CanDoItAll.Manager`; `src/MAF`

**Current observation:** Many sets, locks, and path comparisons use OrdinalIgnoreCase globally, while some use OperatingSystem.IsWindows as a proxy for filesystem behavior.

**Risk:** macOS can be case-sensitive or case-insensitive by volume, and Linux paths can collide in application state despite being distinct on disk.

**Required direction:** Use ordinal semantics for logical identifiers and a root/volume-specific physical path policy for filesystem equality.

**Confidence:** `Verified pattern`

## F-007 — P1: Deterministic enumeration

**Paths:** `src/Foundation`; `src/MAF`; `tools/App/CanDoItAll.Manager`

**Current observation:** Filesystem enumeration is not uniformly ordered before producing persistent or decision-bearing results.

**Risk:** Linux, Windows, and macOS may return different order, changing plans, fingerprints, receipts, tests, or agent context.

**Required direction:** Order by normalized logical key with an explicit comparer at every decision or persistence boundary.

**Confidence:** `Requires A00 full scan`

## F-008 — P0: Symlink and reparse containment

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStoragePathPolicy.cs`; `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`

**Current observation:** Existing reparse checks are valuable but duplicated and do not constitute a proven race-resistant cross-platform contract.

**Risk:** A symlink swap or inconsistent check can escape trusted roots during read, write, open, or process execution.

**Required direction:** Define one deny-by-default managed-root link policy, validate existing ancestors immediately before access, and document residual TOCTOU limits.

**Confidence:** `Verified design gap`

## F-009 — P1: Atomic persistence

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`; `src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`

**Current observation:** Several writes are direct, while other writes use temporary files with only in-process coordination.

**Risk:** Crashes or concurrent processes can leave truncated catalog, secret, or artifact state.

**Required direction:** Centralize same-volume temporary write, flush, atomic replace, bounded cross-process lock, and recovery rules.

**Confidence:** `Verified`

## F-010 — P1: Portable filenames

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`

**Current observation:** Filename sanitization depends on Path.GetInvalidFileNameChars, which is platform-specific.

**Risk:** The same artifact name can serialize to different physical names on different hosts or remain invalid after transfer.

**Required direction:** Define an application-level portable filename policy and preserve the original display name separately.

**Confidence:** `Verified`

## F-011 — P0: Unix permissions

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/ControlPlane`; `src/Modules/CanDoItAll.Modules.Security`

**Current observation:** No explicit 0700/0600 policy is applied to control-plane, key-ring, vault-key, or secret files.

**Risk:** Default umask and service-account configuration can expose protected local state to other users.

**Required direction:** Apply and verify restrictive Unix modes after creation and migration; fail closed for secret-bearing state that cannot be hardened.

**Confidence:** `Verified absence in inspected files`

## F-012 — P1: File watchers

**Paths:** `tools/App/CanDoItAll.Manager/TailwindWatchSupervisorService.cs`

**Current observation:** FileSystemWatcher errors schedule a rebuild, but there is no unified generation/fingerprint/rescan contract.

**Risk:** Overflow, coalescing, rename behavior, or platform differences can silently miss changes.

**Required direction:** Treat watchers as hints, debounce by generation, and confirm state through deterministic rescan/fingerprint with polling fallback.

**Confidence:** `Verified`

## F-013 — P0: Host-bound persisted paths

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`; `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApplicationPreferences.cs`; `src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageBootstrapCatalogPolicy.cs`

**Current observation:** Absolute workspace roots and preferred executable paths are persisted without platform/host affinity or rebind state.

**Risk:** A profile copied to another OS can reinterpret a Windows path as a relative Unix name or silently select the wrong storage/application.

**Required direction:** Version host-bound path records, detect foreign syntax, mark unresolved, and require explicit rebind or migration.

**Confidence:** `Verified`

## F-014 — P1: Application data roots

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ControlPlanePaths.cs`; `src/Foundation/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`

**Current observation:** Default roots rely on LocalApplicationData/fallback behavior without an explicit Windows/Linux/macOS data/config/state policy.

**Risk:** Interactive, service, and headless deployments can place mutable or secret data in surprising locations.

**Required direction:** Define explicit platform defaults, service overrides, path purpose, ownership, and migration behavior.

**Confidence:** `Verified`

## F-015 — P0: Secret provider selection

**Paths:** `src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`

**Current observation:** Auto selects MacOsKeychain on macOS and LinuxSecretService on Linux, but both are UnsupportedSecretVault implementations.

**Risk:** Startup may appear healthy while the first secret operation fails, blocking providers, storage, MCP, and plugins.

**Required direction:** Implement and prove each advertised provider or make capability selection fail fast with actionable diagnostics.

**Confidence:** `Verified`

## F-016 — P0: File vault master key

**Paths:** `src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`

**Current observation:** DataProtectionFileVault creates a random AES key and writes it as plaintext Base64 beside the ciphertext.

**Risk:** Copying or reading the vault directory reveals both ciphertext and its decrypting key; the name overstates protection.

**Required direction:** Remove it from production Auto, require an externally protected wrapping key or an OS/remote vault, and preserve only an explicit test/development mode.

**Confidence:** `Verified`

## F-017 — P0: Data Protection key ring

**Paths:** `src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

**Current observation:** ASP.NET Data Protection keys are persisted to the filesystem without an explicit cross-platform at-rest protector.

**Risk:** Legacy secret records and control-plane database passwords depend on key files whose confidentiality is not protected by the current configuration.

**Required direction:** Bootstrap key-ring protection independently of the ring itself; support DPAPI on Windows and explicit secure macOS/Linux/headless strategies.

**Confidence:** `Verified`

## F-018 — P0: Secret migration graph

**Paths:** `src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs`; `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`; `src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`

**Current observation:** Three persistence mechanisms coexist: legacy Data Protection secret payloads, control-plane Data Protection passwords, and new vault references.

**Risk:** Changing one key location/provider can make the other two unreadable or create partial migrations.

**Required direction:** Create an explicit migration state machine with source-side decryption, staged destination writes, verification, rollback, and per-record versioning.

**Confidence:** `Verified`

## F-019 — P1: Secret write durability

**Paths:** `src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs`

**Current observation:** Vault key/payload writes lack a unified atomic, permission, cross-process, rotation, and repair contract.

**Risk:** Concurrent startup or crash can produce mismatched key/payload generations.

**Required direction:** Use versioned envelopes, atomic commits, bounded locks, restrictive modes, rotation checkpoints, and explicit recovery tooling.

**Confidence:** `Verified`

## F-020 — P0: Composition truthfulness

**Paths:** `src/App/CanDoItAll.Web/Program.cs`; `src/Modules/CanDoItAll.Modules.Security/SecurityModuleServiceCollectionExtensions.cs`

**Current observation:** Current runtime selection can register a nominal provider that is not operational on the host.

**Risk:** Feature UI and readiness can claim support that will fail only during work execution.

**Required direction:** Perform startup capability probes for mandatory providers and expose bounded, non-secret readiness diagnostics for optional features.

**Confidence:** `Verified`

## F-021 — P1: Platform abstractions

**Paths:** `src/Foundation`; `src/MAF`; `tools/App`

**Current observation:** Platform checks are distributed, but a single broad platform service would also violate current ownership boundaries.

**Risk:** Either branch sprawl or a god abstraction can couple unrelated modules and make testing harder.

**Required direction:** Use small purpose-owned ports and leaf adapters selected at composition; common code continues using cross-platform .NET APIs directly.

**Confidence:** `Architecture conclusion`

## F-022 — P0: CI

**Paths:** `.github/workflows-disabled/ci.yml`

**Current observation:** The only discovered CI workflow is disabled. Its main application build/test runs only on Windows.

**Risk:** Cross-platform regressions and even general development regressions can merge without an active repository gate.

**Required direction:** Restore an active workflow with Windows, Ubuntu, and macOS restore/build/stable-test gates plus targeted runtime proofs.

**Confidence:** `Verified`

## F-023 — P1: Installation

**Paths:** `tools/install/Install-CanDoItAllWebApp.ps1`

**Current observation:** The installed-web-app path is Windows/PowerShell oriented.

**Risk:** Linux/macOS support remains a developer-only claim with no repeatable service/install/runbook path.

**Required direction:** Retain the Windows installer, add Unix publish/service scripts or a small cross-platform installer boundary, and document service-user paths and rollback.

**Confidence:** `Verified`

## F-041 — P2: Portable foundation

**Paths:** `CanDoItAll.slnx`; `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`

**Current observation:** Projects target neutral net10.0 and the central workspace process host already uses direct typed arguments.

**Risk:** None; this reduces required rewrite scope.

**Required direction:** Preserve and extend these foundations rather than replacing them with a new parallel runtime.

**Confidence:** `Verified`

## F-042 — P2: Current architecture

**Paths:** `src/Processes`; `src/Processes/Drivers`; `src/Foundation/CanDoItAll.Security.Abstractions`

**Current observation:** Recent refactors created clearer process, runtime, and security boundaries.

**Risk:** Portability changes could accidentally collapse these new boundaries.

**Required direction:** Treat the latest architecture as authoritative and add portability through purpose-owned adapters.

**Confidence:** `Verified`
