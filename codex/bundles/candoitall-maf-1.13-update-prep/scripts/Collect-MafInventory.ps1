param(
    [string]$OutputPath = ".artifacts\maf-package-inventory"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

$projects = @(
    "src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj",
    "src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj"
)

foreach ($project in $projects) {
    if (-not (Test-Path $project)) {
        throw "Project not found: $project"
    }

    $safeName = ($project -replace '[\\/:*?"<>|]', '_')
    dotnet list $project package | Tee-Object -FilePath (Join-Path $OutputPath "$safeName.packages.txt")
    dotnet list $project package --outdated --include-prerelease | Tee-Object -FilePath (Join-Path $OutputPath "$safeName.outdated.txt")
}

if (Get-Command rg -ErrorAction SilentlyContinue) {
    rg "Microsoft\.Agents\.AI|Microsoft\.Extensions\.AI" src tests tools -g "*.csproj" |
        Tee-Object -FilePath (Join-Path $OutputPath "maf-package-references.txt")
}
else {
    Write-Warning "ripgrep was not found. Skipping repository-wide package scan."
}
