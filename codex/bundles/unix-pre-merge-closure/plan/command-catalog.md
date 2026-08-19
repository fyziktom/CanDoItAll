# Command catalog

Commands are examples. Adapt result directories, but retain package mode and
exact filters.

## Restore and build

```powershell
dotnet restore ./CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=false

dotnet build ./CanDoItAll.slnx `
  --configuration Release `
  --no-restore `
  -p:UseLocalCanDoItAllLibraries=false `
  /m:1
```

## Focused process-plan migration

```powershell
dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj `
  --configuration Release `
  --no-build --no-restore `
  --filter "FullyQualifiedName~ProcessPlanMigrationIntegrationTests" `
  --logger "trx;LogFileName=process-plan-migration.trx" `
  --results-directory ./artifacts/pre-merge/F01 `
  -p:UseLocalCanDoItAllLibraries=false
```

## Focused process ownership

```powershell
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj `
  --configuration Release `
  --no-build --no-restore `
  --filter "FullyQualifiedName~LocalWorkspaceProcessHostTests" `
  --logger "trx;LogFileName=process-ownership.trx" `
  --results-directory ./artifacts/pre-merge/F02 `
  -p:UseLocalCanDoItAllLibraries=false
```

## Focused Manager registry/recovery

```powershell
dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj `
  --configuration Release `
  --no-build --no-restore `
  --filter "FullyQualifiedName~ManagerProcessOwnershipTests" `
  --logger "trx;LogFileName=manager-registry.trx" `
  --results-directory ./artifacts/pre-merge/F03 `
  -p:UseLocalCanDoItAllLibraries=false
```

## Focused MAF 1.17 gate

```powershell
$filter = @(
  "FullyQualifiedName~MafPackageBaselineReflectionTests",
  "FullyQualifiedName~MafApprovalSessionRoundTripTests",
  "FullyQualifiedName~MafRuntimeArchitectureServicesTests",
  "FullyQualifiedName~CanonicalAgentExecutionAuthorityResolverTests",
  "FullyQualifiedName~AgentExecutionActivityCoordinatorTests"
) -join "|"

dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj `
  --configuration Release `
  --no-build --no-restore `
  --filter $filter `
  --logger "trx;LogFileName=maf-1.17-focused.trx" `
  --results-directory ./artifacts/pre-merge/F05 `
  -p:UseLocalCanDoItAllLibraries=false

dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj `
  --configuration Release `
  --no-build --no-restore `
  --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests" `
  --logger "trx;LogFileName=maf-1.17-integration.trx" `
  --results-directory ./artifacts/pre-merge/F05 `
  -p:UseLocalCanDoItAllLibraries=false
```

## Runtime portability build stamp and focused catalog

```powershell
./tools/Validation/Test-RuntimePortability.ps1 `
  -Configuration Release `
  -UseLocalCanDoItAllLibraries $false `
  -BuildOnly `
  -ResultsDirectory ./artifacts/pre-merge/runtime

./tools/Validation/Test-RuntimePortability.ps1 `
  -Configuration Release `
  -UseLocalCanDoItAllLibraries $false `
  -SkipBuild `
  -Scope Unit `
  -ResultsDirectory ./artifacts/pre-merge/runtime

./tools/Validation/Test-RuntimePortability.ps1 `
  -Configuration Release `
  -UseLocalCanDoItAllLibraries $false `
  -SkipBuild `
  -Scope Integration `
  -ResultsDirectory ./artifacts/pre-merge/runtime
```

## Docker policy and disposable smoke

```powershell
./tools/Validation/Test-Docker.ps1

New-Item -ItemType Directory -Force ./.secrets | Out-Null
[IO.File]::WriteAllText(
  (Join-Path (Resolve-Path ./.secrets) "db-password"),
  [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant())

docker compose --env-file ./.env.example build app
docker compose --env-file ./.env.example run --rm --no-deps app sh -lc "command -v setsid"
docker compose --env-file ./.env.example up -d --wait --wait-timeout 180
docker compose --env-file ./.env.example ps --all
docker compose --env-file ./.env.example down --volumes --remove-orphans
Remove-Item -Force ./.secrets/db-password
```

Cleanup must execute from `finally`/`if: always()` in automation.
