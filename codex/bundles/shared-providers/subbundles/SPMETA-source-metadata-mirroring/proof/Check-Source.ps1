$ErrorActionPreference = 'Stop'
Start-Transcript -Path (Join-Path $PSScriptRoot 'transcripts\source-assertions.txt') -Force | Out-Null
try {
    Write-Output "SPMETA META-NAMES META-PRICES META-PRIVATE META-SETTINGS META-E2E source assertions; cwd=$((Get-Location).Path)"
    $checks = @(
        @('src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogProjection.cs', 'model.UpstreamModelId,', 'SharedProviderPriceMapper.ToCatalog', 'IsPrivateProvider = ProviderPricingDefaults.ResolveIsPrivateProvider'),
        @('src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderProfileMapper.cs', 'IsPrivateProvider = provider.IsPrivateProvider', 'ModelCatalog = Array.AsReadOnly', 'SharedProviderPriceMapper.ToRuntime'),
        @('src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderCanonicalRevision.cs', 'publication.IsPrivateProvider', 'model.Price'),
        @('src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs', 'if (provider.IsSourceManaged)', 'return provider;', 'ProviderPricingMetadata.Write(normalizedConfigurationJson, isPrivateProvider, modelPrices)'),
        @('src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/ProviderModelPricingEditor.razor', 'if (SourceManaged)', 'provider-pricing-unavailable', 'Provider.GetModelDisplayName(model)'),
        @('src/UI/CanDoItAll.Conversations.Components/ConversationProviderModelSelector.razor', 'GetModelDisplayName', 'Unavailable shared model', 'valueChangedExternally || Provider?.AllowsModelOverride == false'),
        @('src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderManagementService.cs', 'SharedProviderPublicationSnapshotReader.TryRead', 'SharedProviderAvailabilityState.IncompatibleContract')
    )
    foreach ($check in $checks) {
        $content = Get-Content -LiteralPath $check[0] -Raw
        foreach ($assertion in $check[1..($check.Length - 1)]) {
            if (-not $content.Contains($assertion)) {
                throw "Missing production assertion in $($check[0]): $assertion"
            }
            Write-Output "PASS $($check[0]): $assertion"
        }
    }
    $productionPaths = @(
        git diff --name-only -- src
        git ls-files --others --exclude-standard -- src
    ) | Where-Object { $_ -match '\.(cs|razor)$' } | Sort-Object -Unique
    Write-Output 'Anti-stub command: rg -n TODO|NotImplementedException|deterministic fixture|e2e-|spmeta over all changed production .cs/.razor files'
    & rg -n 'TODO|NotImplementedException|deterministic fixture|e2e-|spmeta' @productionPaths | Out-Host
    if ($LASTEXITCODE -ne 1) {
        throw 'Anti-stub scan returned a match or failed.'
    }
    $projects = @(git diff --name-only -- '*.csproj')
    if ($projects.Count -ne 0) {
        throw 'Unexpected project-reference scope change.'
    }
    $diff = git diff --unified=0 -- src
    if ($diff | Where-Object { $_ -match '^\+[^+].*partial\s+class' }) {
        throw 'Unexpected new partial-class boundary.'
    }
    Write-Output "PASS: $($productionPaths.Count) production files scanned; no stubs/fixture branches, project changes, or new partial boundaries. Exit code: 0"
} finally {
    Stop-Transcript | Out-Null
}
