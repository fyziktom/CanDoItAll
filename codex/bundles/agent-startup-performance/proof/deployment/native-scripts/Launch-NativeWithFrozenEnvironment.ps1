param(
    [Parameter(Mandatory)][string]$ProtectedEnvironment,
    [Parameter(Mandatory)][string]$RecordPath,
    [string]$ArtifactsPath
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = 'C:/repositories/CanDoItAll'
$deploymentRoot = Join-Path $repositoryRoot '.artifacts/agent-startup-performance/deployment'
foreach ($path in @($ProtectedEnvironment, $RecordPath)) {
    if (![IO.Path]::GetFullPath($path).StartsWith([IO.Path]::GetFullPath($deploymentRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Launcher paths must remain inside this deployment artifact directory.'
    }
}
$protectedBytes = [IO.File]::ReadAllBytes($ProtectedEnvironment)
$originalMetadata = Get-Content -LiteralPath (Join-Path $deploymentRoot 'native-original.json') -Raw | ConvertFrom-Json
$plainBytes = [Security.Cryptography.ProtectedData]::Unprotect($protectedBytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
try {
    $environmentHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($plainBytes))
    if ($environmentHash -ne $originalMetadata.EnvironmentSha256) {
        throw 'The decrypted native environment does not match its frozen fingerprint.'
    }
    $originalEnvironment = [Text.Encoding]::UTF8.GetString($plainBytes) | ConvertFrom-Json -AsHashtable
} finally {
    [Array]::Clear($plainBytes)
}
$launchProfile = & (Join-Path $PSScriptRoot 'Assert-NativeLaunchProfile.ps1') -Environment $originalEnvironment
if ($launchProfile.LaunchSettingsSha256 -ne $originalMetadata.LaunchSettingsSha256 -or
    $originalEnvironment.Count -ne $originalMetadata.EnvironmentEntryCount) {
    throw 'The native launch settings or environment inventory changed after capture.'
}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class StartupLauncherConsoleLifetime {
    private delegate bool ConsoleControlHandler(uint controlType);
    private static readonly ConsoleControlHandler Handler = controlType => controlType == 0;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleControlHandler handler, bool add);
    public static void KeepDrainingDuringChildShutdown() {
        if (!SetConsoleCtrlHandler(Handler, true)) {
            throw new InvalidOperationException("Cannot retain the launcher while its child shuts down.");
        }
    }
}
'@
[StartupLauncherConsoleLifetime]::KeepDrainingDuringChildShutdown()
$info = [Diagnostics.ProcessStartInfo]::new()
$info.FileName = 'C:/Program Files/dotnet/dotnet.exe'
$info.WorkingDirectory = $repositoryRoot
$info.UseShellExecute = $false
$info.CreateNoWindow = $false
$info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$info.Environment.Clear()
foreach ($key in $originalEnvironment.Keys) {
    $info.Environment[$key] = [string]$originalEnvironment[$key]
}
$arguments = @('run', '--project', 'src/App/CanDoItAll.Web', '--configuration', 'Release', '--no-build', '--launch-profile', 'http')
if ($ArtifactsPath) {
    $resolvedArtifacts = [IO.Path]::GetFullPath($ArtifactsPath)
    if (!$resolvedArtifacts.Equals([IO.Path]::GetFullPath((Join-Path $repositoryRoot '.artifacts/asn-20260831')), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected candidate artifact directory.'
    }
    $arguments += @('--artifacts-path', $resolvedArtifacts)
}
foreach ($argument in $arguments) {
    $info.ArgumentList.Add($argument)
}
$process = [Diagnostics.Process]::new()
$process.StartInfo = $info
$stdoutPath = [IO.Path]::ChangeExtension($RecordPath, '.stdout.log')
$stderrPath = [IO.Path]::ChangeExtension($RecordPath, '.stderr.log')
$stdout = [IO.File]::Create($stdoutPath)
$stderr = [IO.File]::Create($stderrPath)
try {
    if (!$process.Start()) {
        throw 'Candidate process did not start.'
    }
    $copyOut = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
    $copyError = $process.StandardError.BaseStream.CopyToAsync($stderr)
    [ordered]@{
        WrapperPid = $PID
        DotnetPid = $process.Id
        DotnetStartedUtc = $process.StartTime.ToUniversalTime().ToString('O')
        StartedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        Executable = $info.FileName
        Arguments = $arguments
        WorkingDirectory = $repositoryRoot
        EnvironmentEntryCount = $info.Environment.Count
        EnvironmentSha256 = $environmentHash
        LaunchSettingsSha256 = $launchProfile.LaunchSettingsSha256
        ArtifactsPath = $ArtifactsPath
        ConsolePolicy = 'Child inherits the owned hidden wrapper console, permitting bounded exclusive-console shutdown.'
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $RecordPath -Encoding utf8NoBOM
    $process.WaitForExit()
    $copyOut.GetAwaiter().GetResult()
    $copyError.GetAwaiter().GetResult()
    [ordered]@{ ExitCode = $process.ExitCode; ExitedUtc = [DateTimeOffset]::UtcNow.ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath ([IO.Path]::ChangeExtension($RecordPath, '.exit.json')) -Encoding utf8NoBOM
    exit $process.ExitCode
} finally {
    $stdout.Dispose()
    $stderr.Dispose()
    $process.Dispose()
}
