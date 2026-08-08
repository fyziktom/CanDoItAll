param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryPath = "."
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path $RepositoryPath).Path
Push-Location $repo
try {
    python (Join-Path $PSScriptRoot "check_dependency_boundaries.py") $repo
    python (Join-Path $PSScriptRoot "check_followup_architecture.py") $repo
    dotnet build .\CanDoItAll.slnx -c Release
    dotnet test .\tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj -c Release --no-build
    dotnet test .\tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -c Release --no-build
    dotnet test .\tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -c Release --no-build
}
finally {
    Pop-Location
}
