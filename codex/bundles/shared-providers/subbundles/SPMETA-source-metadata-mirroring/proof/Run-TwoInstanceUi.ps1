param(
    [Parameter(Mandatory)] [string] $RunLabel,
    [string] $Filter = 'FullyQualifiedName=CanDoItAll.Tests.Playwright.SharedProviderTwoInstanceUiAcceptanceTests.Provider_empty_client_imports_shared_providers_and_runs_chat_image_and_vision'
)
$ErrorActionPreference = 'Stop'
$temporaryDirectory = Join-Path (Get-Location).Path '.artifacts\spmeta-runtime-secrets'
New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
$tokenPath = Join-Path $temporaryDirectory 'upstream-data-token'
try {
    docker cp candoitall-spui-upstream:/run/secrets/upstream-data-token $tokenPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Cannot read the isolated fixture credential.'
    }
    $env:CANDOITALL_SHARED_UI_SHARED_URL = 'http://127.0.0.1:5210'
    $env:CANDOITALL_SHARED_UI_CLIENT_URL = 'http://127.0.0.1:5212'
    $env:CANDOITALL_SHARED_UI_UPSTREAM_TOKEN_FILE = $tokenPath
    $env:CANDOITALL_SHARED_UI_EVIDENCE_DIRECTORY = Join-Path $PSScriptRoot "browser\$RunLabel"
    $env:CANDOITALL_SHARED_UI_VISION_IMAGE = 'C:\Users\lucys\AppData\Local\Temp\codex-clipboard-a51e0456-c818-472a-9056-ffe168b770e8.png'
    Write-Output "Two-instance UI validation start UTC: $([DateTimeOffset]::UtcNow.ToString('O'))"
    & (Join-Path $PSScriptRoot 'Run-FocusedTests.ps1') -Project tests\Playwright\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -NoBuild -ExpectedCount 1 -Filter $Filter -RunLabel $RunLabel
    $testExitCode = $LASTEXITCODE
} finally {
    if (Test-Path -LiteralPath $tokenPath) {
        Remove-Item -LiteralPath $tokenPath -Force
    }
    Write-Output 'Ephemeral local fixture credential removed; Docker secret volume preserved.'
}
exit $testExitCode
