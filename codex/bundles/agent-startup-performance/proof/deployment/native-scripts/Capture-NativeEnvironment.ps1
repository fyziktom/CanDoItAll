$ErrorActionPreference = 'Stop'
$repositoryRoot = 'C:/repositories/CanDoItAll'
$deploymentRoot = Join-Path $repositoryRoot '.artifacts/agent-startup-performance/deployment'
$privateRoot = Join-Path $deploymentRoot 'private'
$expectedAppPid = 58036
$expectedParentPid = 22496
$expectedDllHash = '60F188E37C58754076D6F462C236120EA7B63FB55ADC55C0A8924428F603A83D'
$process = Get-Process -Id $expectedAppPid -ErrorAction Stop
$cim = Get-CimInstance Win32_Process -Filter "ProcessId=$expectedAppPid"
if ($cim.ParentProcessId -ne $expectedParentPid -or
    $process.Path -ne (Join-Path $repositoryRoot 'src/App/CanDoItAll.Web/bin/Release/net10.0/CanDoItAll.Web.exe')) {
    throw 'Native baseline process identity changed.'
}
$dllPath = Join-Path $repositoryRoot 'src/App/CanDoItAll.Web/bin/Release/net10.0/CanDoItAll.Web.dll'
if ((Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash -ne $expectedDllHash) {
    throw 'Native baseline assembly changed.'
}
Add-Type -Path (Join-Path $repositoryRoot '.artifacts/agent-startup-performance/diagnostics/bin/Release/net10.0/Microsoft.Diagnostics.NETCore.Client.dll')
$original = [Microsoft.Diagnostics.NETCore.Client.DiagnosticsClient]::new($expectedAppPid).GetProcessEnvironment()
if ($original['ASPNETCORE_URLS'] -ne 'http://localhost:5032' -or $original['ASPNETCORE_ENVIRONMENT'] -ne 'Development') {
    throw 'Unexpected native runtime environment.'
}
$canonical = [ordered]@{}
foreach ($key in @($original.Keys | Sort-Object -CaseSensitive)) {
    $canonical[$key] = [string]$original[$key]
}
$launchProfile = & (Join-Path $PSScriptRoot 'Assert-NativeLaunchProfile.ps1') -Environment $canonical
$bytes = [Text.Encoding]::UTF8.GetBytes(($canonical | ConvertTo-Json -Compress -Depth 3))
try {
    $environmentHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    if ($environmentHash -ne 'ABEF49599CD462046EEAF26A2CA59210C502BF3580A0A0FAC9EB4C49836E1051') {
        throw 'The frozen native environment changed.'
    }
    $protected = [Security.Cryptography.ProtectedData]::Protect($bytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
} finally {
    [Array]::Clear($bytes)
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
$aclSections = [Security.AccessControl.AccessControlSections]::Access -bor [Security.AccessControl.AccessControlSections]::Owner
$privateDirectory = [IO.DirectoryInfo]::new($privateRoot)
if (!$privateDirectory.Exists) {
    [IO.Directory]::CreateDirectory($privateRoot) | Out-Null
    $acl = [IO.FileSystemAclExtensions]::GetAccessControl(
        $privateDirectory, [Security.AccessControl.AccessControlSections]::Access)
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new(
        $identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    [IO.FileSystemAclExtensions]::SetAccessControl($privateDirectory, $acl)
}
$privateDirectory.Refresh()
if (($privateDirectory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'The private environment directory cannot be a reparse point.'
}
$acl = [IO.FileSystemAclExtensions]::GetAccessControl($privateDirectory, $aclSections)
$rules = @($acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
$inheritance = [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit
if (!$acl.AreAccessRulesProtected -or $acl.GetOwner([Security.Principal.SecurityIdentifier]) -ne $identity -or
    $rules.Count -ne 1 -or $rules[0].IdentityReference -ne $identity -or
    $rules[0].AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or
    $rules[0].FileSystemRights -ne [Security.AccessControl.FileSystemRights]::FullControl -or
    $rules[0].InheritanceFlags -ne $inheritance -or $rules[0].IsInherited -or
    $rules[0].PropagationFlags -ne [Security.AccessControl.PropagationFlags]::None) {
    throw 'The private environment directory ACL is not restricted to the current user.'
}
$protectedPath = Join-Path $privateRoot 'native-environment.dpapi'
[IO.File]::WriteAllBytes($protectedPath, $protected)
$metadata = [ordered]@{
    CapturedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    AppPid = $expectedAppPid
    ParentPid = $expectedParentPid
    AppStartUtc = $process.StartTime.ToUniversalTime().ToString('O')
    AppPath = $process.Path
    OriginalDllSha256 = $expectedDllHash
    EnvironmentSha256 = $environmentHash
    EnvironmentEntryCount = $canonical.Count
    EnvironmentProtection = 'DPAPI CurrentUser; private directory ACL limited to the current user'
    Url = $original['ASPNETCORE_URLS']
    EnvironmentName = $original['ASPNETCORE_ENVIRONMENT']
    LaunchProfile = $original['DOTNET_LAUNCH_PROFILE']
    LaunchSettingsSha256 = $launchProfile.LaunchSettingsSha256
    VerifiedProfileEnvironmentOverrideCount = $launchProfile.VerifiedProfileEnvironmentOverrideCount
}
$metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $deploymentRoot 'native-original.json') -Encoding utf8NoBOM
$metadata | ConvertTo-Json -Compress
