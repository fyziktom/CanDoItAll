# Source hotspot map

| ID | Path | Area | Prepared observation | Evidence |
|---|---|---|---|---|
| ROOT-001 | `global.json` | Build | Pins .NET SDK 10.0.302. | Verified |
| ROOT-002 | `Directory.Build.props` | Build | Repository-wide build and package versions; no Windows-only TFM. | Verified |
| ROOT-003 | `CanDoItAll.slnx` | Architecture | Current solution includes the new Processes stack, process drivers, Security.Abstractions, and MAF runtime abstractions. | Verified |
| ROOT-004 | `.github/workflows-disabled/ci.yml` | CI | Former CI is disabled; application build/test is Windows-only while Ubuntu only exercises container policy. | Verified |
| APP-001 | `src/App/CanDoItAll.Web/Program.cs` | Composition | Composition root and host startup surface. | Verified |
| APP-002 | `src/App/CanDoItAll.Web/appsettings.json` | Configuration | Base settings keep desktop launch disabled and define runtime profiles. | Verified |
| APP-003 | `src/App/CanDoItAll.Web/appsettings.Development.json` | Configuration | Workspace and control-plane roots use %LOCALAPPDATA% and Windows separators. | Verified |
| APP-004 | `src/App/CanDoItAll.Web/Properties/launchSettings.json` | Configuration | Development profiles repeat Windows-only roots and enable desktop launch. | Verified |
| CORE-001 | `src/Foundation/CanDoItAll.Infrastructure/Configuration/AppOptions.cs` | Configuration | Defines storage and control-plane options and relative defaults. | Verified |
| CORE-002 | `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ControlPlanePaths.cs` | Paths | Expands environment variables and derives LocalApplicationData-based control-plane paths. | Verified |
| CORE-003 | `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs` | Control plane | Persists database profiles, encrypted passwords, and host filesystem workspace roots. | Verified |
| CORE-004 | `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApplicationPreferences.cs` | Control plane | Persists absolute preferred application executable paths. | Verified |
| CORE-005 | `src/Foundation/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | Composition | Persists ASP.NET Data Protection keys under the control plane. | Verified |
| CORE-006 | `src/Foundation/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs` | Paths | Workspace path guard uses native separator conversion that does not treat backslash as a legacy logical separator on Unix. | Verified |
| CORE-007 | `src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStoragePathPolicy.cs` | Filesystem | Storage root containment and reparse checks; separator semantics differ from MAF workspace policy. | Verified |
| CORE-008 | `src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs` | Storage | Direct non-atomic SaveAsync, temporary replace path, platform-dependent filename sanitization, and case-insensitive lock hashing. | Verified |
| CORE-009 | `src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageBootstrapCatalogPolicy.cs` | Storage | Treats persisted filesystem root equality according to the current OS. | Verified |
| SEC-001 | `src/Foundation/CanDoItAll.Security.Abstractions/CanDoItAll.Security.Abstractions.csproj` | Security | New abstractions project introduced by the MAF refactor. | Verified |
| SEC-002 | `src/Modules/CanDoItAll.Modules.Security/CanDoItAll.Modules.Security.csproj` | Security | References ProtectedData and Security.Abstractions. | Verified |
| SEC-003 | `src/Modules/CanDoItAll.Modules.Security/SecretVaults.cs` | Security | Auto selects macOS/Linux providers that are currently unsupported; file vault stores a Base64 master key beside ciphertext. | Verified |
| SEC-004 | `src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs` | Security | Secret records support vault references and legacy Data Protection payloads. | Verified |
| SEC-005 | `src/Modules/CanDoItAll.Modules.Security/SecurityModuleServiceCollectionExtensions.cs` | Security | Registers secret protector, vault factory, and runtime secret services. | Verified |
| SEC-006 | `src/Modules/CanDoItAll.Modules.Security/SecretRuntimeResolver.cs` | Security | Runtime secret resolution boundary; full local scan required after materialization. | Search-confirmed |
| SEC-007 | `src/Modules/CanDoItAll.Modules.Security/StorageSecretResolver.cs` | Security | Storage credential resolution boundary. | Search-confirmed |
| SEC-008 | `src/Modules/CanDoItAll.Modules.Security/PluginSecretBroker.cs` | Security | Plugin secret dispatch boundary. | Search-confirmed |
| MAF-001 | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathPolicy.cs` | Paths | Canonicalizes logical backslashes but has Windows-drive-shaped external target aliases. | Verified |
| MAF-002 | `src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimePathResolver.cs` | Paths | Uses OrdinalIgnoreCase containment on every OS. | Verified |
| OPS-001 | `tools/install/Install-CanDoItAllWebApp.ps1` | Installation | Windows-oriented installer and launcher generation. | Verified |
| TEST-001 | `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | Tests | Primary unit test project. | Verified |
| TEST-002 | `tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` | Tests | Integration test project. | Verified |
| TEST-003 | `tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj` | Tests | Browser validation project. | Verified |

All paths must be revalidated during A00/B00. Search-confirmed paths require direct content inspection.
