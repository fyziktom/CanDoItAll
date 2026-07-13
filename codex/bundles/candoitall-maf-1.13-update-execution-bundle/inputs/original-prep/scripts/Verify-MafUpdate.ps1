param(
    [string]$Configuration = "Release",
    [switch]$SkipFocusedTests,
    [switch]$SkipBroadTests,
    [switch]$SkipPlaywright
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    Write-Host ""
    Write-Host "== $Name =="
    & $Command
}

Invoke-Step "dotnet info" {
    dotnet --info
}

Invoke-Step "restore" {
    dotnet restore CanDoItAll.slnx
}

Invoke-Step "build" {
    dotnet build CanDoItAll.slnx --configuration $Configuration --no-restore
}

if (-not $SkipFocusedTests) {
    Invoke-Step "focused unit tests" {
        dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration $Configuration --no-build --filter "FullyQualifiedName~MafAgentRuntime|FullyQualifiedName~ProviderDispatchLaneGate|FullyQualifiedName~ProviderRuntimeLifecycle|FullyQualifiedName~Finalizer|FullyQualifiedName~ToolProviderComposition|FullyQualifiedName~Workflow"
    }

    Invoke-Step "focused integration tests" {
        dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration $Configuration --no-build --filter "FullyQualifiedName~AgentFramework|FullyQualifiedName~Process|FullyQualifiedName~ProjectStructureAgent"
    }
}

if (-not $SkipBroadTests) {
    Invoke-Step "unit tests" {
        dotnet test tests\Unit\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --configuration $Configuration
    }

    Invoke-Step "integration tests" {
        dotnet test tests\Integration\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration $Configuration
    }

    Invoke-Step "component tests" {
        dotnet test tests\Components\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --configuration $Configuration
    }
}

if (-not $SkipPlaywright) {
    Invoke-Step "playwright tests" {
        dotnet test tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration $Configuration
    }
}

if (Get-Command rg -ErrorAction SilentlyContinue) {
    Invoke-Step "stale stable MAF package scan" {
        rg 'Microsoft\.Agents\.AI" Version="1\.8\.0|Microsoft\.Agents\.AI\.OpenAI" Version="1\.8\.0|Microsoft\.Agents\.AI\.Workflows" Version="1\.8\.0' src tests tools -g "*.csproj"
        if ($LASTEXITCODE -eq 0) {
            throw "Found stale stable MAF 1.8 package references."
        }
        if ($LASTEXITCODE -ne 1) {
            throw "Package scan failed with exit code $LASTEXITCODE."
        }
    }

    Invoke-Step "architecture guardrail scan" {
        rg 'registers .*ProcessAgentRuntimeToolProvider|new ProcessAgentRuntimeToolProvider|class ProcessAgentRuntimeToolProvider|Add.*ProcessAgentRuntimeToolProvider|Current direct runtime tools: 23|/api/processes/definitions|/api/processes/templates|/api/processes/runs/\{runId\}/detail|ProcessManagerTools' docs src tests -g "*.md" -g "*.cs" -g "*.json"
        if ($LASTEXITCODE -eq 0) {
            throw "Found possible stale or resurrected process direct-tool/API references."
        }
        if ($LASTEXITCODE -ne 1) {
            throw "Architecture guardrail scan failed with exit code $LASTEXITCODE."
        }
    }

    Invoke-Step "diff hygiene" {
        git diff --check
    }
}
else {
    Write-Warning "ripgrep was not found. Skipping source scans."
}
