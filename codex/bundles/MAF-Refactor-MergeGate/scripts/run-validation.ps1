param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")
Push-Location $repoRoot
try {
    python (Join-Path $PSScriptRoot "validate_bundle_structure.py")
    python (Join-Path $PSScriptRoot "check_merge_blockers.py")

    dotnet build CanDoItAll.slnx --configuration $Configuration

    dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj `
        --configuration $Configuration --no-build `
        --filter "FullyQualifiedName~ExecutionGovernance|FullyQualifiedName~AuthorityProvider|FullyQualifiedName~ToolGovernancePipeline|FullyQualifiedName~ProcessLeaseCleanup|FullyQualifiedName~FileLlmConversationStore|FullyQualifiedName~LlmConversationService|FullyQualifiedName~ProviderBackedLlmInvocationAdapter|FullyQualifiedName~WorkflowLlm"

    Write-Host "Focused validation passed. SB09 still requires full Unit, Integration, architecture, and application-smoke gates."
}
finally {
    Pop-Location
}
