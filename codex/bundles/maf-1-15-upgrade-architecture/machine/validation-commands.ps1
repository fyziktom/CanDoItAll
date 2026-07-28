[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepositoryRoot).Path
$outputRoot = Join-Path $root ".artifacts/maf-1.15-validation"
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

function Invoke-LoggedStep {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [scriptblock]$Action
    )

    $logPath = Join-Path $outputRoot ($Name + ".log")
    Write-Host "Running $Name"
    & $Action 2>&1 | Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) {
        throw "Step '$Name' failed with exit code $LASTEXITCODE. See $logPath"
    }
}

Push-Location $root
try {
    dotnet --info | Set-Content -Path (Join-Path $outputRoot "dotnet-info.txt") -Encoding utf8
    git rev-parse HEAD | Set-Content -Path (Join-Path $outputRoot "git-head.txt") -Encoding utf8
    git status --short | Set-Content -Path (Join-Path $outputRoot "git-status.txt") -Encoding utf8

    Invoke-LoggedStep "01-restore-solution" {
        dotnet restore "CanDoItAll.slnx"
    }

    $projects = [ordered]@{
        "02-package-main" = "src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj"
        "03-package-workflows" = "src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj"
        "04-package-hosting" = "src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj"
    }

    foreach ($entry in $projects.GetEnumerator()) {
        $target = Join-Path $outputRoot ($entry.Key + ".txt")
        dotnet list $entry.Value package --include-transitive | Set-Content -Path $target -Encoding utf8
        if ($LASTEXITCODE -ne 0) {
            throw "Package listing failed for $($entry.Value)."
        }
    }

    $mainBinlog = Join-Path $outputRoot "05-build-main-maf.binlog"
    Invoke-LoggedStep "05-build-main-maf" {
        dotnet build $projects["02-package-main"] --no-restore "-bl:$mainBinlog"
    }

    $workflowBinlog = Join-Path $outputRoot "06-build-workflow-adapter.binlog"
    Invoke-LoggedStep "06-build-workflow-adapter" {
        dotnet build $projects["03-package-workflows"] --no-restore "-bl:$workflowBinlog"
    }

    $hostingBinlog = Join-Path $outputRoot "07-build-hosting.binlog"
    Invoke-LoggedStep "07-build-hosting" {
        dotnet build $projects["04-package-hosting"] --no-restore "-bl:$hostingBinlog"
    }

    $solutionBinlog = Join-Path $outputRoot "08-build-solution.binlog"
    Invoke-LoggedStep "08-build-solution" {
        dotnet build "CanDoItAll.slnx" --no-restore "-bl:$solutionBinlog"
    }

    Invoke-LoggedStep "09-test-solution" {
        dotnet test "CanDoItAll.slnx" --no-build --logger "trx;LogFileName=maf-1.15-tests.trx" --results-directory $outputRoot
    }
}
finally {
    Pop-Location
}

Write-Host "MAF 1.15 validation output: $outputRoot"
