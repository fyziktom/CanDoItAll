# Command catalog

Commands are templates. Record the exact command, host, SDK, dependency mode, exit code, duration, and evidence path.

## Anchor

```powershell
git rev-parse HEAD
git status --short
dotnet --info
```

## Build once per checkpoint

```powershell
dotnet restore ./CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=false
./tools/Validation/Test-RuntimePortability.ps1 -Configuration Release -UseLocalCanDoItAllLibraries:$false -BuildOnly
```

## Exact affected tests

```powershell
dotnet test <test-project.csproj> -c Release --no-build --no-restore --filter "FullyQualifiedName=<exact-name>" --logger "trx;LogFileName=<name>.trx"
```

## Runtime gate

```powershell
./tools/Validation/Test-RuntimePortability.ps1 -Scope All -SkipBuild -Configuration Release
```

`-BuildOnly` performs the single solution build and writes a durable stamp. Only use `-SkipBuild` against that exact stamp; the runner rejects commit, source, configuration, dependency-mode, SDK, dependency-anchor, catalog, assembly-path, or assembly-hash drift before starting tests.

## Stable suite — scheduled only

```powershell
dotnet test ./CanDoItAll.slnx -c Release --no-build --no-restore --filter "Category!=Playwright&Category!=LiveProcess&Category!=LongRunning&Category!=Quarantined&RequiresHostDocker!=true" -p:UseLocalCanDoItAllLibraries=false /m:1
```

Run only at C1 when authorized by the invalidation policy and at final M08.

## Docker local stack

```powershell
New-Item -ItemType Directory -Force ./.secrets | Out-Null
[System.IO.File]::WriteAllText((Resolve-Path ./.secrets).Path + [IO.Path]::DirectorySeparatorChar + 'db-password', [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)))
docker compose --env-file .env.example -p candoitall-unix-merge-candidate up -d --build --wait
docker compose --env-file .env.example -p candoitall-unix-merge-candidate ps --all
docker compose --env-file .env.example -p candoitall-unix-merge-candidate down --remove-orphans
Remove-Item -LiteralPath ./.secrets/db-password -Force
```

The cleanup command belongs in a `finally` block in automation.
