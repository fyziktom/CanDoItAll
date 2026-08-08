param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path $RepoRoot).Path
$bundle = Split-Path -Parent $PSScriptRoot
$proofRoot = Join-Path $bundle "validation-output"
New-Item -ItemType Directory -Force -Path $proofRoot | Out-Null

Push-Location $repo
try {
    python "$bundle/scripts/scan_affected_runtime_callers.py" --repo-root $repo --json-out "$proofRoot/affected-callers.json"
    python "$bundle/scripts/report_project_references.py" --repo-root $repo --json-out "$proofRoot/project-references.json"
    python "$bundle/scripts/check_architecture_guards.py" --repo-root $repo
    python "$bundle/scripts/check_cutover_guards.py" --repo-root $repo

    dotnet build "CanDoItAll.slnx" --configuration $Configuration
    dotnet test "tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj" --configuration $Configuration --no-build
    dotnet test "tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj" --configuration $Configuration --no-build
    dotnet test "tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj" --configuration $Configuration --no-build
}
finally {
    Pop-Location
}
