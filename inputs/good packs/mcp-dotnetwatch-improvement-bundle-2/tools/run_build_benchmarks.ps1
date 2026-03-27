param(
    [string]$ProjectPath = "C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
    [string]$OutputRoot = "C:\repositories\CanDoItAll\mcp-dotnetwatch-improvement-bundle-2\artifacts\build-bench"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

function Invoke-BuildBenchmark {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [hashtable]$Environment = @{}
    )

    $binlogPath = Join-Path $OutputRoot ($Name + ".binlog")
    $logPath = Join-Path $OutputRoot ($Name + ".log")

    if (Test-Path $binlogPath) {
        Remove-Item $binlogPath -Force
    }

    if (Test-Path $logPath) {
        Remove-Item $logPath -Force
    }

    function Quote-Argument([string]$Value) {
        if ($Value -notmatch '[\s"]') {
            return $Value
        }

        return '"' + ($Value -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
    }

    $processInfo = New-Object System.Diagnostics.ProcessStartInfo
    $processInfo.FileName = "dotnet"
    $processInfo.WorkingDirectory = "C:\repositories\CanDoItAll"
    $processInfo.UseShellExecute = $false
    $processInfo.RedirectStandardOutput = $true
    $processInfo.RedirectStandardError = $true
    $processInfo.Arguments = ($Arguments | ForEach-Object { Quote-Argument $_ }) -join " "

    foreach ($pair in $Environment.GetEnumerator()) {
        $processInfo.Environment[$pair.Key] = $pair.Value
    }

    $process = [System.Diagnostics.Process]::Start($processInfo)
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $start = Get-Date
    $process.WaitForExit()
    $elapsedMs = [math]::Round(((Get-Date) - $start).TotalMilliseconds)

    $combinedLog = $stdoutTask.Result + [Environment]::NewLine + $stderrTask.Result
    Set-Content -Path $logPath -Value $combinedLog

    return [pscustomobject]@{
        Name = $Name
        ExitCode = $process.ExitCode
        ElapsedMs = $elapsedMs
        BinlogPath = $binlogPath
        LogPath = $logPath
        LogBytes = (Get-Item $logPath).Length
        LogLines = (Get-Content $logPath | Measure-Object -Line).Lines
        Arguments = $Arguments
        Environment = $Environment
    }
}

$results = @()

$results += Invoke-BuildBenchmark -Name "normal-warm" -Arguments @(
    "build",
    $ProjectPath,
    "--configuration",
    "Debug",
    "-bl:$((Join-Path $OutputRoot 'normal-warm.binlog'))"
)

$results += Invoke-BuildBenchmark -Name "managed-exact" -Environment @{
    DOTNET_CLI_UI_LANGUAGE = "en"
    DOTNET_NOLOGO = "1"
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    DOTNET_CLI_USE_MSBUILD_SERVER = "0"
} -Arguments @(
    "build",
    $ProjectPath,
    "--configuration",
    "Debug",
    "--artifacts-path",
    (Join-Path $OutputRoot "managed-exact-artifacts"),
    "-p:CanDoItAllMcpOwnerKind=operation",
    "-p:CanDoItAllMcpOwnerId=bench",
    "-p:CanDoItAllMcpWorkspaceRoot=C:\repositories\CanDoItAll",
    "-p:CanDoItAllMcpServerInstanceId=bench",
    "-bl:$((Join-Path $OutputRoot 'managed-exact.binlog'))"
)

$results += Invoke-BuildBenchmark -Name "improved-candidate" -Environment @{
    DOTNET_CLI_UI_LANGUAGE = "en"
    DOTNET_NOLOGO = "1"
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    DOTNET_CLI_USE_MSBUILD_SERVER = "1"
} -Arguments @(
    "build",
    $ProjectPath,
    "--configuration",
    "Debug",
    "-m",
    "--no-restore",
    "-bl:$((Join-Path $OutputRoot 'improved-candidate.binlog'))"
)

$summaryPath = Join-Path $OutputRoot "summary.json"
$results | ConvertTo-Json -Depth 6 | Set-Content -Path $summaryPath
$results | ConvertTo-Json -Depth 6
