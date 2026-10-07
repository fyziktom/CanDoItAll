# CanDoItAll.Tests.Integration

Exercises host composition, APIs, PostgreSQL persistence, providers, plugins, workflows,
processes, AgentFramework execution, and module integration.

```powershell
dotnet test .\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Release
```

Tests that require external services must declare prerequisites and fail or skip
explicitly according to their test contract.

## Governed local-file launch

`ProjectStructureNativeLocalActionsTests` is an explicit `LiveProcess` / `HostPlatform`
journey for interactive Windows. It creates an isolated project and storage source through
native services, obtains two independent download leases, launches a harmless preferred
application, then verifies capability-loss and headless-host denial. It requires the same
PostgreSQL prerequisites as the other native integration tests.

Build the reviewed [probe source](Fixtures/NativeFileOpenProbe.cs.fixture) in a new ignored
directory. The probe only reads the one authorized test file and appends its path, byte
count, SHA-256 and process ID to a receipt. It refuses paths outside the test root.

```powershell
$probeRoot = Join-Path (Get-Location) 'artifacts/native-file-open-probe'
if (Test-Path -LiteralPath $probeRoot) {
    throw 'Inspect the existing probe and receipts; do not replay an unknown launch.'
}
New-Item -ItemType Directory -Path $probeRoot | Out-Null
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $probeRoot 'NativeFileOpenProbe.csproj')
Copy-Item -LiteralPath 'tests/Integration/CanDoItAll.Tests.Integration/Fixtures/NativeFileOpenProbe.cs.fixture' -Destination (Join-Path $probeRoot 'Program.cs')
dotnet build (Join-Path $probeRoot 'NativeFileOpenProbe.csproj') -c Release
if ($LASTEXITCODE -ne 0) {
    throw 'The probe build failed.'
}
$env:CANDOITALL_FILE_OPEN_PROBE = Join-Path $probeRoot 'bin/Release/net10.0/NativeFileOpenProbe.exe'
$env:CANDOITALL_FILE_OPEN_PROBE_RECEIPT = Join-Path $probeRoot 'launch.jsonl'
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'FullyQualifiedName~ProjectStructureNativeLocalActionsTests'
```

An accepted-node receipt is written before launch; a completed proof adds `.native.json`.
Retain failures and inspect all receipts before any separate attempt. The test never
substitutes a fake desktop launcher and never launches an unrelated file.
