param([Parameter(Mandatory)][Collections.IDictionary]$Environment)
$ErrorActionPreference = 'Stop'
$launchSettingsPath = 'C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Properties/launchSettings.json'
$settings = Get-Content -LiteralPath $launchSettingsPath -Raw | ConvertFrom-Json -AsHashtable
$profile = $settings.profiles.http
if ($null -eq $profile -or $profile.commandName -cne 'Project' -or
    $Environment['DOTNET_LAUNCH_PROFILE'] -cne 'http' -or
    $profile.applicationUrl -cne $Environment['ASPNETCORE_URLS']) {
    throw 'The native launch profile no longer matches the captured runtime contract.'
}
$allowedKeys = @('commandName', 'dotnetRunMessages', 'launchBrowser', 'applicationUrl', 'environmentVariables')
if (@($profile.Keys | Where-Object { $_ -cnotin $allowedKeys }).Count -gt 0) {
    throw 'The native launch profile contains an unreviewed launch override.'
}
foreach ($key in $profile.environmentVariables.Keys) {
    if (!$Environment.Contains($key) -or [string]$profile.environmentVariables[$key] -cne [string]$Environment[$key]) {
        throw 'A native launch-profile environment override differs from the captured runtime.'
    }
}
[ordered]@{
    LaunchSettingsSha256 = (Get-FileHash -LiteralPath $launchSettingsPath -Algorithm SHA256).Hash
    VerifiedProfileEnvironmentOverrideCount = $profile.environmentVariables.Count
}
