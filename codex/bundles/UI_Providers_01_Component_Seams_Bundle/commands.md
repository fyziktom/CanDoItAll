# Reproducible validation

Run from the repository root using Release and /m:1. Restore only if required by environment; each affected assembly must be rebuilt before --no-build.

Production: dotnet build src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj -c Release --no-restore -m:1
Browser host: dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release --no-restore -m:1

Owning Unit filter (27 cases): FullyQualifiedName~ProviderProfilesSessionTests|FullyQualifiedName~ProviderProfilesReadsTests
Owning Components filter (28 cases): FullyQualifiedName~ProviderProfilesSeamTests|FullyQualifiedName~ProviderAdministrationLayoutTests|FullyQualifiedName~ProviderCatalogRefreshTests|FullyQualifiedName~AgentProviderProfilesPanelPricingTests|FullyQualifiedName~SecretProviderSelectionTests
SB09 filter (8 cases): FullyQualifiedName~AgentSeamFinalizationTests

Use dotnet test with --list-tests --filter first, verify the declared runtime case inventory, then execute with the identical filter. Unit and Components csproj paths are under tests/Unit/CanDoItAll.Tests.Unit and tests/Components/CanDoItAll.Tests.Components respectively.

The broader checkpoint used the Unit, Components and Integration csproj paths with the exact stable filter from docs/testing.md:
Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&Category!=UnixRuntimePortability&RequiresHostDocker!=true

Discovery expansion and the isolated workflow rerun are explained in reviews/validation-scope.md. No broad repeat is required without a new invalidation trigger.

Portability: run both Python checker self-test scripts, scan_portability.py --repo-root . --output <task-json> --tracked-only --include-untracked, then enforce_portability_baseline.py --scan <task-json> --baseline tools/Validation/Portability/portability-risk-baseline.json. --include-untracked is essential before adding new files to Git. No baseline change was needed.

Documentation: ./tools/Validation/Test-Documentation.ps1. The unchanged 118 tracked historical logs are a separate repository blocker.
