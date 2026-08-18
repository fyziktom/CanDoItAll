function ConvertTo-NativeProcessArgument {
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$Value
    )

    if ($null -eq $Value -or $Value.Length -eq 0) {
        return '""'
    }

    $charactersRequiringQuotes = [char[]]@(' ', "`t", "`r", "`n", '"')
    if ($Value.IndexOfAny($charactersRequiringQuotes) -lt 0) {
        return $Value
    }

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append([char]'"')
    $pendingBackslashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq [char]'\') {
            $pendingBackslashes++
            continue
        }

        if ($character -eq [char]'"') {
            for ($index = 0; $index -lt (($pendingBackslashes * 2) + 1); $index++) {
                [void]$builder.Append([char]'\')
            }

            [void]$builder.Append([char]'"')
            $pendingBackslashes = 0
            continue
        }

        for ($index = 0; $index -lt $pendingBackslashes; $index++) {
            [void]$builder.Append([char]'\')
        }

        $pendingBackslashes = 0
        [void]$builder.Append($character)
    }

    for ($index = 0; $index -lt ($pendingBackslashes * 2); $index++) {
        [void]$builder.Append([char]'\')
    }

    [void]$builder.Append([char]'"')
    return $builder.ToString()
}

function Get-ProcessOutputTail {
    param(
        [AllowNull()]
        [string]$StandardOutput,

        [AllowNull()]
        [string]$StandardError,

        [int]$MaximumCharacters = 4000
    )

    $output = @($StandardOutput, $StandardError) `
        | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $text = ($output -join [Environment]::NewLine).Trim()
    if ($text.Length -le $MaximumCharacters) {
        return $text
    }

    return "..." + $text.Substring($text.Length - $MaximumCharacters)
}

function Stop-LaunchedProcessTree {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,

        [Parameter(Mandatory = $true)]
        [DateTime]$ExpectedStartTime,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    if ($Process.HasExited) {
        return
    }

    $liveProcess = Get-Process -Id $Process.Id -ErrorAction SilentlyContinue
    if ($null -eq $liveProcess) {
        return
    }

    if ($liveProcess.StartTime -ne $ExpectedStartTime) {
        throw "Refusing to terminate reused process id $($Process.Id)."
    }

    $taskKillPath = Join-Path $env:SystemRoot "System32\taskkill.exe"
    $taskKillStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $taskKillStartInfo.FileName = $taskKillPath
    $taskKillStartInfo.Arguments = "/PID $($Process.Id) /T /F"
    $taskKillStartInfo.UseShellExecute = $false
    $taskKillStartInfo.CreateNoWindow = $true
    $taskKillStartInfo.RedirectStandardOutput = $true
    $taskKillStartInfo.RedirectStandardError = $true

    $taskKillProcess = New-Object System.Diagnostics.Process
    $taskKillProcess.StartInfo = $taskKillStartInfo
    try {
        if (-not $taskKillProcess.Start()) {
            throw "Could not start taskkill for process $($Process.Id)."
        }

        $taskKillOutputTask = $taskKillProcess.StandardOutput.ReadToEndAsync()
        $taskKillErrorTask = $taskKillProcess.StandardError.ReadToEndAsync()
        if (-not $taskKillProcess.WaitForExit($TimeoutSeconds * 1000)) {
            if (-not $taskKillProcess.HasExited) {
                $taskKillProcess.Kill()
            }
            if (-not $taskKillProcess.WaitForExit($TimeoutSeconds * 1000)) {
                throw "The exact taskkill process could not be terminated within $TimeoutSeconds seconds for process $($Process.Id)."
            }
            throw "taskkill did not complete within $TimeoutSeconds seconds for process $($Process.Id)."
        }

        $taskKillProcess.WaitForExit()
        $taskKillOutput = $taskKillOutputTask.GetAwaiter().GetResult()
        $taskKillError = $taskKillErrorTask.GetAwaiter().GetResult()
        if (-not $Process.WaitForExit($TimeoutSeconds * 1000)) {
            $taskKillTail = Get-ProcessOutputTail `
                -StandardOutput $taskKillOutput `
                -StandardError $taskKillError
            throw "Launched process $($Process.Id) did not exit after exact process-tree termination. taskkill output: $taskKillTail"
        }
    }
    finally {
        $taskKillProcess.Dispose()
    }
}

function Invoke-CheckedDotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [string]$FailureMessage,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds,

        [Parameter(Mandatory = $true)]
        [int]$TerminationTimeoutSeconds,

        [switch]$Quiet
    )

    $dotNetCommand = Get-Command dotnet -CommandType Application -ErrorAction Stop `
        | Select-Object -First 1
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $dotNetCommand.Source
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.Arguments = ($Arguments | ForEach-Object {
            ConvertTo-NativeProcessArgument -Value $_
        }) -join ' '
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "$FailureMessage The dotnet process could not be started."
        }

        $expectedStartTime = $process.StartTime
        $standardOutputTask = $process.StandardOutput.ReadToEndAsync()
        $standardErrorTask = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        $terminationFailure = $null
        if ($timedOut) {
            try {
                Stop-LaunchedProcessTree `
                    -Process $process `
                    -ExpectedStartTime $expectedStartTime `
                    -TimeoutSeconds $TerminationTimeoutSeconds
            }
            catch {
                $terminationFailure = $_.Exception.Message
            }
        }

        if (-not $process.HasExited) {
            throw "$FailureMessage Timed out after $TimeoutSeconds seconds, and exact process-tree termination failed: $terminationFailure"
        }

        $process.WaitForExit()
        $standardOutput = $standardOutputTask.GetAwaiter().GetResult()
        $standardError = $standardErrorTask.GetAwaiter().GetResult()
        $commandOutput = @($standardOutput, $standardError) `
            | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        if (-not $Quiet) {
            foreach ($line in $commandOutput) {
                Write-Host $line
            }
        }

        $outputTail = Get-ProcessOutputTail `
            -StandardOutput $standardOutput `
            -StandardError $standardError
        if ($timedOut) {
            $terminationDetail = if ([string]::IsNullOrWhiteSpace($terminationFailure)) {
                "The exact launched process tree was terminated."
            }
            else {
                "Process-tree termination reported: $terminationFailure"
            }
            throw "$FailureMessage Timed out after $TimeoutSeconds seconds. $terminationDetail Output tail: $outputTail"
        }

        if ($process.ExitCode -ne 0) {
            throw "$FailureMessage Exit code: $($process.ExitCode). Output tail: $outputTail"
        }

        return $commandOutput
    }
    finally {
        $process.Dispose()
    }
}
