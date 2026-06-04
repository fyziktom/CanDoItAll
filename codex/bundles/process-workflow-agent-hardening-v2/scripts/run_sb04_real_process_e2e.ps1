[CmdletBinding()]
param(
    [int]$WebPort = 5048,
    [int]$FirstScenarioPort = 5301,
    [int]$ScenarioLimit = 5,
    [string[]]$ScenarioKeys = @(),
    [int]$RunTimeoutMinutes = 45,
    [switch]$KeepHosts,
    [switch]$SkipBrowser
)

$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "SB04 real process E2E harness requires PowerShell 7 or newer. Run it with pwsh."
}

$BundleRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RepoRoot = (Resolve-Path (Join-Path $BundleRoot "..\..\..")).Path
$ProofRoot = Join-Path $BundleRoot "proof\SB04"
$ScenarioProofRoot = Join-Path $ProofRoot "scenarios"
$TranscriptRoot = Join-Path $ProofRoot "command-transcripts"
$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ArtifactRoot = Join-Path $RepoRoot ".artifacts\sb04-real-process-e2e\$RunStamp"
$WorkspaceRoot = Join-Path $ArtifactRoot "workspace"
$ManagedRoot = Join-Path $WorkspaceRoot "managed-files"
$ControlPlaneRoot = Join-Path $ArtifactRoot "control-plane"
$ManagerArtifactsRoot = Join-Path $ArtifactRoot "manager-artifacts"
$HostLogRoot = Join-Path $ArtifactRoot "host-logs"
$BrowserRoot = Join-Path $ArtifactRoot "browser"
$DatabaseName = "cditall_sb04_$($RunStamp -replace '-', '')"
$ConnectionString = "Host=127.0.0.1;Port=5432;Database=$DatabaseName;Username=candoitall;Password=candoitall;Include Error Detail=true;Timeout=5;Command Timeout=15"
$BaseUrl = "http://127.0.0.1:$WebPort"
$JsonDepth = 100

$ProjectStatusActive = 1
$ObjectTypeFile = 10
$OperatingModeGovernedLive = 3
$TerminalRunStatuses = @(2, 3, 4, 5, "Blocked", "Completed", "Cancelled", "Failed")

$ScenarioOrder = @(
    "tetris-mini-game",
    "expense-tracker-lite",
    "plant-watering-planner",
    "study-kanban-flashcards",
    "recipe-pantry-planner"
)

function New-Directory {
    param([string]$Path)
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Write-Utf8File {
    param(
        [string]$Path,
        [string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (![string]::IsNullOrWhiteSpace($directory)) {
        New-Directory $directory
    }

    Set-Content -Path $Path -Value $Content -Encoding UTF8
}

function ConvertTo-BodyJson {
    param([object]$Value)
    return $Value | ConvertTo-Json -Depth $JsonDepth
}

function ConvertTo-ProcessArgument {
    param([string]$Argument)

    if ($null -eq $Argument) {
        return '""'
    }

    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') {
        return $Argument
    }

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.Append('"')
    $backslashCount = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq [char]'\') {
            $backslashCount++
            continue
        }

        if ($character -eq [char]'"') {
            if ($backslashCount -gt 0) {
                [void]$builder.Append(('\' * ($backslashCount * 2)))
                $backslashCount = 0
            }

            [void]$builder.Append('\"')
            continue
        }

        if ($backslashCount -gt 0) {
            [void]$builder.Append(('\' * $backslashCount))
            $backslashCount = 0
        }

        [void]$builder.Append($character)
    }

    if ($backslashCount -gt 0) {
        [void]$builder.Append(('\' * ($backslashCount * 2)))
    }

    [void]$builder.Append('"')
    return $builder.ToString()
}

function ConvertTo-ProcessArgumentString {
    param([string[]]$Arguments)

    return (($Arguments | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join " ")
}

function Invoke-External {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$TranscriptPath,
        [switch]$AllowFailure
    )

    New-Directory (Split-Path -Parent $TranscriptPath)
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FileName
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    if ($null -ne $psi.ArgumentList) {
        foreach ($argument in $Arguments) {
            $psi.ArgumentList.Add($argument) | Out-Null
        }
    }
    else {
        $psi.Arguments = ConvertTo-ProcessArgumentString $Arguments
    }

    $process = [System.Diagnostics.Process]::Start($psi)
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    Write-Utf8File $TranscriptPath (@(
        "Command: $FileName $($Arguments -join ' ')",
        "WorkingDirectory: $WorkingDirectory",
        "ExitCode: $($process.ExitCode)",
        "",
        "STDOUT:",
        $stdout,
        "",
        "STDERR:",
        $stderr
    ) -join [Environment]::NewLine)

    if ($process.ExitCode -ne 0 -and !$AllowFailure) {
        throw "Command failed with exit code $($process.ExitCode): $FileName $($Arguments -join ' '). Transcript: $TranscriptPath"
    }

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        Stdout = $stdout
        Stderr = $stderr
        TranscriptPath = $TranscriptPath
    }
}

function Start-CapturedProcess {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [hashtable]$Environment,
        [string]$StdoutPath,
        [string]$StderrPath
    )

    New-Directory (Split-Path -Parent $StdoutPath)
    New-Directory (Split-Path -Parent $StderrPath)
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FileName
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.RedirectStandardOutput = $false
    $psi.RedirectStandardError = $false
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    if ($null -ne $psi.ArgumentList) {
        foreach ($argument in $Arguments) {
            $psi.ArgumentList.Add($argument) | Out-Null
        }
    }
    else {
        $psi.Arguments = ConvertTo-ProcessArgumentString $Arguments
    }

    foreach ($pair in $Environment.GetEnumerator()) {
        $psi.Environment[$pair.Key] = [string]$pair.Value
    }

    $process = [System.Diagnostics.Process]::Start($psi)
    Write-Utf8File $StdoutPath (@(
        "Command: $FileName $($Arguments -join ' ')",
        "WorkingDirectory: $WorkingDirectory",
        "ProcessId: $($process.Id)",
        "StartedAtUtc: $((Get-Date).ToUniversalTime().ToString("o"))",
        "Output redirection disabled for long-running host stability."
    ) -join [Environment]::NewLine)
    Write-Utf8File $StderrPath ""

    return [pscustomobject]@{
        Process = $process
        StdoutPath = $StdoutPath
        StderrPath = $StderrPath
    }
}

function Stop-CapturedProcess {
    param([object]$Handle)

    if ($null -eq $Handle) {
        return
    }

    if ($Handle.Process -and !$Handle.Process.HasExited) {
        try {
            $Handle.Process.Kill($true)
        }
        catch [System.Management.Automation.MethodException] {
            & taskkill.exe /PID $Handle.Process.Id /T /F | Out-Null
            if (!$Handle.Process.HasExited) {
                $Handle.Process.Kill()
            }
        }

        $Handle.Process.WaitForExit(10000) | Out-Null
    }
}

function Wait-HttpReady {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 180
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            return Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 5
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "Timed out waiting for $Url."
}

function Invoke-JsonApi {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [int]$TimeoutSeconds = 60
    )

    $uri = if ($Path.StartsWith("http", [StringComparison]::OrdinalIgnoreCase)) { $Path } else { "$BaseUrl$Path" }
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -TimeoutSec $TimeoutSeconds
    }

    return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body (ConvertTo-BodyJson $Body) -TimeoutSec $TimeoutSeconds
}

function Get-AgentHeaders {
    return @{
        "X-CanDoItAll-Agent-Id" = "codex-sb04-real-process"
        "X-CanDoItAll-Agent-Name" = "Codex SB04 real process harness"
        "X-CanDoItAll-Agent-Machine" = [Environment]::MachineName
        "X-CanDoItAll-Agent-RepoRoot" = $RepoRoot
        "X-CanDoItAll-Agent-Branch" = (git -C $RepoRoot branch --show-current 2>$null)
        "X-CanDoItAll-Agent-Session" = "sb04-$RunStamp"
    }
}

function Invoke-ProjectStructureApi {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [int]$TimeoutSeconds = 60
    )

    $uri = "$BaseUrl$Path"
    $headers = Get-AgentHeaders
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -TimeoutSec $TimeoutSeconds
    }

    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body (ConvertTo-BodyJson $Body) -TimeoutSec $TimeoutSeconds
}

function New-MediaPayload {
    param(
        [string]$Path,
        [string]$FileName,
        [string]$ContentType
    )

    return @{
        fileName = $FileName
        contentType = $ContentType
        base64Data = [Convert]::ToBase64String([IO.File]::ReadAllBytes($Path))
    }
}

function Initialize-ProofRoots {
    New-Directory $ProofRoot
    New-Directory $ScenarioProofRoot
    New-Directory $TranscriptRoot
    New-Directory $ArtifactRoot
    New-Directory $WorkspaceRoot
    New-Directory $ManagedRoot
    New-Directory $ControlPlaneRoot
    New-Directory $ManagerArtifactsRoot
    New-Directory $HostLogRoot
    New-Directory $BrowserRoot
}

function Initialize-Postgres {
    Invoke-External -FileName "docker" -Arguments @("compose", "up", "-d", "postgres") -WorkingDirectory $RepoRoot -TranscriptPath (Join-Path $TranscriptRoot "docker-compose-postgres.txt") | Out-Null
    Invoke-External -FileName "docker" -Arguments @("exec", "candoitall-postgres", "psql", "-U", "candoitall", "-d", "postgres", "-c", "drop database if exists `"$DatabaseName`" with (force);") -WorkingDirectory $RepoRoot -TranscriptPath (Join-Path $TranscriptRoot "postgres-drop-database.txt") | Out-Null
    Invoke-External -FileName "docker" -Arguments @("exec", "candoitall-postgres", "psql", "-U", "candoitall", "-d", "postgres", "-c", "create database `"$DatabaseName`";") -WorkingDirectory $RepoRoot -TranscriptPath (Join-Path $TranscriptRoot "postgres-create-database.txt") | Out-Null
}

function Initialize-BrowserValidator {
    Invoke-External `
        -FileName "dotnet" `
        -Arguments @(
            "test",
            "tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj",
            "--filter",
            "FullyQualifiedName~Sb04GeneratedAppBrowserValidationTests.Generated_app_supports_desktop_and_mobile_browser_validation",
            "--no-restore"
        ) `
        -WorkingDirectory $RepoRoot `
        -TranscriptPath (Join-Path $TranscriptRoot "browser-validator-prebuild.txt") | Out-Null
}

function Start-WebHost {
    $environment = @{
        "ASPNETCORE_ENVIRONMENT" = "Development"
        "CanDoItAllMcpLaneKind" = "SourceWatch"
        "LaneKind" = "SourceWatch"
        "Database__Provider" = "PostgreSql"
        "Database__ConnectionString" = $ConnectionString
        "CANDOITALL_DATABASE_PROVIDER" = "PostgreSql"
        "CANDOITALL_DATABASE_CONNECTION" = $ConnectionString
        "Storage__WorkspaceRoot" = $WorkspaceRoot
        "Storage__ManagedFilesFolder" = "managed-files"
        "Storage__ExportsFolder" = "exports"
        "Storage__EvidenceFolder" = "evidence"
        "Storage__ManagerArtifactsFolder" = $ManagerArtifactsRoot
        "ControlPlane__RootPath" = $ControlPlaneRoot
        "Processes__Runtime__RequirePostgreSqlForAgentAutomation" = "true"
        "Workflows__ExampleSeed__Enabled" = "true"
        "Workflows__ExampleSeed__SeedSampleWorkspaceFiles" = "true"
    }

    $handle = Start-CapturedProcess `
        -FileName "dotnet" `
        -Arguments @("run", "--no-launch-profile", "--project", "src\CanDoItAll.Web\CanDoItAll.Web.csproj", "--urls", $BaseUrl) `
        -WorkingDirectory $RepoRoot `
        -Environment $environment `
        -StdoutPath (Join-Path $HostLogRoot "web-host.out.log") `
        -StderrPath (Join-Path $HostLogRoot "web-host.err.log")

    $handle.Process.Id | Set-Content -Path (Join-Path $HostLogRoot "web-host.pid")
    Wait-HttpReady -Url "$BaseUrl/api/access/status" -TimeoutSeconds 300 | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $TranscriptRoot "access-status.json") -Encoding UTF8
    return $handle
}

function Import-And-PublishProcess {
    $definitionName = "SB04 Real Agent Blazor App Delivery $RunStamp"
    $definitionId = Invoke-JsonApi -Method "Post" -Path "/api/processes/templates/blazor-app-delivery/import" -Body @{
        definitionName = $definitionName
    } -TimeoutSeconds 120

    Invoke-JsonApi -Method "Post" -Path "/api/processes/definitions/$definitionId/publish" -TimeoutSeconds 120 | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $TranscriptRoot "process-template-publish.json") -Encoding UTF8
    return [string]$definitionId
}

function Get-ScenarioTitle {
    param([string]$ScenarioKey)

    return ($ScenarioKey -replace "[-_]", " ").Split(" ") | ForEach-Object {
        if ($_.Length -eq 0) {
            ""
        }
        else {
            $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1)
        }
    } | Join-String -Separator " "
}

function Load-Scenario {
    param([string]$ScenarioKey)

    $path = Join-Path $BundleRoot "templates\process-test-scenarios\$ScenarioKey.md"
    $requestMarkdown = Get-Content -Path $path -Raw
    return [pscustomobject]@{
        ScenarioKey = $ScenarioKey
        Title = Get-ScenarioTitle $ScenarioKey
        RequestMarkdown = $requestMarkdown
        SourcePath = $path
    }
}

function New-ProjectAndScenarioAsset {
    param(
        [object]$Scenario,
        [string]$ScenarioDir
    )

    $requestPath = Join-Path $ScenarioDir "request.md"
    $expectedSourceRootName = "GeneratedBlazorApp"
    $requestMarkdown = @(
        $Scenario.RequestMarkdown.Trim(),
        "",
        "## SB04 Production-Path Harness Constraints",
        "",
        "- Create generated app source in the current-run generated app output root named ``$expectedSourceRootName``.",
        "- Treat that root as the product root. The runnable host project must be directly under that root as ``$expectedSourceRootName/$expectedSourceRootName.csproj`` or another single direct non-test ``.csproj``. Do not create sibling app roots or sibling test projects beside it.",
        "- Put generated tests under ``$expectedSourceRootName/tests``. Do not create ``*.Tests`` beside ``$expectedSourceRootName``.",
        "- Do not create probe, template-test, scratch, copied scaffold, or scenario-named project directories anywhere under this process run. Scaffold directly into ``$expectedSourceRootName`` and repair that root in place.",
        "- Do not set ``BaseOutputPath``, ``BaseIntermediateOutputPath``, or ``MSBuildProjectExtensionsPath`` in the generated host project. Use normal ``bin``/``obj`` output so generated build artifacts are not compiled as source on reruns.",
        "- Do not ask the proof harness to write app source code.",
        "- Record current-run artifacts that identify the generated source root, build command, runtime command or URL, browser proof expectation, and cleanup notes.",
        "- Keep the app client-only and avoid backend/database/authentication integration unless the scenario packet asks for it.",
        "- Before finalizing, inspect the app entry document and ensure every local stylesheet, script, manifest, icon, service-worker, and generated-style reference resolves at the exact served path. Do not leave browser console 404s for missing local static assets."
    ) -join [Environment]::NewLine
    Write-Utf8File $requestPath $requestMarkdown

    $project = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure/projects" -Body @{
        name = "SB04 $($Scenario.Title) $RunStamp"
        description = "SB04 real process app-generation scenario."
        objective = $requestMarkdown
        currentPhase = "SB04 real agent-driven process E2E"
        status = $ProjectStatusActive
    } -TimeoutSeconds 120

    $asset = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure/projects/$($project.id)/assets" -Body @{
        objectType = $ObjectTypeFile
        title = "$($Scenario.Title) request packet"
        subtitle = "SB04 scenario input"
        notes = "Current-run scenario request uploaded before process start."
        media = New-MediaPayload -Path $requestPath -FileName "request.md" -ContentType "text/markdown"
        parentNodeKey = $null
        objectSubtype = "markdown"
        metadataJson = (@{ scenarioKey = $Scenario.ScenarioKey; runStamp = $RunStamp; expectedSourceRootName = $expectedSourceRootName } | ConvertTo-Json -Depth 8)
        sourceWorkspacePath = $null
        sourceFileName = $null
        sourceContentType = $null
    } -TimeoutSeconds 120

    return [pscustomobject]@{
        Project = $project
        Asset = $asset
        RequestPath = $requestPath
        ExpectedSourceRootName = $expectedSourceRootName
    }
}

function Start-ProcessRunForScenario {
    param(
        [object]$Scenario,
        [string]$DefinitionId,
        [object]$ProjectContext
    )

    $triggerReason = @(
        "Deliver a Blazor WebAssembly PWA for $($Scenario.Title) from the uploaded SB04 request packet.",
        "Generated source target: current-run output root named $($ProjectContext.ExpectedSourceRootName).",
        "The process automation dispatcher and assigned technical agents must perform implementation, validation, artifact recording, and finalizer submission. The proof harness will not write source code or manually complete steps.",
        "",
        $Scenario.RequestMarkdown.Trim()
    ) -join [Environment]::NewLine

    $runId = Invoke-JsonApi -Method "Post" -Path "/api/processes/runs/start" -Body @{
        processDefinitionId = $DefinitionId
        projectId = $ProjectContext.Project.id
        runName = "SB04 real Blazor PWA delivery / $($Scenario.Title) / $RunStamp"
        operatingMode = $OperatingModeGovernedLive
        triggerReason = $triggerReason
        projectStructureContext = @{
            projectId = $ProjectContext.Project.id
            nodeId = $ProjectContext.Asset.id
            nodeTitle = $ProjectContext.Asset.title
            parentNodeId = $null
            parentNodeTitle = ""
        }
        lintMode = 0
    } -TimeoutSeconds 120

    return [string]$runId
}

function Get-RunDetail {
    param([string]$RunId)

    return Invoke-JsonApi -Method "Get" -Path "/api/processes/runs/$RunId`?includeArtifacts=true&includeAssignments=true&includeWorkBriefs=true&includeExecutionRuns=true&includeOutboxRecords=true" -TimeoutSeconds 120
}

function Get-ExecutionRunDetails {
    param([string]$RunId)

    $runs = Invoke-JsonApi -Method "Get" -Path "/api/agents/execution-runs?processRunId=$RunId&take=500" -TimeoutSeconds 120
    $details = @()
    foreach ($run in @($runs)) {
        try {
            $details += Invoke-JsonApi -Method "Get" -Path "/api/agents/execution-runs/$($run.id)" -TimeoutSeconds 120
        }
        catch {
            $details += [pscustomobject]@{
                run = $run
                detailUnavailable = $true
                detailUnavailableReason = $_.Exception.Message
                toolReceipts = @()
                usageObservations = @()
                artifacts = @()
            }
        }
    }

    return @($details)
}

function Confirm-PendingApprovals {
    param([object[]]$ExecutionDetails)

    foreach ($detail in $ExecutionDetails) {
        if ($detail.run.pendingApprovals -and @($detail.run.pendingApprovals).Count -gt 0) {
            Invoke-JsonApi -Method "Post" -Path "/api/agents/execution-runs/$($detail.run.id)/pending-approvals" -Body @{
                approved = $true
                autoApprovePendingToolCalls = $true
            } -TimeoutSeconds 120 | Out-Null
        }
    }
}

function Wait-RunTerminal {
    param(
        [string]$RunId,
        [string]$ScenarioDir
    )

    $deadline = (Get-Date).AddMinutes($RunTimeoutMinutes)
    $polls = @()
    while ((Get-Date) -lt $deadline) {
        $detail = Get-RunDetail -RunId $RunId
        $executionDetails = Get-ExecutionRunDetails -RunId $RunId
        Confirm-PendingApprovals -ExecutionDetails $executionDetails
        $polls += [pscustomobject]@{
            observedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
            runStatus = $detail.run.status
            completedStepCount = $detail.run.completedStepCount
            totalStepCount = $detail.run.totalStepCount
            executionRunCount = @($executionDetails).Count
            activeExecutionRunCount = @($executionDetails | Where-Object { $_.run.state -notin @(2, 3, "Completed", "Failed") }).Count
        }

        if ($TerminalRunStatuses -contains $detail.run.status) {
            $polls | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $ScenarioDir "poll-log.json") -Encoding UTF8
            return [pscustomobject]@{
                Detail = $detail
                ExecutionDetails = $executionDetails
            }
        }

        Start-Sleep -Seconds 15
    }

    $polls | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $ScenarioDir "poll-log.json") -Encoding UTF8
    throw "Timed out waiting for process run $RunId to reach a terminal state after $RunTimeoutMinutes minute(s)."
}

function Resolve-GeneratedSourceRoot {
    param(
        [object[]]$ExecutionDetails,
        [string]$RunId,
        [string]$ExpectedSourceRootName
    )

    $normalizedRunId = $RunId.ToLowerInvariant()
    $normalizedRunPath = "process-runs/$normalizedRunId"
    $normalizedSourceRootName = $ExpectedSourceRootName.Trim()
    $candidateRoots = New-Object System.Collections.Generic.List[string]

    function Test-PathHasCurrentRunLineage {
        param([string]$RelativePath)

        $normalizedRelativePath = $RelativePath.Replace("\", "/").Trim("/").ToLowerInvariant()
        return $normalizedRelativePath.Contains($normalizedRunPath, [StringComparison]::Ordinal)
    }

    function Add-CurrentRunRootCandidate {
        param([string]$Path)

        if ([string]::IsNullOrWhiteSpace($Path)) {
            return
        }

        $candidateFullPath = try {
            [System.IO.Path]::GetFullPath(
                $(if ([System.IO.Path]::IsPathRooted($Path)) {
                    $Path
                }
                else {
                    Join-Path $WorkspaceRoot ($Path -replace "/", [System.IO.Path]::DirectorySeparatorChar)
                }))
        }
        catch {
            return
        }

        $workspaceFullPath = [System.IO.Path]::GetFullPath($WorkspaceRoot)
        if (!($candidateFullPath.StartsWith(
            $workspaceFullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($candidateFullPath, $workspaceFullPath, [StringComparison]::OrdinalIgnoreCase))) {
            return
        }

        $relativePath = [System.IO.Path]::GetRelativePath($workspaceFullPath, $candidateFullPath).Replace("\", "/").Trim("/")
        if (!(Test-PathHasCurrentRunLineage -RelativePath $relativePath)) {
            return
        }

        $segments = @($relativePath -split "/" | Where-Object { ![string]::IsNullOrWhiteSpace($_) })
        for ($index = 0; $index -lt $segments.Count; $index++) {
            if (![string]::Equals($segments[$index], $normalizedSourceRootName, [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $declaredRelativeRoot = ($segments[0..$index] -join "/")
            $candidateRoots.Add((Join-Path $workspaceFullPath ($declaredRelativeRoot -replace "/", [System.IO.Path]::DirectorySeparatorChar)))
            return
        }
    }

    function Resolve-ProjectInRoot {
        param(
            [string]$RootPath,
            [string]$Resolution
        )

        if ([string]::IsNullOrWhiteSpace($RootPath)) {
            return $null
        }

        $fullRootPath = try { [System.IO.Path]::GetFullPath($RootPath) } catch { return $null }
        if (!(Test-Path -LiteralPath $fullRootPath -PathType Container)) {
            return $null
        }

        $projectFile = Get-ChildItem -LiteralPath $fullRootPath -Filter *.csproj -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch "[/\\]tests[/\\]" -and $_.BaseName -notmatch "\.Tests$" } |
            Sort-Object FullName |
            Select-Object -First 1
        if (!$projectFile) {
            return $null
        }

        return [pscustomobject]@{
            Root = $fullRootPath
            ProjectFile = $projectFile.FullName
            Resolution = $Resolution
            ExpectedSourceRootName = $ExpectedSourceRootName
            ProcessRunId = $RunId
            CurrentRunLineage = $true
        }
    }

    foreach ($detail in $ExecutionDetails) {
        foreach ($receipt in @($detail.toolReceipts)) {
            if (![string]::IsNullOrWhiteSpace($receipt.workingDirectory)) {
                Add-CurrentRunRootCandidate -Path ([string]$receipt.workingDirectory)
            }

            foreach ($text in @($receipt.requestSummary, $receipt.exitSummary)) {
                if ([string]::IsNullOrWhiteSpace($text)) {
                    continue
                }

                $runPathPattern = '[^\s`"'',;)]*process-runs[/\\]' + [regex]::Escape($RunId) + '[/\\][^\s`"'',;)]*'
                $matches = [regex]::Matches($text, $runPathPattern)
                foreach ($match in $matches) {
                    Add-CurrentRunRootCandidate -Path $match.Value
                }
            }
        }

        foreach ($artifact in @($detail.artifacts)) {
            if (![string]::IsNullOrWhiteSpace($artifact.relativePath)) {
                Add-CurrentRunRootCandidate -Path ([string]$artifact.relativePath)
            }
        }
    }

    foreach ($path in $candidateRoots | Select-Object -Unique) {
        $resolved = Resolve-ProjectInRoot -RootPath $path -Resolution "current-run-root-from-receipt"
        if ($null -ne $resolved) {
            return $resolved
        }
    }

    $workspaceRootFullPath = [System.IO.Path]::GetFullPath($WorkspaceRoot)
    foreach ($projectFile in Get-ChildItem -LiteralPath $WorkspaceRoot -Filter *.csproj -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName) {
        $relativeProjectDirectory = [System.IO.Path]::GetRelativePath($workspaceRootFullPath, $projectFile.Directory.FullName).Replace("\", "/").Trim("/")
        if ((Test-PathHasCurrentRunLineage -RelativePath $relativeProjectDirectory) -and
            $relativeProjectDirectory.Split("/")[-1].Equals($normalizedSourceRootName, [StringComparison]::OrdinalIgnoreCase) -and
            $projectFile.FullName -notmatch "[/\\]tests[/\\]" -and
            $projectFile.BaseName -notmatch "\.Tests$") {
            return [pscustomobject]@{
                Root = $projectFile.Directory.FullName
                ProjectFile = $projectFile.FullName
                Resolution = "workspace-scan-current-run-root"
                ExpectedSourceRootName = $ExpectedSourceRootName
                ProcessRunId = $RunId
                CurrentRunLineage = $true
            }
        }
    }

    return $null
}

function Get-RelativeWorkspacePath {
    param([string]$Path)

    $workspaceRootFullPath = [System.IO.Path]::GetFullPath($WorkspaceRoot)
    return [System.IO.Path]::GetRelativePath($workspaceRootFullPath, [System.IO.Path]::GetFullPath($Path)).Replace("\", "/").Trim("/")
}

function Get-ProcessRunRootFromSourceRoot {
    param(
        [string]$SourceRootPath,
        [string]$RunId
    )

    $workspaceRootFullPath = [System.IO.Path]::GetFullPath($WorkspaceRoot)
    $relativePath = Get-RelativeWorkspacePath -Path $SourceRootPath
    $segments = @($relativePath -split "/" | Where-Object { ![string]::IsNullOrWhiteSpace($_) })
    for ($index = 0; $index -lt $segments.Count - 1; $index++) {
        if (![string]::Equals($segments[$index], "process-runs", [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        if (![string]::Equals($segments[$index + 1], $RunId, [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $processRunRelativePath = ($segments[0..($index + 1)] -join "/")
        return Join-Path $workspaceRootFullPath ($processRunRelativePath -replace "/", [System.IO.Path]::DirectorySeparatorChar)
    }

    throw "Could not resolve process-run root from generated source root '$SourceRootPath' for run '$RunId'."
}

function Assert-GeneratedSourceRootLayout {
    param(
        [object]$Scenario,
        [object]$SourceRoot,
        [string]$ScenarioDir,
        [string]$ExpectedSourceRootName
    )

    $sourceRootFullPath = [System.IO.Path]::GetFullPath([string]$SourceRoot.Root)
    $projectFullPath = [System.IO.Path]::GetFullPath([string]$SourceRoot.ProjectFile)
    $projectRelativePath = [System.IO.Path]::GetRelativePath($sourceRootFullPath, $projectFullPath).Replace("\", "/")
    $processRunRoot = Get-ProcessRunRootFromSourceRoot -SourceRootPath $sourceRootFullPath -RunId ([string]$SourceRoot.ProcessRunId)
    $expectedSourceRootFullPath = Join-Path $processRunRoot $ExpectedSourceRootName
    $sourceRootLayout = [ordered]@{
        scenarioKey = $Scenario.ScenarioKey
        processRunRoot = $processRunRoot
        expectedSourceRoot = $expectedSourceRootFullPath
        sourceRoot = $sourceRootFullPath
        projectFile = $projectFullPath
        projectRelativePath = $projectRelativePath
        misplacedProjectFiles = @()
        misplacedTestProjectFiles = @()
        disallowedSourceRootDirectories = @()
        disallowedProjectProperties = @()
    }

    try {
        if (![string]::Equals($sourceRootFullPath, [System.IO.Path]::GetFullPath($expectedSourceRootFullPath), [StringComparison]::OrdinalIgnoreCase)) {
            throw "SB04 scenario '$($Scenario.ScenarioKey)' resolved generated source root '$sourceRootFullPath', but the required root is '$expectedSourceRootFullPath'."
        }

        if ($projectRelativePath.Contains("/", [StringComparison]::Ordinal) -or $projectRelativePath.Contains("\", [StringComparison]::Ordinal)) {
            throw "SB04 scenario '$($Scenario.ScenarioKey)' runnable host project must be directly under '$ExpectedSourceRootName'. Resolved project '$projectRelativePath'."
        }

        $projectXml = [xml](Get-Content -LiteralPath $projectFullPath -Raw)
        $disallowedProjectProperties = @()
        foreach ($propertyName in @("BaseOutputPath", "BaseIntermediateOutputPath", "MSBuildProjectExtensionsPath")) {
            $values = @($projectXml.Project.PropertyGroup | ForEach-Object { $_.$propertyName } | Where-Object { ![string]::IsNullOrWhiteSpace([string]$_) })
            foreach ($value in $values) {
                $disallowedProjectProperties += [pscustomobject]@{
                    property = $propertyName
                    value = [string]$value
                }
            }
        }

        $sourceRootLayout.disallowedProjectProperties = @($disallowedProjectProperties)
        if (@($disallowedProjectProperties).Count -gt 0) {
            throw "SB04 scenario '$($Scenario.ScenarioKey)' generated host project sets disallowed MSBuild output properties: $((@($disallowedProjectProperties) | ForEach-Object { \"$($_.property)=$($_.value)\" }) -join ', ')."
        }

        $processRunProjects = @(Get-ChildItem -LiteralPath $processRunRoot -Filter *.csproj -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch "[/\\]tests[/\\]" -and $_.BaseName -notmatch "\.Tests$" })

        $misplacedProjectFiles = @($processRunProjects | Where-Object {
            $projectPath = [System.IO.Path]::GetFullPath($_.FullName)
            if ([string]::Equals($projectPath, $projectFullPath, [StringComparison]::OrdinalIgnoreCase)) {
                return $false
            }

            $projectRelativeToSourceRoot = [System.IO.Path]::GetRelativePath($sourceRootFullPath, $projectPath).Replace("\", "/")
            if (!$projectRelativeToSourceRoot.StartsWith("..", [StringComparison]::Ordinal) -and
                !$projectRelativeToSourceRoot.StartsWith("/", [StringComparison]::Ordinal) -and
                !$projectRelativeToSourceRoot.Contains("../", [StringComparison]::Ordinal)) {
                return !$projectRelativeToSourceRoot.StartsWith("src/", [StringComparison]::OrdinalIgnoreCase)
            }

            return $true
        } | ForEach-Object { Get-RelativeWorkspacePath -Path $_.FullName })
        $sourceRootLayout.misplacedProjectFiles = @($misplacedProjectFiles)
        if (@($misplacedProjectFiles).Count -gt 0) {
            throw "SB04 scenario '$($Scenario.ScenarioKey)' created non-test project(s) outside the required host root layout: $($misplacedProjectFiles -join ', ')."
        }

        $testProjectFiles = @(Get-ChildItem -LiteralPath $processRunRoot -Filter *.csproj -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "[/\\]tests[/\\]" -or $_.BaseName -match "\.Tests$" })
        $misplacedTestProjectFiles = @($testProjectFiles | Where-Object {
            $projectPath = [System.IO.Path]::GetFullPath($_.FullName)
            $projectRelativeToSourceRoot = [System.IO.Path]::GetRelativePath($sourceRootFullPath, $projectPath).Replace("\", "/")
            return $projectRelativeToSourceRoot.StartsWith("..", [StringComparison]::Ordinal) -or
                !$projectRelativeToSourceRoot.StartsWith("tests/", [StringComparison]::OrdinalIgnoreCase)
        } | ForEach-Object { Get-RelativeWorkspacePath -Path $_.FullName })
        $sourceRootLayout.misplacedTestProjectFiles = @($misplacedTestProjectFiles)
        if (@($misplacedTestProjectFiles).Count -gt 0) {
            throw "SB04 scenario '$($Scenario.ScenarioKey)' created test project(s) outside '$ExpectedSourceRootName/tests': $($misplacedTestProjectFiles -join ', ')."
        }

        $disallowedDirectories = @(Get-ChildItem -LiteralPath $sourceRootFullPath -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object {
                [string]::Equals($_.Name, "_probe", [StringComparison]::OrdinalIgnoreCase) -or
                [string]::Equals($_.Name, "probe", [StringComparison]::OrdinalIgnoreCase) -or
                $_.Name.Contains("Probe", [StringComparison]::OrdinalIgnoreCase)
            } |
            ForEach-Object { Get-RelativeWorkspacePath -Path $_.FullName })
        $sourceRootLayout.disallowedSourceRootDirectories = @($disallowedDirectories)
        if (@($disallowedDirectories).Count -gt 0) {
            throw "SB04 scenario '$($Scenario.ScenarioKey)' left probe/scaffold directories inside the generated product root: $($disallowedDirectories -join ', ')."
        }
    }
    finally {
        $sourceRootLayout | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $ScenarioDir "generated-source-root-layout.json") -Encoding UTF8
    }
}

function Assert-ScenarioEvidence {
    param(
        [object]$Scenario,
        [string]$ScenarioDir,
        [object]$TerminalState,
        [object]$SourceRoot,
        [string]$ExpectedSourceRootName
    )

    $executionDetails = @($TerminalState.ExecutionDetails)
    $toolReceipts = @($executionDetails | ForEach-Object { @($_.toolReceipts) })
    $usageObservations = @($executionDetails | ForEach-Object { @($_.usageObservations) })
    $knownUsage = @($usageObservations | Where-Object { $_.usageStatus -in @(0, 4, "Observed", "ObservedFromMetric") })

    if (@($executionDetails).Count -eq 0) {
        throw "SB04 scenario '$($Scenario.ScenarioKey)' produced no AgentFramework execution runs."
    }

    if (@($toolReceipts).Count -eq 0) {
        throw "SB04 scenario '$($Scenario.ScenarioKey)' produced no tool receipts."
    }

    if (@($usageObservations).Count -eq 0) {
        throw "SB04 scenario '$($Scenario.ScenarioKey)' produced no provider usage observations."
    }

    if (@($knownUsage).Count -eq 0) {
        throw "SB04 scenario '$($Scenario.ScenarioKey)' produced only unknown provider usage observations."
    }

    if ($null -eq $SourceRoot) {
        throw "SB04 scenario '$($Scenario.ScenarioKey)' did not expose a generated source root named '$ExpectedSourceRootName' with current-run lineage."
    }

    if (!$SourceRoot.CurrentRunLineage) {
        throw "SB04 scenario '$($Scenario.ScenarioKey)' exposed a generated source root without current-run lineage."
    }

    Assert-GeneratedSourceRootLayout -Scenario $Scenario -SourceRoot $SourceRoot -ScenarioDir $ScenarioDir -ExpectedSourceRootName $ExpectedSourceRootName

    $TerminalState.Detail | ConvertTo-Json -Depth $JsonDepth | Set-Content -Path (Join-Path $ScenarioDir "process-run-detail.json") -Encoding UTF8
    $executionDetails | ConvertTo-Json -Depth $JsonDepth | Set-Content -Path (Join-Path $ScenarioDir "agent-execution-runs.json") -Encoding UTF8
    $toolReceipts | ConvertTo-Json -Depth $JsonDepth | Set-Content -Path (Join-Path $ScenarioDir "tool-receipts.json") -Encoding UTF8
    $usageObservations | ConvertTo-Json -Depth $JsonDepth | Set-Content -Path (Join-Path $ScenarioDir "usage-observations.json") -Encoding UTF8
    [pscustomobject]@{
        scenarioKey = $Scenario.ScenarioKey
        canDoItAllProviderUsageObserved = $true
        observationCount = @($usageObservations).Count
        knownObservationCount = @($knownUsage).Count
        totalTokens = ($knownUsage | Measure-Object -Property totalTokens -Sum).Sum
        providerResponseIds = @($knownUsage | ForEach-Object { $_.providerResponseId } | Where-Object { ![string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
        statuses = @($usageObservations | ForEach-Object { $_.usageStatus } | Select-Object -Unique)
    } | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $ScenarioDir "usage-summary.json") -Encoding UTF8
    $SourceRoot | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $ScenarioDir "generated-source-root.json") -Encoding UTF8
}

function Build-GeneratedApp {
    param(
        [object]$SourceRoot,
        [string]$TranscriptPath
    )

    $sourceRootFullPath = [System.IO.Path]::GetFullPath([string]$SourceRoot.Root)
    $sourceRootBoundary = $sourceRootFullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    foreach ($directoryName in @("bin", "obj", "b", "o")) {
        $targetPath = Join-Path $sourceRootFullPath $directoryName
        if (!(Test-Path -LiteralPath $targetPath -PathType Container)) {
            continue
        }

        $targetFullPath = [System.IO.Path]::GetFullPath($targetPath)
        if (!$targetFullPath.StartsWith($sourceRootBoundary, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clear generated build directory outside source root: $targetFullPath"
        }

        Remove-Item -LiteralPath $targetFullPath -Recurse -Force
    }

    $projectFullPath = [System.IO.Path]::GetFullPath([string]$SourceRoot.ProjectFile)
    $projectRelativePath = [System.IO.Path]::GetRelativePath($sourceRootFullPath, $projectFullPath)
    if ($projectRelativePath.StartsWith("..", [StringComparison]::Ordinal) -or [System.IO.Path]::IsPathRooted($projectRelativePath)) {
        throw "Generated project file is outside source root: $projectFullPath"
    }

    $workspaceRootFullPath = [System.IO.Path]::GetFullPath($WorkspaceRoot)
    $workspaceRootBoundary = $workspaceRootFullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (!$projectFullPath.StartsWith($workspaceRootBoundary, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Generated project file is outside SB04 workspace root: $projectFullPath"
    }

    $projectWorkspaceRelativePath = [System.IO.Path]::GetRelativePath($workspaceRootFullPath, $projectFullPath)
    $sourceRootWorkspaceRelativePath = [System.IO.Path]::GetRelativePath($workspaceRootFullPath, $sourceRootFullPath)
    $buildWorkingDirectory = $sourceRootFullPath
    $buildProjectFile = $projectFullPath
    $aliasDrive = $null
    try {
        if (($IsWindows -eq $true -or $env:OS -eq "Windows_NT") -and ($projectFullPath.Length -ge 240 -or $sourceRootFullPath.Length -ge 120)) {
            $usedDriveLetters = @(Get-PSDrive -PSProvider FileSystem |
                Where-Object { ![string]::IsNullOrWhiteSpace($_.Name) } |
                ForEach-Object { ([string]$_.Name).ToUpperInvariant() })
            foreach ($candidate in @("Z", "Y", "X", "W", "V", "U", "T", "S", "R", "Q", "P")) {
                if ($usedDriveLetters -notcontains $candidate) {
                    $aliasDrive = "${candidate}:"
                    break
                }
            }

            if ([string]::IsNullOrWhiteSpace($aliasDrive)) {
                throw "No free drive letter is available for generated app build path aliasing."
            }

            $substOutput = & subst $aliasDrive $workspaceRootFullPath 2>&1
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to create generated app build path alias $aliasDrive -> '$workspaceRootFullPath'. $substOutput"
            }

            $aliasRoot = "$aliasDrive\"
            $buildWorkingDirectory = Join-Path $aliasRoot $sourceRootWorkspaceRelativePath
            $buildProjectFile = Join-Path $aliasRoot $projectWorkspaceRelativePath
        }

        Invoke-External -FileName "dotnet" -Arguments @("build", $buildProjectFile, "--no-incremental") -WorkingDirectory $buildWorkingDirectory -TranscriptPath $TranscriptPath | Out-Null
    }
    finally {
        if (![string]::IsNullOrWhiteSpace($aliasDrive)) {
            & subst $aliasDrive /d 2>$null | Out-Null
        }
    }
}

function New-WorkspaceRootDriveAlias {
    param([string]$Purpose)

    if (!($IsWindows -eq $true -or $env:OS -eq "Windows_NT")) {
        return $null
    }

    $workspaceRootFullPath = [System.IO.Path]::GetFullPath($WorkspaceRoot)
    $usedDriveLetters = @(Get-PSDrive -PSProvider FileSystem |
        Where-Object { ![string]::IsNullOrWhiteSpace($_.Name) } |
        ForEach-Object { ([string]$_.Name).ToUpperInvariant() })
    $aliasDrive = $null
    foreach ($candidate in @("Z", "Y", "X", "W", "V", "U", "T", "S", "R", "Q", "P")) {
        if ($usedDriveLetters -notcontains $candidate) {
            $aliasDrive = "${candidate}:"
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($aliasDrive)) {
        throw "No free drive letter is available for $Purpose path aliasing."
    }

    $substOutput = & subst $aliasDrive $workspaceRootFullPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create $Purpose workspace drive alias $aliasDrive -> '$workspaceRootFullPath'. $substOutput"
    }

    return [pscustomobject]@{
        Drive = $aliasDrive
        Root = "$aliasDrive\"
        WorkspaceRoot = $workspaceRootFullPath
        Purpose = $Purpose
    }
}

function Remove-WorkspaceRootDriveAlias {
    param([object]$Alias)

    if ($null -eq $Alias -or [string]::IsNullOrWhiteSpace([string]$Alias.Drive)) {
        return
    }

    & subst ([string]$Alias.Drive) /d 2>$null | Out-Null
}

function Start-GeneratedAppBrowserHost {
    param(
        [object]$SourceRoot,
        [string]$ScenarioDir,
        [int]$Port
    )

    $sourceRootFullPath = [System.IO.Path]::GetFullPath([string]$SourceRoot.Root)
    $projectFullPath = [System.IO.Path]::GetFullPath([string]$SourceRoot.ProjectFile)
    $workspaceRootFullPath = [System.IO.Path]::GetFullPath($WorkspaceRoot)
    $projectRelativePath = [System.IO.Path]::GetRelativePath($workspaceRootFullPath, $projectFullPath)
    $sourceRootRelativePath = [System.IO.Path]::GetRelativePath($workspaceRootFullPath, $sourceRootFullPath)
    $alias = New-WorkspaceRootDriveAlias -Purpose "generated app browser validation"
    $projectPath = $projectFullPath
    $workingDirectory = $sourceRootFullPath
    if ($null -ne $alias) {
        $projectPath = Join-Path ([string]$alias.Root) $projectRelativePath
        $workingDirectory = Join-Path ([string]$alias.Root) $sourceRootRelativePath
    }

    $url = "http://127.0.0.1:$Port"
    $handle = $null
    try {
        $handle = Start-CapturedProcess `
            -FileName "dotnet" `
            -Arguments @("run", "--no-build", "--project", $projectPath, "--configuration", "Debug", "--no-launch-profile", "--", "--urls", $url) `
            -WorkingDirectory $workingDirectory `
            -Environment @{
                "ASPNETCORE_ENVIRONMENT" = "Development"
                "DOTNET_ENVIRONMENT" = "Development"
            } `
            -StdoutPath (Join-Path $ScenarioDir "command-transcripts\browser-host.out.log") `
            -StderrPath (Join-Path $ScenarioDir "command-transcripts\browser-host.err.log")

        Wait-HttpReady -Url $url -TimeoutSeconds 240 | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $ScenarioDir "browser-host-ready.json") -Encoding UTF8
        return [pscustomobject]@{
            Handle = $handle
            Alias = $alias
            Url = $url
            ProjectPath = $projectPath
            WorkingDirectory = $workingDirectory
        }
    }
    catch {
        Stop-CapturedProcess $handle
        Remove-WorkspaceRootDriveAlias -Alias $alias
        throw
    }
}

function Stop-GeneratedAppBrowserHost {
    param(
        [object]$BrowserHost,
        [string]$ScenarioDir
    )

    if ($null -eq $BrowserHost) {
        return
    }

    Stop-CapturedProcess $BrowserHost.Handle
    Remove-WorkspaceRootDriveAlias -Alias $BrowserHost.Alias
    [pscustomobject]@{
        stopped = $true
        url = $BrowserHost.Url
        processId = if ($BrowserHost.Handle -and $BrowserHost.Handle.Process) { $BrowserHost.Handle.Process.Id } else { $null }
        aliasDrive = if ($BrowserHost.Alias) { $BrowserHost.Alias.Drive } else { $null }
        aliasWorkspaceRoot = if ($BrowserHost.Alias) { $BrowserHost.Alias.WorkspaceRoot } else { $null }
        timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
    } | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $ScenarioDir "browser-host-cleanup.json") -Encoding UTF8
}

function Assert-GeneratedAppBrowserEvidence {
    param([string]$ScenarioDir)

    $requiredFiles = @(
        "browser\browser-validation-summary.json",
        "browser\browser-console-desktop.txt",
        "browser\browser-console-mobile.txt",
        "browser\browser-network-desktop.txt",
        "browser\browser-network-mobile.txt",
        "browser\screenshots\desktop-initial.png",
        "browser\screenshots\desktop-after-interaction.png",
        "browser\screenshots\desktop-after-reload.png",
        "browser\screenshots\mobile-initial.png",
        "browser\screenshots\mobile-after-interaction.png",
        "browser\screenshots\mobile-after-reload.png",
        "browser-host-ready.json",
        "browser-host-cleanup.json"
    )

    foreach ($relativePath in $requiredFiles) {
        $path = Join-Path $ScenarioDir $relativePath
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing SB04 browser evidence file: $path"
        }

        if ($relativePath.EndsWith(".png", [StringComparison]::OrdinalIgnoreCase) -and (Get-Item -LiteralPath $path).Length -le 0) {
            throw "SB04 browser screenshot is empty: $path"
        }
    }
}

function Invoke-GeneratedAppBrowserValidation {
    param(
        [object]$Scenario,
        [object]$SourceRoot,
        [string]$ScenarioDir,
        [int]$Port
    )

    $browserHost = $null
    $outputRoot = Join-Path $ScenarioDir "browser"
    New-Directory $outputRoot
    $previousUrl = $env:CANDOITALL_SB04_BROWSER_URL
    $previousOutputRoot = $env:CANDOITALL_SB04_BROWSER_OUTPUT_ROOT
    $previousScenarioKey = $env:CANDOITALL_SB04_BROWSER_SCENARIO_KEY

    try {
        $browserHost = Start-GeneratedAppBrowserHost -SourceRoot $SourceRoot -ScenarioDir $ScenarioDir -Port $Port
        $env:CANDOITALL_SB04_BROWSER_URL = $browserHost.Url
        $env:CANDOITALL_SB04_BROWSER_OUTPUT_ROOT = $outputRoot
        $env:CANDOITALL_SB04_BROWSER_SCENARIO_KEY = $Scenario.ScenarioKey
        Invoke-External `
            -FileName "dotnet" `
            -Arguments @(
                "test",
                "tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj",
                "--filter",
                "FullyQualifiedName~Sb04GeneratedAppBrowserValidationTests.Generated_app_supports_desktop_and_mobile_browser_validation",
                "--no-build"
            ) `
            -WorkingDirectory $RepoRoot `
            -TranscriptPath (Join-Path $ScenarioDir "command-transcripts\browser-validation-dotnet-test.txt") | Out-Null
    }
    finally {
        if ($null -eq $previousUrl) { Remove-Item Env:\CANDOITALL_SB04_BROWSER_URL -ErrorAction SilentlyContinue } else { $env:CANDOITALL_SB04_BROWSER_URL = $previousUrl }
        if ($null -eq $previousOutputRoot) { Remove-Item Env:\CANDOITALL_SB04_BROWSER_OUTPUT_ROOT -ErrorAction SilentlyContinue } else { $env:CANDOITALL_SB04_BROWSER_OUTPUT_ROOT = $previousOutputRoot }
        if ($null -eq $previousScenarioKey) { Remove-Item Env:\CANDOITALL_SB04_BROWSER_SCENARIO_KEY -ErrorAction SilentlyContinue } else { $env:CANDOITALL_SB04_BROWSER_SCENARIO_KEY = $previousScenarioKey }
        Stop-GeneratedAppBrowserHost -BrowserHost $browserHost -ScenarioDir $ScenarioDir
    }

    Assert-GeneratedAppBrowserEvidence -ScenarioDir $ScenarioDir
    Write-Utf8File (Join-Path $ScenarioDir "browser-validation.md") (@(
        "# Browser Validation",
        "",
        "- Scenario: $($Scenario.ScenarioKey)",
        "- Host URL: $($browserHost.Url)",
        "- Desktop screenshots: `browser/screenshots/desktop-initial.png`, `desktop-after-interaction.png`, `desktop-after-reload.png`.",
        "- Mobile screenshots: `browser/screenshots/mobile-initial.png`, `mobile-after-interaction.png`, `mobile-after-reload.png`.",
        "- Console logs: `browser/browser-console-desktop.txt`, `browser/browser-console-mobile.txt`.",
        "- Network logs: `browser/browser-network-desktop.txt`, `browser/browser-network-mobile.txt`.",
        "- Validation summary: `browser/browser-validation-summary.json`."
    ) -join [Environment]::NewLine)
}

function Write-BrowserSkipped {
    param(
        [string]$ScenarioDir,
        [string]$Reason
    )

    Write-Utf8File (Join-Path $ScenarioDir "browser-validation-skipped.md") (@(
        "# Browser Validation Skipped",
        "",
        $Reason
    ) -join [Environment]::NewLine)
}

function Run-Scenario {
    param(
        [object]$Scenario,
        [string]$DefinitionId,
        [int]$Index
    )

    $scenarioDir = Join-Path $ScenarioProofRoot $Scenario.ScenarioKey
    if (Test-Path $scenarioDir) {
        Remove-Item -LiteralPath $scenarioDir -Recurse -Force
    }

    New-Directory $scenarioDir
    New-Directory (Join-Path $scenarioDir "command-transcripts")
    New-Directory (Join-Path $scenarioDir "screenshots")

    $projectContext = New-ProjectAndScenarioAsset -Scenario $Scenario -ScenarioDir $scenarioDir
    $runId = Start-ProcessRunForScenario -Scenario $Scenario -DefinitionId $DefinitionId -ProjectContext $projectContext
    Write-Utf8File (Join-Path $scenarioDir "run-start.json") (@{
        scenarioKey = $Scenario.ScenarioKey
        runId = $runId
        projectId = $projectContext.Project.id
        requestAssetId = $projectContext.Asset.id
        expectedSourceRootName = $projectContext.ExpectedSourceRootName
    } | ConvertTo-Json -Depth 20)

    $terminalState = Wait-RunTerminal -RunId $runId -ScenarioDir $scenarioDir
    $sourceRoot = Resolve-GeneratedSourceRoot -ExecutionDetails @($terminalState.ExecutionDetails) -RunId $runId -ExpectedSourceRootName $projectContext.ExpectedSourceRootName
    Assert-ScenarioEvidence -Scenario $Scenario -ScenarioDir $scenarioDir -TerminalState $terminalState -SourceRoot $sourceRoot -ExpectedSourceRootName $projectContext.ExpectedSourceRootName
    Build-GeneratedApp -SourceRoot $sourceRoot -TranscriptPath (Join-Path $scenarioDir "command-transcripts\dotnet-build-generated-app.txt")

    if ($SkipBrowser) {
        Write-BrowserSkipped -ScenarioDir $scenarioDir -Reason "Browser validation intentionally skipped by -SkipBrowser for harness shakeout. Full SB04 closure must run without -SkipBrowser."
    }
    else {
        Invoke-GeneratedAppBrowserValidation -Scenario $Scenario -SourceRoot $sourceRoot -ScenarioDir $scenarioDir -Port ($FirstScenarioPort + $Index)
    }

    return [pscustomobject]@{
        scenarioKey = $Scenario.ScenarioKey
        title = $Scenario.Title
        runId = $runId
        projectId = $projectContext.Project.id
        requestAssetId = $projectContext.Asset.id
        generatedSourceRoot = $sourceRoot.Root
        generatedProjectFile = $sourceRoot.ProjectFile
        proofPath = $scenarioDir
    }
}

function Write-Manifest {
    param([object[]]$ScenarioResults)

    $manifest = [pscustomobject]@{
        schema = "candoitall.sb04.realProcessE2E.v1"
        runStamp = $RunStamp
        databaseName = $DatabaseName
        baseUrl = $BaseUrl
        workspaceRoot = $WorkspaceRoot
        processTemplate = "blazor-app-delivery"
        scenarioCount = @($ScenarioResults).Count
        browserProofDeferred = [bool]$SkipBrowser
        scenarios = $ScenarioResults
    }
    $manifest | ConvertTo-Json -Depth $JsonDepth | Set-Content -Path (Join-Path $ProofRoot "manifest.json") -Encoding UTF8
    Write-Utf8File (Join-Path $ProofRoot "manifest.md") (@(
        "# SB04 Real Process E2E Manifest",
        "",
        "- Run stamp: $RunStamp",
        "- PostgreSQL database: $DatabaseName",
        "- Web host: $BaseUrl",
        "- Process template: blazor-app-delivery",
        "- Scenario count: $(@($ScenarioResults).Count)",
        "- Browser proof deferred: $([bool]$SkipBrowser)",
        "",
        "## Scenarios",
        ($ScenarioResults | ForEach-Object { "- $($_.scenarioKey) run $($_.runId) source $($_.generatedSourceRoot)" }) -join [Environment]::NewLine
    ) -join [Environment]::NewLine)
}

Initialize-ProofRoots
$webHost = $null
$results = @()

try {
    Initialize-BrowserValidator
    Initialize-Postgres
    $webHost = Start-WebHost
    $definitionId = Import-And-PublishProcess
    $selectedScenarios = @(if (@($ScenarioKeys).Count -gt 0) {
        foreach ($scenarioKey in $ScenarioKeys) {
            if ($ScenarioOrder -notcontains $scenarioKey) {
                throw "Unknown SB04 scenario '$scenarioKey'. Known scenarios: $($ScenarioOrder -join ', ')."
            }

            $scenarioKey
        }
    }
    else {
        @($ScenarioOrder | Select-Object -First ([Math]::Clamp($ScenarioLimit, 1, $ScenarioOrder.Count)))
    })

    for ($index = 0; $index -lt $selectedScenarios.Count; $index++) {
        $scenario = Load-Scenario -ScenarioKey $selectedScenarios[$index]
        $results += Run-Scenario -Scenario $scenario -DefinitionId $definitionId -Index $index
    }

    Write-Manifest -ScenarioResults $results
    $results | ConvertTo-Json -Depth $JsonDepth
}
finally {
    if (!$KeepHosts) {
        Stop-CapturedProcess $webHost
        Write-Utf8File (Join-Path $ProofRoot "cleanup-receipt.md") (@(
            "# SB04 Cleanup Receipt",
            "",
            "- Web host stopped: true",
            "- Web port: $WebPort",
            "- PostgreSQL compose service left running for shared repository use; isolated database name: $DatabaseName.",
            "- Timestamp UTC: $((Get-Date).ToUniversalTime().ToString("o"))"
        ) -join [Environment]::NewLine)
    }
}
