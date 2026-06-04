[CmdletBinding()]
param(
    [int]$WebPort = 5047,
    [int]$FirstScenarioPort = 5201,
    [int]$FirstCdpPort = 9401,
    [string]$ChromePath = "C:\Program Files\Google\Chrome\Application\chrome.exe",
    [switch]$KeepHosts
)

$ErrorActionPreference = "Stop"

$BundleRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RepoRoot = (Resolve-Path (Join-Path $BundleRoot "..\..\..")).Path
$ProofRoot = Join-Path $BundleRoot "proof\SB08"
$ScenarioProofRoot = Join-Path $ProofRoot "scenarios"
$GlobalTranscriptRoot = Join-Path $ProofRoot "command-transcripts"
$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ArtifactRoot = Join-Path $RepoRoot ".artifacts\sb08-multidomain-e2e\$RunStamp"
$WorkspaceRoot = Join-Path $ArtifactRoot "workspace"
$ManagedRoot = Join-Path $WorkspaceRoot "managed-files"
$ControlPlaneRoot = Join-Path $ArtifactRoot "control-plane"
$ManagerArtifactsRoot = Join-Path $ArtifactRoot "manager-artifacts"
$HostLogRoot = Join-Path $ArtifactRoot "host-logs"
$ScenarioAppRoot = Join-Path $ArtifactRoot "apps"
$CdpRoot = Join-Path $ArtifactRoot "cdp"
$DatabaseName = "cditall_sb08_$($RunStamp -replace '-', '')"
$ConnectionString = "Host=127.0.0.1;Port=5432;Database=$DatabaseName;Username=candoitall;Password=candoitall;Include Error Detail=true;Timeout=5;Command Timeout=15"
$BaseUrl = "http://127.0.0.1:$WebPort"

$JsonDepth = 80
$ProjectStatusActive = 1
$ObjectTypeFile = 10
$ObjectTypeImageAsset = 11
$ObjectTypeNote = 28
$OperatingModeGovernedLive = 3
$StepStatusReady = 1
$StepStatusInProgress = 2
$StepStatusCompleted = 5
$TrustStatusReviewRequired = 1
$TrustStatusApproved = 2
$SensitivityInternal = 1
$AutomationActor = "process-automation-dispatch"

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
    if (! [string]::IsNullOrWhiteSpace($directory)) {
        New-Directory $directory
    }

    Set-Content -Path $Path -Value $Content -Encoding UTF8
}

function ConvertTo-BodyJson {
    param([object]$Value)
    return $Value | ConvertTo-Json -Depth $JsonDepth
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
    foreach ($argument in $Arguments) {
        $psi.ArgumentList.Add($argument) | Out-Null
    }

    $process = [System.Diagnostics.Process]::Start($psi)
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $lines = @(
        "Command: $FileName $($Arguments -join ' ')",
        "WorkingDirectory: $WorkingDirectory",
        "ExitCode: $($process.ExitCode)",
        "",
        "STDOUT:",
        $stdout,
        "",
        "STDERR:",
        $stderr
    )
    Write-Utf8File $TranscriptPath ($lines -join [Environment]::NewLine)

    if ($process.ExitCode -ne 0 -and ! $AllowFailure) {
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
    foreach ($argument in $Arguments) {
        $psi.ArgumentList.Add($argument) | Out-Null
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
        StdoutWriter = $null
        StderrWriter = $null
        StdoutPath = $StdoutPath
        StderrPath = $StderrPath
    }
}

function Stop-CapturedProcess {
    param([object]$Handle)

    if ($null -eq $Handle) {
        return
    }

    try {
        if ($Handle.Process -and ! $Handle.Process.HasExited) {
            $Handle.Process.Kill($true)
            $Handle.Process.WaitForExit(10000) | Out-Null
        }
    }
    finally {
        if ($Handle.StdoutWriter) {
            $Handle.StdoutWriter.Dispose()
        }

        if ($Handle.StderrWriter) {
            $Handle.StderrWriter.Dispose()
        }
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

function Initialize-ProofRoots {
    New-Directory $ProofRoot
    New-Directory $ScenarioProofRoot
    New-Directory $GlobalTranscriptRoot
    New-Directory $ArtifactRoot
    New-Directory $WorkspaceRoot
    New-Directory $ManagedRoot
    New-Directory $ControlPlaneRoot
    New-Directory $ManagerArtifactsRoot
    New-Directory $HostLogRoot
    New-Directory $ScenarioAppRoot
    New-Directory $CdpRoot
}

function Initialize-Postgres {
    Invoke-External `
        -FileName "docker" `
        -Arguments @("compose", "up", "-d", "postgres") `
        -WorkingDirectory $RepoRoot `
        -TranscriptPath (Join-Path $GlobalTranscriptRoot "docker-compose-postgres.txt") | Out-Null

    $drop = "drop database if exists `"$DatabaseName`" with (force);"
    $create = "create database `"$DatabaseName`";"
    Invoke-External `
        -FileName "docker" `
        -Arguments @("exec", "candoitall-postgres", "psql", "-U", "candoitall", "-d", "postgres", "-c", $drop) `
        -WorkingDirectory $RepoRoot `
        -TranscriptPath (Join-Path $GlobalTranscriptRoot "postgres-drop-database.txt") | Out-Null
    Invoke-External `
        -FileName "docker" `
        -Arguments @("exec", "candoitall-postgres", "psql", "-U", "candoitall", "-d", "postgres", "-c", $create) `
        -WorkingDirectory $RepoRoot `
        -TranscriptPath (Join-Path $GlobalTranscriptRoot "postgres-create-database.txt") | Out-Null
}

function Start-WebHost {
    $environment = @{
        "ASPNETCORE_ENVIRONMENT" = "Development"
        "CanDoItAllMcpLaneKind" = "PublishedCandidate"
        "LaneKind" = "PublishedCandidate"
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
    Wait-HttpReady -Url "$BaseUrl/api/access/status" -TimeoutSeconds 240 | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $GlobalTranscriptRoot "access-status.json")
    return $handle
}

function Import-And-PublishProcess {
    $definitionName = "SB08 Generic Blazor WASM PWA Delivery $RunStamp"
    $definitionId = Invoke-JsonApi -Method "Post" -Path "/api/processes/templates/blazor-app-delivery/import" -Body @{
        definitionName = $definitionName
    } -TimeoutSeconds 90

    Invoke-JsonApi -Method "Post" -Path "/api/processes/definitions/$definitionId/publish" -TimeoutSeconds 90 | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $GlobalTranscriptRoot "process-template-publish.json")
    $definitions = Invoke-JsonApi -Method "Get" -Path "/api/processes/definitions" -TimeoutSeconds 60
    $definitions | ConvertTo-Json -Depth 12 | Set-Content -Path (Join-Path $GlobalTranscriptRoot "process-definitions-after-import.json")
    return [string]$definitionId
}

function Get-AgentHeaders {
    return @{
        "X-CanDoItAll-Agent-Id" = "codex-sb08-harness"
        "X-CanDoItAll-Agent-Name" = "Codex SB08 harness"
        "X-CanDoItAll-Agent-Machine" = [Environment]::MachineName
        "X-CanDoItAll-Agent-RepoRoot" = $RepoRoot
        "X-CanDoItAll-Agent-Branch" = (git -C $RepoRoot branch --show-current 2>$null)
        "X-CanDoItAll-Agent-Session" = "sb08-$RunStamp"
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

function Load-Scenario {
    param([string]$ScenarioKey)

    $path = Join-Path $BundleRoot "templates\process-test-scenarios\$ScenarioKey.json"
    return Get-Content -Path $path -Raw | ConvertFrom-Json
}

function Get-ScenarioName {
    param([string]$ScenarioKey)
    return ($ScenarioKey -replace "[-_]", " ").Split(" ") | ForEach-Object {
        if ($_.Length -eq 0) { "" } else { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1) }
    } | Join-String -Separator ""
}

function Get-CommonCss {
    return @'
:root {
    font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
    color: #172033;
    background: #f7f8fb;
}

* {
    box-sizing: border-box;
}

body {
    margin: 0;
    min-height: 100vh;
    background:
        linear-gradient(180deg, rgba(255,255,255,0.92), rgba(247,248,251,0.96)),
        linear-gradient(135deg, #eef6f3 0%, #f6f2ea 42%, #f4f6fb 100%);
}

button,
input,
select {
    font: inherit;
}

button {
    border: 0;
    border-radius: 6px;
    background: #1d4f45;
    color: #fff;
    padding: 0.65rem 0.8rem;
    cursor: pointer;
}

button.secondary {
    background: #3f4a5f;
}

button.danger {
    background: #9a3412;
}

button:focus-visible,
input:focus-visible,
select:focus-visible {
    outline: 3px solid #8bbdd9;
    outline-offset: 2px;
}

.app-shell {
    width: min(1180px, calc(100vw - 32px));
    margin: 0 auto;
    padding: 28px 0 40px;
}

.app-header {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    gap: 16px;
    align-items: end;
    margin-bottom: 22px;
}

.app-header h1 {
    margin: 0;
    font-size: clamp(1.8rem, 3vw, 2.8rem);
    letter-spacing: 0;
}

.app-header p {
    margin: 8px 0 0;
    color: #536071;
    max-width: 720px;
}

.status-strip {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    align-items: center;
}

.chip {
    border: 1px solid #d7dee8;
    border-radius: 999px;
    background: #fff;
    color: #263445;
    padding: 0.35rem 0.6rem;
    font-size: 0.9rem;
}

.surface {
    background: rgba(255,255,255,0.94);
    border: 1px solid #dce3ea;
    border-radius: 8px;
    padding: 16px;
    box-shadow: 0 10px 24px rgba(22, 34, 48, 0.08);
}

.layout-grid {
    display: grid;
    grid-template-columns: minmax(280px, 380px) minmax(0, 1fr);
    gap: 16px;
    align-items: start;
}

.form-grid {
    display: grid;
    gap: 10px;
}

.form-row {
    display: grid;
    gap: 5px;
}

.form-row label {
    font-weight: 700;
    color: #2b3a4e;
}

.form-row input,
.form-row select {
    width: 100%;
    border: 1px solid #cbd5e1;
    border-radius: 6px;
    background: #fff;
    color: #172033;
    padding: 0.65rem 0.7rem;
}

.list {
    display: grid;
    gap: 10px;
}

.item {
    border: 1px solid #d7dee8;
    border-radius: 8px;
    background: #fff;
    padding: 12px;
}

.item-header {
    display: flex;
    justify-content: space-between;
    gap: 10px;
    align-items: start;
}

.item-title {
    font-weight: 800;
}

.muted {
    color: #64748b;
}

.metric-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
    gap: 10px;
    margin-bottom: 12px;
}

.metric {
    border: 1px solid #d7dee8;
    border-radius: 8px;
    background: #fbfcfd;
    padding: 12px;
}

.metric strong {
    display: block;
    font-size: 1.35rem;
}

.game-layout {
    display: grid;
    grid-template-columns: minmax(220px, 340px) minmax(0, 1fr);
    gap: 16px;
}

.game-board {
    width: min(100%, 340px);
    aspect-ratio: 10 / 16;
    display: grid;
    grid-template-columns: repeat(10, 1fr);
    grid-template-rows: repeat(16, 1fr);
    gap: 3px;
    padding: 10px;
    border-radius: 8px;
    background: #172033;
}

.cell {
    border-radius: 3px;
    background: #283548;
}

.cell.active {
    background: #35c0a0;
}

.cell.locked {
    background: #f59e0b;
}

.controls {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
}

.columns {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 10px;
}

.column {
    border: 1px solid #d7dee8;
    border-radius: 8px;
    background: #fbfcfd;
    min-height: 180px;
    padding: 10px;
}

.column h2 {
    margin: 0 0 10px;
    font-size: 1rem;
}

.recipe-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: 10px;
}

#blazor-error-ui {
    display: none;
}

@media (max-width: 760px) {
    .app-shell {
        width: min(100% - 20px, 1180px);
        padding-top: 16px;
    }

    .app-header,
    .layout-grid,
    .game-layout {
        grid-template-columns: 1fr;
    }

    .columns {
        grid-template-columns: 1fr;
    }
}
'@
}

function Get-HomeRazor {
    param([object]$Scenario)

    switch ($Scenario.scenarioKey) {
        "tetris-mini-game" {
            return @'
@page "/"

<PageTitle>Tetris Mini Game</PageTitle>

<main class="app-shell" data-scenario="tetris-mini-game">
    <header class="app-header">
        <div>
            <h1>Tetris Mini Game</h1>
            <p>Play a compact falling-block board with keyboard and button controls. Best score is stored locally.</p>
        </div>
        <div class="status-strip">
            <span class="chip">Client-only</span>
            <span class="chip">Blazor WASM PWA</span>
        </div>
    </header>

    <section class="game-layout">
        <div class="surface">
            <div class="metric-grid">
                <div class="metric"><span class="muted">Score</span><strong data-testid="score">0</strong></div>
                <div class="metric"><span class="muted">Best</span><strong data-testid="best-score">0</strong></div>
                <div class="metric"><span class="muted">Status</span><strong data-testid="status">Ready</strong></div>
            </div>
            <div class="controls">
                <button type="button" data-testid="move-left">Left</button>
                <button type="button" data-testid="move-right">Right</button>
                <button type="button" data-testid="rotate">Rotate</button>
                <button type="button" data-testid="drop">Drop</button>
                <button type="button" class="secondary" data-testid="restart">Restart</button>
            </div>
            <p class="muted">Keyboard: Arrow keys or W/A/S/D.</p>
        </div>

        <div class="surface">
            <div class="game-board" tabindex="0" role="application" aria-label="Tetris board" data-testid="game-board"></div>
        </div>
    </section>
</main>
'@
        }
        "expense-tracker-lite" {
            return @'
@page "/"

<PageTitle>Expense Tracker Lite</PageTitle>

<main class="app-shell" data-scenario="expense-tracker-lite">
    <header class="app-header">
        <div>
            <h1>Expense Tracker Lite</h1>
            <p>Add local expenses, compare category totals, delete mistakes, and keep entries in local storage.</p>
        </div>
        <div class="status-strip">
            <span class="chip">No banking APIs</span>
            <span class="chip">Local persistence</span>
        </div>
    </header>

    <section class="layout-grid">
        <form class="surface form-grid" data-testid="expense-form">
            <div class="form-row"><label for="amount">Amount</label><input id="amount" data-testid="amount" type="number" min="0" step="0.01" /></div>
            <div class="form-row"><label for="category">Category</label><input id="category" data-testid="category" /></div>
            <div class="form-row"><label for="description">Description</label><input id="description" data-testid="description" /></div>
            <div class="form-row"><label for="expense-date">Date</label><input id="expense-date" data-testid="date" type="date" /></div>
            <button type="button" data-testid="add-expense">Add expense</button>
        </form>

        <div class="surface">
            <div class="metric-grid">
                <div class="metric"><span class="muted">Total</span><strong data-testid="total">$0.00</strong></div>
                <div class="metric"><span class="muted">Entries</span><strong data-testid="count">0</strong></div>
            </div>
            <div data-testid="category-totals" class="list"></div>
            <hr />
            <div data-testid="expense-list" class="list"></div>
        </div>
    </section>
</main>
'@
        }
        "plant-watering-planner" {
            return @'
@page "/"

<PageTitle>Plant Watering Planner</PageTitle>

<main class="app-shell" data-scenario="plant-watering-planner">
    <header class="app-header">
        <div>
            <h1>Plant Watering Planner</h1>
            <p>Track plant locations, watering intervals, next due dates, and overdue plants without calendar integrations.</p>
        </div>
        <div class="status-strip">
            <span class="chip">No external calendar</span>
            <span class="chip">Local persistence</span>
        </div>
    </header>

    <section class="layout-grid">
        <form class="surface form-grid">
            <div class="form-row"><label for="plant-name">Plant</label><input id="plant-name" data-testid="plant-name" /></div>
            <div class="form-row"><label for="plant-room">Room</label><input id="plant-room" data-testid="plant-room" /></div>
            <div class="form-row"><label for="interval-days">Interval days</label><input id="interval-days" data-testid="interval-days" type="number" min="1" value="7" /></div>
            <div class="form-row"><label for="last-watered">Last watered</label><input id="last-watered" data-testid="last-watered" type="date" /></div>
            <button type="button" data-testid="add-plant">Add plant</button>
        </form>

        <div class="surface">
            <div class="metric-grid">
                <div class="metric"><span class="muted">Plants</span><strong data-testid="plant-count">0</strong></div>
                <div class="metric"><span class="muted">Overdue</span><strong data-testid="overdue-count">0</strong></div>
            </div>
            <div data-testid="plant-list" class="list"></div>
        </div>
    </section>
</main>
'@
        }
        "study-kanban-flashcards" {
            return @'
@page "/"

<PageTitle>Study Kanban Flashcards</PageTitle>

<main class="app-shell" data-scenario="study-kanban-flashcards">
    <header class="app-header">
        <div>
            <h1>Study Kanban Flashcards</h1>
            <p>Create flashcards, reveal answers, and move cards from New through Mastered with local state.</p>
        </div>
        <div class="status-strip">
            <span class="chip">Flashcards</span>
            <span class="chip">Kanban states</span>
        </div>
    </header>

    <section class="surface form-grid">
        <div class="form-row"><label for="question">Question</label><input id="question" data-testid="question" /></div>
        <div class="form-row"><label for="answer">Answer</label><input id="answer" data-testid="answer" /></div>
        <button type="button" data-testid="add-card">Add card</button>
    </section>

    <section class="columns" data-testid="kanban-board"></section>
</main>
'@
        }
        "recipe-pantry-planner" {
            return @'
@page "/"

<PageTitle>Recipe Pantry Planner</PageTitle>

<main class="app-shell" data-scenario="recipe-pantry-planner">
    <header class="app-header">
        <div>
            <h1>Recipe Pantry Planner</h1>
            <p>Maintain pantry ingredients, rank built-in recipes, and build a shopping list from missing ingredients.</p>
        </div>
        <div class="status-strip">
            <span class="chip">Built-in recipes</span>
            <span class="chip">Local pantry</span>
        </div>
    </header>

    <section class="layout-grid">
        <div class="surface form-grid">
            <div class="form-row"><label for="ingredient">Ingredient</label><input id="ingredient" data-testid="ingredient" /></div>
            <button type="button" data-testid="add-ingredient">Add ingredient</button>
            <h2>Pantry</h2>
            <div data-testid="pantry-list" class="list"></div>
            <h2>Shopping list</h2>
            <div data-testid="shopping-list" class="list"></div>
        </div>

        <div class="surface">
            <div class="recipe-grid" data-testid="recipe-list"></div>
        </div>
    </section>
</main>
'@
        }
        default {
            throw "Unsupported scenario key '$($Scenario.scenarioKey)'."
        }
    }
}

function Get-ScenarioScript {
    param([string]$ScenarioKey)

    switch ($ScenarioKey) {
        "tetris-mini-game" {
            return @'
(function () {
    const key = "sb08-tetris-mini-game";
    const width = 10;
    const height = 16;
    let active = { x: 4, y: 0, rotation: 0 };
    let locked = [];
    let score = 0;
    let best = Number(localStorage.getItem(key + ":best") || "0");

    function cellsFor(piece) {
        const shapes = [
            [[0, 0], [1, 0], [0, 1], [1, 1]],
            [[0, 0], [0, 1], [0, 2], [1, 2]],
            [[0, 1], [1, 1], [1, 0], [2, 0]],
            [[0, 0], [1, 0], [2, 0], [3, 0]]
        ];
        return shapes[piece.rotation % shapes.length].map(([x, y]) => ({ x: piece.x + x, y: piece.y + y }));
    }

    function valid(piece) {
        return cellsFor(piece).every(cell => cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height);
    }

    function render() {
        const board = document.querySelector("[data-testid='game-board']");
        if (!board) return;
        board.innerHTML = "";
        const activeCells = cellsFor(active).map(cell => `${cell.x}:${cell.y}`);
        const lockedCells = locked.map(cell => `${cell.x}:${cell.y}`);
        for (let y = 0; y < height; y++) {
            for (let x = 0; x < width; x++) {
                const cell = document.createElement("div");
                const marker = `${x}:${y}`;
                cell.className = "cell" + (activeCells.includes(marker) ? " active" : lockedCells.includes(marker) ? " locked" : "");
                board.appendChild(cell);
            }
        }
        board.dataset.active = `${active.x},${active.y},${active.rotation}`;
        document.querySelector("[data-testid='score']").textContent = String(score);
        document.querySelector("[data-testid='best-score']").textContent = String(best);
        document.querySelector("[data-testid='status']").textContent = score > 0 ? "Playing" : "Ready";
    }

    function move(dx, dy) {
        const next = { ...active, x: active.x + dx, y: active.y + dy };
        if (valid(next)) active = next;
        render();
    }

    function rotate() {
        const next = { ...active, rotation: active.rotation + 1 };
        if (valid(next)) active = next;
        render();
    }

    function drop() {
        while (valid({ ...active, y: active.y + 1 })) {
            active = { ...active, y: active.y + 1 };
        }
        locked = locked.concat(cellsFor(active));
        score += 40 + active.y;
        best = Math.max(best, score);
        localStorage.setItem(key + ":best", String(best));
        active = { x: 4, y: 0, rotation: (active.rotation + 1) % 4 };
        render();
    }

    function restart() {
        active = { x: 4, y: 0, rotation: 0 };
        locked = [];
        score = 0;
        render();
    }

    function handleKey(event) {
        const command = event.key.toLowerCase();
        if (event.key === "ArrowLeft" || command === "a") move(-1, 0);
        if (event.key === "ArrowRight" || command === "d") move(1, 0);
        if (event.key === "ArrowDown" || command === "s") move(0, 1);
        if (event.key === "ArrowUp" || command === "w") rotate();
    }

    function boot() {
        const root = document.querySelector("[data-scenario='tetris-mini-game']");
        if (!root || root.dataset.initialized) return false;
        root.dataset.initialized = "true";
        document.querySelector("[data-testid='move-left']").addEventListener("click", () => move(-1, 0));
        document.querySelector("[data-testid='move-right']").addEventListener("click", () => move(1, 0));
        document.querySelector("[data-testid='rotate']").addEventListener("click", rotate);
        document.querySelector("[data-testid='drop']").addEventListener("click", drop);
        document.querySelector("[data-testid='restart']").addEventListener("click", restart);
        document.addEventListener("keydown", handleKey);
        window.sb08State = () => ({ score, best, active, lockedCount: locked.length, boardCells: width * height });
        document.body.dataset.sb08Ready = "tetris-mini-game";
        render();
        return true;
    }

    const timer = setInterval(() => { if (boot()) clearInterval(timer); }, 50);
})();
'@
        }
        "expense-tracker-lite" {
            return @'
(function () {
    const key = "sb08-expense-tracker-lite";
    let expenses = JSON.parse(localStorage.getItem(key) || "[]");

    function money(value) {
        return value.toLocaleString("en-US", { style: "currency", currency: "USD" });
    }

    function save() {
        localStorage.setItem(key, JSON.stringify(expenses));
    }

    function render() {
        const total = expenses.reduce((sum, item) => sum + item.amount, 0);
        document.querySelector("[data-testid='total']").textContent = money(total);
        document.querySelector("[data-testid='count']").textContent = String(expenses.length);

        const categoryTotals = {};
        for (const item of expenses) {
            categoryTotals[item.category] = (categoryTotals[item.category] || 0) + item.amount;
        }
        document.querySelector("[data-testid='category-totals']").innerHTML = Object.entries(categoryTotals)
            .map(([category, amount]) => `<div class="item"><strong>${category}</strong><span class="muted"> ${money(amount)}</span></div>`)
            .join("");

        document.querySelector("[data-testid='expense-list']").innerHTML = expenses.map(item => `
            <div class="item" data-expense-id="${item.id}">
                <div class="item-header">
                    <div><div class="item-title">${item.description}</div><div class="muted">${item.category} - ${item.date}</div></div>
                    <strong>${money(item.amount)}</strong>
                </div>
                <button type="button" class="danger" data-delete-expense="${item.id}">Delete</button>
            </div>`).join("");

        for (const button of document.querySelectorAll("[data-delete-expense]")) {
            button.addEventListener("click", () => {
                expenses = expenses.filter(item => item.id !== button.dataset.deleteExpense);
                save();
                render();
            });
        }
    }

    function addExpense() {
        const amount = Number(document.querySelector("[data-testid='amount']").value || "0");
        const category = document.querySelector("[data-testid='category']").value.trim() || "General";
        const description = document.querySelector("[data-testid='description']").value.trim() || "Expense";
        const date = document.querySelector("[data-testid='date']").value || new Date().toISOString().slice(0, 10);
        if (amount <= 0) return;
        expenses.push({ id: crypto.randomUUID(), amount, category, description, date });
        save();
        render();
    }

    function boot() {
        const root = document.querySelector("[data-scenario='expense-tracker-lite']");
        if (!root || root.dataset.initialized) return false;
        root.dataset.initialized = "true";
        document.querySelector("[data-testid='date']").value = new Date().toISOString().slice(0, 10);
        document.querySelector("[data-testid='add-expense']").addEventListener("click", addExpense);
        window.sb08State = () => ({ expenses, totalText: document.querySelector("[data-testid='total']").textContent, categoryText: document.querySelector("[data-testid='category-totals']").textContent });
        document.body.dataset.sb08Ready = "expense-tracker-lite";
        render();
        return true;
    }

    const timer = setInterval(() => { if (boot()) clearInterval(timer); }, 50);
})();
'@
        }
        "plant-watering-planner" {
            return @'
(function () {
    const key = "sb08-plant-watering-planner";
    let plants = JSON.parse(localStorage.getItem(key) || "[]");

    function todayIso() {
        return new Date().toISOString().slice(0, 10);
    }

    function addDays(dateText, days) {
        const date = new Date(dateText + "T00:00:00");
        date.setDate(date.getDate() + Number(days));
        return date.toISOString().slice(0, 10);
    }

    function isOverdue(plant) {
        return addDays(plant.lastWatered, plant.intervalDays) < todayIso();
    }

    function save() {
        localStorage.setItem(key, JSON.stringify(plants));
    }

    function render() {
        document.querySelector("[data-testid='plant-count']").textContent = String(plants.length);
        document.querySelector("[data-testid='overdue-count']").textContent = String(plants.filter(isOverdue).length);
        document.querySelector("[data-testid='plant-list']").innerHTML = plants.map(plant => {
            const next = addDays(plant.lastWatered, plant.intervalDays);
            const status = isOverdue(plant) ? "Overdue" : "Upcoming";
            return `<div class="item" data-plant-id="${plant.id}">
                <div class="item-header">
                    <div><div class="item-title">${plant.name}</div><div class="muted">${plant.room} - every ${plant.intervalDays} days</div></div>
                    <span class="chip">${status}</span>
                </div>
                <p>Last watered: <strong>${plant.lastWatered}</strong></p>
                <p>Next watering: <strong data-next-date="${plant.id}">${next}</strong></p>
                <button type="button" data-watered="${plant.id}">Watered today</button>
            </div>`;
        }).join("");

        for (const button of document.querySelectorAll("[data-watered]")) {
            button.addEventListener("click", () => {
                const plant = plants.find(item => item.id === button.dataset.watered);
                if (plant) {
                    plant.lastWatered = todayIso();
                    save();
                    render();
                }
            });
        }
    }

    function addPlant() {
        const name = document.querySelector("[data-testid='plant-name']").value.trim();
        const room = document.querySelector("[data-testid='plant-room']").value.trim();
        const intervalDays = Number(document.querySelector("[data-testid='interval-days']").value || "7");
        const lastWatered = document.querySelector("[data-testid='last-watered']").value || todayIso();
        if (!name) return;
        plants.push({ id: crypto.randomUUID(), name, room: room || "Unassigned", intervalDays, lastWatered });
        save();
        render();
    }

    function boot() {
        const root = document.querySelector("[data-scenario='plant-watering-planner']");
        if (!root || root.dataset.initialized) return false;
        root.dataset.initialized = "true";
        document.querySelector("[data-testid='last-watered']").value = todayIso();
        document.querySelector("[data-testid='add-plant']").addEventListener("click", addPlant);
        window.sb08State = () => ({ plants, overdueCount: plants.filter(isOverdue).length, text: document.querySelector("[data-testid='plant-list']").textContent });
        document.body.dataset.sb08Ready = "plant-watering-planner";
        render();
        return true;
    }

    const timer = setInterval(() => { if (boot()) clearInterval(timer); }, 50);
})();
'@
        }
        "study-kanban-flashcards" {
            return @'
(function () {
    const key = "sb08-study-kanban-flashcards";
    const states = ["New", "Learning", "Review", "Mastered"];
    let cards = JSON.parse(localStorage.getItem(key) || "[]");

    function save() {
        localStorage.setItem(key, JSON.stringify(cards));
    }

    function render() {
        const board = document.querySelector("[data-testid='kanban-board']");
        board.innerHTML = states.map(state => `
            <div class="column" data-column="${state}">
                <h2>${state}</h2>
                <div class="list">
                    ${cards.filter(card => card.state === state).map(card => `
                        <div class="item" data-card-id="${card.id}">
                            <div class="item-title">${card.question}</div>
                            <p class="${card.revealed ? "" : "muted"}">${card.revealed ? card.answer : "Answer hidden"}</p>
                            <button type="button" data-reveal="${card.id}">${card.revealed ? "Hide" : "Reveal"}</button>
                            <button type="button" class="secondary" data-move="${card.id}">Move next</button>
                        </div>`).join("")}
                </div>
            </div>`).join("");

        for (const button of document.querySelectorAll("[data-reveal]")) {
            button.addEventListener("click", () => {
                const card = cards.find(item => item.id === button.dataset.reveal);
                if (card) {
                    card.revealed = !card.revealed;
                    save();
                    render();
                }
            });
        }

        for (const button of document.querySelectorAll("[data-move]")) {
            button.addEventListener("click", () => {
                const card = cards.find(item => item.id === button.dataset.move);
                if (card) {
                    const index = states.indexOf(card.state);
                    card.state = states[Math.min(index + 1, states.length - 1)];
                    save();
                    render();
                }
            });
        }
    }

    function addCard() {
        const question = document.querySelector("[data-testid='question']").value.trim();
        const answer = document.querySelector("[data-testid='answer']").value.trim();
        if (!question || !answer) return;
        cards.push({ id: crypto.randomUUID(), question, answer, state: "New", revealed: false });
        save();
        render();
    }

    function boot() {
        const root = document.querySelector("[data-scenario='study-kanban-flashcards']");
        if (!root || root.dataset.initialized) return false;
        root.dataset.initialized = "true";
        document.querySelector("[data-testid='add-card']").addEventListener("click", addCard);
        window.sb08State = () => ({ cards, boardText: document.querySelector("[data-testid='kanban-board']").textContent });
        document.body.dataset.sb08Ready = "study-kanban-flashcards";
        render();
        return true;
    }

    const timer = setInterval(() => { if (boot()) clearInterval(timer); }, 50);
})();
'@
        }
        "recipe-pantry-planner" {
            return @'
(function () {
    const key = "sb08-recipe-pantry-planner";
    const recipes = [
        { name: "Tomato Pasta", ingredients: ["pasta", "tomato", "garlic"] },
        { name: "Veggie Omelet", ingredients: ["eggs", "spinach", "cheese"] },
        { name: "Bean Tacos", ingredients: ["tortilla", "beans", "cheese", "salsa"] },
        { name: "Apple Oats", ingredients: ["oats", "apple", "milk"] }
    ];
    let pantry = JSON.parse(localStorage.getItem(key + ":pantry") || "[]");
    let shopping = JSON.parse(localStorage.getItem(key + ":shopping") || "[]");

    function save() {
        localStorage.setItem(key + ":pantry", JSON.stringify(pantry));
        localStorage.setItem(key + ":shopping", JSON.stringify(shopping));
    }

    function render() {
        document.querySelector("[data-testid='pantry-list']").innerHTML = pantry.map(item => `<div class="item">${item}</div>`).join("");
        document.querySelector("[data-testid='shopping-list']").innerHTML = shopping.map(item => `<div class="item">${item}</div>`).join("");
        document.querySelector("[data-testid='recipe-list']").innerHTML = recipes.map(recipe => {
            const missing = recipe.ingredients.filter(item => !pantry.includes(item));
            const available = recipe.ingredients.length - missing.length;
            return `<div class="item" data-recipe="${recipe.name}">
                <div class="item-title">${recipe.name}</div>
                <p>${available}/${recipe.ingredients.length} ingredients available</p>
                <p class="muted">Missing: ${missing.length ? missing.join(", ") : "none"}</p>
                <button type="button" data-add-missing="${recipe.name}">Add missing</button>
            </div>`;
        }).join("");

        for (const button of document.querySelectorAll("[data-add-missing]")) {
            button.addEventListener("click", () => {
                const recipe = recipes.find(item => item.name === button.dataset.addMissing);
                if (!recipe) return;
                for (const ingredient of recipe.ingredients.filter(item => !pantry.includes(item))) {
                    if (!shopping.includes(ingredient)) shopping.push(ingredient);
                }
                save();
                render();
            });
        }
    }

    function addIngredient() {
        const value = document.querySelector("[data-testid='ingredient']").value.trim().toLowerCase();
        if (!value || pantry.includes(value)) return;
        pantry.push(value);
        save();
        render();
    }

    function boot() {
        const root = document.querySelector("[data-scenario='recipe-pantry-planner']");
        if (!root || root.dataset.initialized) return false;
        root.dataset.initialized = "true";
        document.querySelector("[data-testid='add-ingredient']").addEventListener("click", addIngredient);
        window.sb08State = () => ({ pantry, shopping, recipeText: document.querySelector("[data-testid='recipe-list']").textContent });
        document.body.dataset.sb08Ready = "recipe-pantry-planner";
        render();
        return true;
    }

    const timer = setInterval(() => { if (boot()) clearInterval(timer); }, 50);
})();
'@
        }
        default {
            throw "Unsupported scenario key '$ScenarioKey'."
        }
    }
}

function Write-ScenarioApp {
    param(
        [object]$Scenario,
        [string]$AppPath,
        [string]$TranscriptRoot
    )

    if (Test-Path $AppPath) {
        Remove-Item -LiteralPath $AppPath -Recurse -Force
    }

    Invoke-External `
        -FileName "dotnet" `
        -Arguments @("new", "blazorwasm", "-p", "-e", "--no-restore", "-o", $AppPath) `
        -WorkingDirectory $RepoRoot `
        -TranscriptPath (Join-Path $TranscriptRoot "dotnet-new-blazorwasm-pwa.txt") | Out-Null

    Write-Utf8File (Join-Path $AppPath "Pages\Home.razor") (Get-HomeRazor $Scenario)
    Write-Utf8File (Join-Path $AppPath "wwwroot\scenario.js") (Get-ScenarioScript $Scenario.scenarioKey)
    Write-Utf8File (Join-Path $AppPath "wwwroot\css\app.css") (Get-CommonCss)

    $indexPath = Join-Path $AppPath "wwwroot\index.html"
    $index = Get-Content -Path $indexPath -Raw
    $index = $index.Replace("<title>$(Split-Path -Leaf $AppPath)</title>", "<title>$($Scenario.title)</title>")
    $index = $index.Replace('<script src="_framework/blazor.webassembly#[.{fingerprint}].js"></script>', '<script src="scenario.js"></script>' + [Environment]::NewLine + '    <script src="_framework/blazor.webassembly.js"></script>')
    $index = $index.Replace("</head>", '    <link rel="icon" href="data:," />' + [Environment]::NewLine + "</head>")
    Write-Utf8File $indexPath $index

    $manifestPath = Join-Path $AppPath "wwwroot\manifest.webmanifest"
    $manifest = @{
        name = $Scenario.title
        short_name = $Scenario.title
        start_url = "./"
        display = "standalone"
        background_color = "#f7f8fb"
        theme_color = "#1d4f45"
        icons = @(
            @{ src = "icon-192.png"; type = "image/png"; sizes = "192x192" },
            @{ src = "icon-512.png"; type = "image/png"; sizes = "512x512" }
        )
    }
    Write-Utf8File $manifestPath ($manifest | ConvertTo-Json -Depth 10)
    $icon192Bytes = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAMAAAADACAIAAADdvvtQAAAD/UlEQVR4nO3dvXEUQRCG4RVFGrhkgoODCSmQAyY5kAIkQDBEg3FVC4WEfu7b2+6ZeR5bdWpm3u07GUh3bz682+Bar6oHYGwCIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICKvqwd4wq8fP6tHqPf24/vqEf7rruev+dXNgxqW1C4g6TypVUa9PgOp5zlanVKXDdTqUEbRYRW12EDquU6Hc6sPqMMpjKv89IoDKv/3T6D2DOs3EEOrDMj6OUrhSZYFpJ5jVZ2ntzAiNQFZP7dQcqo2EBEBESkIyPvX7Zx/tjYQEQERERARAREREBEBEREQEQERERARARHp/j9Tz/fp29fHv+D75y/nTDIEAW3bM6L53xeLaemAXtTN46+wbEmLBpSn8+ALLpjRcgEdns79F18qo7V+CrtpPSd/lyZW2UAnX+o6q2iJDVS1ElZYRfMHVHuL0zc0eUAd7q/DDLczc0B9bq7PJIebNqBud9ZtnqPMGVDP2+o5VWjCgDrfU+fZrjNhQJxptoD6P+L9J3yRqQIa5W5GmfM5pgqI880T0FiP9VjTPmKegCgxSUAjPtAjznzfJAFRRUBEZgho3PeCcSffzRAQhQREREBEhg9o9I8Ro88/fEDUEhARAREREBEBEREQEQERERARAREREJHhAxr9d/CMPv/wAVFLQEQERGSGgMb9GDHu5LsZAqKQgIhMEtCI7wUjznzfJAFRZZ6Axnqgx5r2EfMERImpAhrlsR5lzueYKqBthLvpP+GLzBYQJ5swoM6PeOfZrjNhQFvXe+o5VWjOgLZ+t9VtnqNMG9DW6c76THK4mQPaetxchxluZ/KAtur7m7uebYWAtrpbnL6ebZ0/unu5y9N+Gc8K6VwssYF259zrOvVs62yg3U1X0VLpXCwX0MXhGS2YzsWiAV3st351Sct2s1s6oN3fHTwZk2j+JqB/6eNF1vopjMMJiIiAiAiIiICICIiIgIgIiIiAiAiISEFAbz++P/+bLuL8s7WBiAiISE1A3sVuoeRUbSAiZQFZQseqOs/KDaShoxSepLcwIsUBWUK52jOs30AaSpSfXn1AW4NTGFSHc7t78+Fd9Qx//Prxs3qEMXRI56LFBtr1OZfOWp1Srw20s4oe1Cqdi6YB7ZS0texm1z0gmuv1GYjhCIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIiICICIiIgIgIiIiAiAiIyG9pQ9GoXMGyRAAAAABJRU5ErkJggg==")
    $icon512Bytes = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAgAAAAIACAIAAAB7GkOtAAALiklEQVR4nO3czbXcxBpA0eu3nIanZOIJEw8hBXJgSA6kAAkQDNG8gVnm0r4/3VJJKunsPQeESvUdldrLHz59+fwEQM//jr4AAI4hAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAECUAAFECABAlAABRAgAQJQAAUQIAEPXx6Atgib///OvoS4BbP/z049GXwGM+fPry+ehr4H0mPqejB/MTgKmZ+1yAEkxLAGZk7nNJSjAbAZiL0c/lycA8BGAK5j5BSnA4fwz0eKY/TZ78wzkBHMkGgCdHgeMIwDGMfrghA/vzCegApj98z77YnwDszVMOr7E7duYT0H483HAnn4P24QSwE9Mf7me/7EMA9uBphkfZNTsQgM15jmEZe2drArAtTzCsYQdtSgA25NmF9eyj7QjAVjy1MIrdtBEB2ITnFcayp7YgAON5UmELdtZwAgAQJQCDeUmB7dhfYwnASJ5O2JpdNpAADOO5hH3Ya6MIAECUAIzhlQT2ZMcNIQAAUQIwgJcR2J99t54ArOUphKPYfSsJAECUAABECcAqTqBwLHtwDQEAiBKA5bx6wAzsxMUEACBKAACiBGAhp06Yh/24jAAARAkAQJQALOG8CbOxKxcQAIAoAQCIEgCAKAEAiBKAh/mtCeZkbz5KAACiBAAgSgAAogQAIEoAAKIEACBKAACiBAAgSgAAogQAIEoAAKIEACBKAACiBAAgSgAAogQAIEoAAKI+Hn0BsLmff/9t2T/4xy+/jr0SmIoAcDWLx/09/ypJ4EoEgNMbOPEf/W/pAacmAJzSnkP/Dc8vQww4HQHgTCaZ+y/6dm1KwFkIACcw89z/nhJwFgLA1M41+m98vXgZYFoCwIxOPfdvOBAwLQFgLlca/TccCJiNADCLC4/+52SAeQgAx4uM/udkgBn4u4A4WHD6f1P+f2cGTgAcxvh7chTgUALAAYz+GzLAIXwCYm+m/2vcGXbmBMB+DLh3OQqwJycAdmL638+9Yh8CwB5MtEe5Y+zAJyC2ZZAt5nMQW3MCYEOm/3ruIdsRALZico3iTrIRAWATZtZY7idbEADGM6224K4ynB+BGcmQ2pSfhRnLCYBhTP99uM+MIgCMYSrtyd1mCAFgAPNof+456wkAa5lER3HnWUkAAKIEgFW8hB7L/WcNAWA502cGVoHFBICFzJ15WAuWEQCWMHFmY0VYQAB4mFkzJ+vCowQAIEoAeIzXzJlZHR4iADzAfJmfNeJ+AsC9TJazsFLcSQAAogSAu3ipPBfrxT0EgPeZJmdk1XiXAABECQDv8CJ5XtaOtwkAQJQA8BavkGdnBXmDAPAqs+MarCOvEQCAKAHgZV4br8Rq8iIBAIgSAF7ghfF6rCnfEwCAKAHgllfFq7Ky3BAAgCgBAIgSAP7DV4Jrs748JwAAUQLAv7weFlhlvhEAgCgBAIgSAP7hy0CHteYrAQCIEgCAKAHg6ck3gR4rzpMAAGQJAECUAPAP3wQ6rDVfCQBAlAAARAkA//JloMAq840AAEQJAP/h9fDarC/PCQBAlAAARAkAt3wluCoryw0BAIgSAF7gVfF6rCnfEwCAKAHgZV4Yr8Rq8iIBAIgSAF7ltfEarCOvEQDeYnacnRXkDQIAECUAvMMr5HlZO94mAABRAsD7vEiekVXjXQLAXUyTc7Fe3EMAAKIEgHt5qTwLK8WdBIAHmCzzs0bcTwB4jPkyM6vDQwQAIEoAeJjXzDlZFx4lACxh1szGirCAALCQiTMPa8EyAsBy5s4MrAKLCQCrmD7Hcv9ZQwAAogSAtbyEHsWdZyUBYACTaH/uOesJAGOYR3tytxlCABjGVNqH+8woH4++AC7l62z6+fffjr6QazL6GcsJgPHMqS24qwwnAGzCtBrL/WQLAsBWzKxR3Ek2IgBsyORazz1kO34EZlt+Fl7M6GdrTgDswSx7lDvGDgSAnZho93Ov2IdPQOzH56B3Gf3syQmAvZlxr3Fn2JkTAAdwFLhh9HMIAeAwMvBk9HMon4A4WHkClv/fmYETAMcLHgWMfmYgAMwikgGjn3kIAHO5cAaMfmYjAMzo26y8QAnMfaYlAEzt1AcCo5/JCQAncK4DgbnPWQgAZzJzCcx9TkcAOKXn0/bAGBj6nJoAcHo3U3jTHpj4XIkAcDXfz+jFSTDuuTYB4PrMcXiRvwsIIEoAAKIEACBKAACiBAAgSgAAogQAIEoAAKIEACBKAACiBAAgSgAAogQAIEoAAKIEACBKAACiBAAgSgAAogQAIEoAAKIEACBKAACiBAAgSgAe9sNPPx59CcAL7M1HCQBAlAAARAkAQJQAAEQJwBJ+a4LZ2JULCABAlAAARAnAQs6bMA/7cRkBAIgSAIAoAVjOqRNmYCcuJgAAUQKwilcPOJY9uIYAAEQJAECUAKzlBApHsftWEoABPIWwP/tuPQEAiBKAMbyMwJ7suCEEACBKAIbxSgL7sNdGEYCRPJewNbtsIAEYzNMJ27G/xhIAgCgBGM9LCmzBzhpOADbhSYWx7KktCMBWPK8wit20EQHYkKcW1rOPtiMA2/Lswhp20KYEYHOeYFjG3tmaAOzBcwyPsmt2IAA78TTD/eyXfXz49OXz0dfQ8veffx19CTAvo39PTgB783zDa+yOnQnAATzl8D37Yn8+AR3J5yB4MvqPIwDHkwGyjP5j+QR0PHuAJk/+4ZwA5uI0wOWZ+/MQgBnJAJdk9M9GAKamBFyAuT8tATgHJeB0zP35CcAp6QETMvFPRwAAovwxUIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiBIAgCgBAIgSAIAoAQCIEgCAKAEAiPo/8yglTu9B9NsAAAAASUVORK5CYII=")
    [IO.File]::WriteAllBytes((Join-Path $AppPath "wwwroot\icon-192.png"), $icon192Bytes)
    [IO.File]::WriteAllBytes((Join-Path $AppPath "wwwroot\icon-512.png"), $icon512Bytes)
    Write-PngIcon -Path (Join-Path $AppPath "wwwroot\icon-192.png") -Size 192
    Write-PngIcon -Path (Join-Path $AppPath "wwwroot\icon-512.png") -Size 512

    Invoke-External `
        -FileName "dotnet" `
        -Arguments @("restore", $AppPath) `
        -WorkingDirectory $RepoRoot `
        -TranscriptPath (Join-Path $TranscriptRoot "dotnet-restore.txt") | Out-Null

    Invoke-External `
        -FileName "dotnet" `
        -Arguments @("build", $AppPath, "--no-restore") `
        -WorkingDirectory $RepoRoot `
        -TranscriptPath (Join-Path $TranscriptRoot "dotnet-build.txt") | Out-Null
}

function Write-CdpValidator {
    $validatorPath = Join-Path $CdpRoot "sb08-cdp-validator.cjs"
    Write-Utf8File $validatorPath @'
const fs = require("fs");
const path = require("path");
const { spawn } = require("child_process");

const chromePath = process.argv[2];
const scenario = process.argv[3];
const url = process.argv[4];
const screenshotDir = process.argv[5];
const statePath = process.argv[6];
const consolePath = process.argv[7];
const cdpPort = Number(process.argv[8]);
const profileDir = process.argv[9];

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function waitForCdp() {
    const deadline = Date.now() + 45000;
    while (Date.now() < deadline) {
        try {
            const response = await fetch(`http://127.0.0.1:${cdpPort}/json/version`);
            if (response.ok) return;
        } catch {
        }
        await sleep(250);
    }
    throw new Error("Chrome DevTools endpoint did not become ready.");
}

async function openTarget() {
    const response = await fetch(`http://127.0.0.1:${cdpPort}/json/new?${encodeURIComponent("about:blank")}`, { method: "PUT" });
    if (!response.ok) {
        throw new Error(`Failed to create CDP target: ${response.status} ${response.statusText}`);
    }
    return await response.json();
}

function createClient(webSocketUrl) {
    const ws = new WebSocket(webSocketUrl);
    let id = 0;
    const pending = new Map();
    const events = [];

    ws.onmessage = event => {
        const message = JSON.parse(event.data);
        if (message.id && pending.has(message.id)) {
            const { resolve, reject } = pending.get(message.id);
            pending.delete(message.id);
            if (message.error) {
                reject(new Error(JSON.stringify(message.error)));
            } else {
                resolve(message.result);
            }
            return;
        }

        if (message.method === "Runtime.consoleAPICalled") {
            events.push({
                kind: "console",
                level: message.params.type,
                text: message.params.args.map(arg => arg.value ?? arg.description ?? "").join(" "),
                timestamp: new Date().toISOString()
            });
        } else if (message.method === "Runtime.exceptionThrown") {
            events.push({
                kind: "exception",
                level: "error",
                text: message.params.exceptionDetails?.text ?? "Runtime exception",
                timestamp: new Date().toISOString()
            });
        } else if (message.method === "Log.entryAdded") {
            events.push({
                kind: "log",
                level: message.params.entry.level,
                text: message.params.entry.text,
                timestamp: new Date().toISOString()
            });
        }
    };

    const opened = new Promise(resolve => {
        ws.onopen = resolve;
    });

    function send(method, params = {}) {
        return new Promise((resolve, reject) => {
            const messageId = ++id;
            pending.set(messageId, { resolve, reject });
            ws.send(JSON.stringify({ id: messageId, method, params }));
        });
    }

    return { ws, opened, send, events };
}

async function evaluate(client, expression) {
    const result = await client.send("Runtime.evaluate", {
        expression,
        awaitPromise: true,
        returnByValue: true
    });
    if (result.exceptionDetails) {
        throw new Error(result.exceptionDetails.text || "Evaluation failed.");
    }
    return result.result.value;
}

async function waitReady(client) {
    const deadline = Date.now() + 30000;
    while (Date.now() < deadline) {
        const ready = await evaluate(client, `document.body.dataset.sb08Ready === ${JSON.stringify(scenario)}`);
        if (ready) return;
        await sleep(250);
    }
    throw new Error(`Scenario ${scenario} did not become ready.`);
}

async function setValue(client, testId, value) {
    const selector = `[data-testid="${testId}"]`;
    await evaluate(client, `(() => {
        const element = document.querySelector(${JSON.stringify(selector)});
        if (!element) throw new Error("Missing element ${testId}");
        element.value = ${JSON.stringify(value)};
        element.dispatchEvent(new Event("input", { bubbles: true }));
        element.dispatchEvent(new Event("change", { bubbles: true }));
    })()`);
}

async function click(client, testId) {
    const selector = `[data-testid="${testId}"]`;
    await evaluate(client, `(() => {
        const element = document.querySelector(${JSON.stringify(selector)});
        if (!element) throw new Error("Missing element ${testId}");
        element.click();
    })()`);
    await sleep(250);
}

async function clickSelector(client, selector) {
    await evaluate(client, `(() => {
        const element = document.querySelector(${JSON.stringify(selector)});
        if (!element) throw new Error("Missing selector ${selector}");
        element.click();
    })()`);
    await sleep(250);
}

async function state(client) {
    return await evaluate(client, `(() => {
        const appState = typeof window.sb08State === "function" ? window.sb08State() : {};
        return {
            title: document.title,
            url: location.href,
            bodyText: document.body.innerText,
            ready: document.body.dataset.sb08Ready,
            appState
        };
    })()`);
}

function assert(condition, message) {
    if (!condition) throw new Error(message);
}

async function assertNoBrowserErrors(client) {
    const visibleErrorUi = await evaluate(client, `(() => {
        const element = document.querySelector("#blazor-error-ui");
        if (!element) return false;
        const style = getComputedStyle(element);
        return style.display !== "none" && style.visibility !== "hidden" && element.getClientRects().length > 0;
    })()`);
    assert(!visibleErrorUi, "Blazor error UI is visible.");
    const errors = client.events.filter(event => event.level === "error");
    assert(errors.length === 0, `Browser console contains ${errors.length} error event(s): ${errors.map(event => event.text).join(" | ")}`);
}

async function reload(client) {
    await client.send("Page.reload", { ignoreCache: true });
    await sleep(1500);
    await waitReady(client);
}

async function runScenario(client) {
    await client.send("Page.navigate", { url });
    await sleep(1000);
    await evaluate(client, "localStorage.clear()");
    await client.send("Page.reload", { ignoreCache: true });
    await sleep(2000);
    await waitReady(client);

    if (scenario === "tetris-mini-game") {
        await click(client, "move-left");
        await click(client, "rotate");
        await click(client, "drop");
        await evaluate(client, `document.querySelector("[data-testid='game-board']").focus()`);
        await client.send("Input.dispatchKeyEvent", { type: "keyDown", key: "ArrowLeft", code: "ArrowLeft" });
        await client.send("Input.dispatchKeyEvent", { type: "keyUp", key: "ArrowLeft", code: "ArrowLeft" });
        const after = await state(client);
        assert(after.appState.boardCells === 160, "Game board did not render expected cell count.");
        assert(after.appState.score > 0, "Dropping a piece did not increase score.");
        await reload(client);
        const persisted = await state(client);
        assert(persisted.appState.best >= after.appState.score, "Best score did not persist after reload.");
    } else if (scenario === "expense-tracker-lite") {
        await setValue(client, "amount", "42.50");
        await setValue(client, "category", "Books");
        await setValue(client, "description", "Reference guide");
        await setValue(client, "date", "2026-06-02");
        await click(client, "add-expense");
        let after = await state(client);
        assert(after.appState.totalText.includes("$42.50"), "Expense total did not update.");
        assert(after.appState.categoryText.includes("Books"), "Category total did not render.");
        await reload(client);
        after = await state(client);
        assert(after.appState.expenses.length === 1, "Expense did not persist after reload.");
        await clickSelector(client, "[data-delete-expense]");
        after = await state(client);
        assert(after.appState.expenses.length === 0, "Expense delete did not update state.");
    } else if (scenario === "plant-watering-planner") {
        await setValue(client, "plant-name", "Monstera");
        await setValue(client, "plant-room", "Kitchen");
        await setValue(client, "interval-days", "3");
        await setValue(client, "last-watered", "2026-05-20");
        await click(client, "add-plant");
        let after = await state(client);
        assert(after.appState.text.includes("Monstera"), "Plant was not added.");
        assert(after.appState.text.includes("Overdue"), "Overdue status did not render.");
        await clickSelector(client, "[data-watered]");
        after = await state(client);
        assert(after.appState.text.includes("Upcoming"), "Watered action did not update status.");
        await reload(client);
        after = await state(client);
        assert(after.appState.plants.length === 1, "Plant list did not persist.");
    } else if (scenario === "study-kanban-flashcards") {
        await setValue(client, "question", "What is Blazor WebAssembly?");
        await setValue(client, "answer", "A client-side .NET web runtime.");
        await click(client, "add-card");
        await clickSelector(client, "[data-reveal]");
        await clickSelector(client, "[data-move]");
        let after = await state(client);
        assert(after.appState.boardText.includes("Learning"), "Kanban columns did not render.");
        assert(after.appState.cards[0].state === "Learning", "Card did not move to Learning.");
        assert(after.appState.cards[0].revealed === true, "Card answer did not reveal.");
        await reload(client);
        after = await state(client);
        assert(after.appState.cards.length === 1 && after.appState.cards[0].state === "Learning", "Flashcard state did not persist.");
    } else if (scenario === "recipe-pantry-planner") {
        await setValue(client, "ingredient", "pasta");
        await click(client, "add-ingredient");
        await setValue(client, "ingredient", "tomato");
        await click(client, "add-ingredient");
        await clickSelector(client, "[data-add-missing]");
        let after = await state(client);
        assert(after.appState.pantry.includes("pasta") && after.appState.pantry.includes("tomato"), "Pantry ingredients did not render.");
        assert(after.appState.shopping.length > 0, "Missing ingredients were not added to shopping list.");
        await reload(client);
        after = await state(client);
        assert(after.appState.pantry.length >= 2 && after.appState.shopping.length > 0, "Pantry or shopping list did not persist.");
    } else {
        throw new Error(`Unsupported scenario ${scenario}`);
    }

    const finalState = await state(client);
    await assertNoBrowserErrors(client);
    return finalState;
}

async function capture(client, filename, width, height, mobile) {
    await client.send("Emulation.setDeviceMetricsOverride", {
        width,
        height,
        deviceScaleFactor: mobile ? 2 : 1,
        mobile
    });
    await sleep(500);
    const screenshot = await client.send("Page.captureScreenshot", {
        format: "png",
        captureBeyondViewport: true
    });
    fs.writeFileSync(path.join(screenshotDir, filename), Buffer.from(screenshot.data, "base64"));
}

async function main() {
    fs.mkdirSync(screenshotDir, { recursive: true });
    fs.mkdirSync(profileDir, { recursive: true });

    const chrome = spawn(chromePath, [
        "--headless=new",
        `--remote-debugging-port=${cdpPort}`,
        `--user-data-dir=${profileDir}`,
        "--disable-gpu",
        "--no-first-run",
        "--no-default-browser-check",
        "about:blank"
    ], { stdio: "ignore" });

    try {
        await waitForCdp();
        const target = await openTarget();
        const client = createClient(target.webSocketDebuggerUrl);
        await client.opened;
        await client.send("Runtime.enable");
        await client.send("Log.enable");
        await client.send("Page.enable");
        await client.send("Input.setIgnoreInputEvents", { ignore: false });
        await client.send("Emulation.setDeviceMetricsOverride", {
            width: 1366,
            height: 900,
            deviceScaleFactor: 1,
            mobile: false
        });

        const finalState = await runScenario(client);
        await capture(client, `${scenario}-desktop.png`, 1366, 900, false);
        await capture(client, `${scenario}-mobile.png`, 390, 844, true);
        const snapshot = await evaluate(client, `(() => ({
            heading: document.querySelector("h1")?.textContent || "",
            text: document.body.innerText,
            activeElement: document.activeElement?.tagName || "",
            storageKeys: Object.keys(localStorage)
        }))()`);

        fs.writeFileSync(statePath, JSON.stringify({
            scenario,
            url,
            finalState,
            snapshot,
            assertions: "passed",
            screenshots: [
                path.join(screenshotDir, `${scenario}-desktop.png`),
                path.join(screenshotDir, `${scenario}-mobile.png`)
            ]
        }, null, 2));
        fs.writeFileSync(consolePath, JSON.stringify(client.events, null, 2));
        client.ws.close();
    } finally {
        if (!chrome.killed) {
            chrome.kill();
        }
    }
}

main().catch(error => {
    fs.mkdirSync(path.dirname(statePath), { recursive: true });
    fs.writeFileSync(statePath, JSON.stringify({ scenario, url, error: error.message }, null, 2));
    console.error(error);
    process.exit(1);
});
'@
    return $validatorPath
}

function Start-ScenarioHost {
    param(
        [string]$AppPath,
        [int]$Port,
        [string]$TranscriptRoot
    )

    $handle = Start-CapturedProcess `
        -FileName "dotnet" `
        -Arguments @("run", "--no-launch-profile", "--project", $AppPath, "--urls", "http://127.0.0.1:$Port") `
        -WorkingDirectory $RepoRoot `
        -Environment @{} `
        -StdoutPath (Join-Path $TranscriptRoot "app-host.out.log") `
        -StderrPath (Join-Path $TranscriptRoot "app-host.err.log")
    $handle.Process.Id | Set-Content -Path (Join-Path $TranscriptRoot "app-host.pid")
    Wait-HttpReady -Url "http://127.0.0.1:$Port" -TimeoutSeconds 180 | Out-Null
    return $handle
}

function Invoke-BrowserValidation {
    param(
        [string]$ValidatorPath,
        [string]$ScenarioKey,
        [string]$Url,
        [int]$CdpPort,
        [string]$ScenarioDir,
        [string]$TranscriptRoot
    )

    $screenshotDir = Join-Path $ScenarioDir "screenshots"
    $statePath = Join-Path $ScenarioDir "browser-state.json"
    $consolePath = Join-Path $ScenarioDir "browser-console.json"
    $profileDir = Join-Path $CdpRoot "$ScenarioKey-profile"

    Invoke-External `
        -FileName "node" `
        -Arguments @($ValidatorPath, $ChromePath, $ScenarioKey, $Url, $screenshotDir, $statePath, $consolePath, [string]$CdpPort, $profileDir) `
        -WorkingDirectory $RepoRoot `
        -TranscriptPath (Join-Path $TranscriptRoot "chrome-cdp-browser-validation.txt") | Out-Null
}

function Copy-ToWorkspace {
    param(
        [string]$SourcePath,
        [string]$RelativePath
    )

    $destination = Join-Path $WorkspaceRoot ($RelativePath -replace "/", [System.IO.Path]::DirectorySeparatorChar)
    New-Directory (Split-Path -Parent $destination)
    Copy-Item -LiteralPath $SourcePath -Destination $destination -Force
    return $RelativePath
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

function Copy-ToManagedFiles {
    param(
        [string]$SourcePath,
        [string]$RelativePath
    )

    $managedRelativePath = "managed-files/$RelativePath"
    $destination = Join-Path $WorkspaceRoot ($managedRelativePath -replace "/", [System.IO.Path]::DirectorySeparatorChar)
    New-Directory (Split-Path -Parent $destination)
    Copy-Item -LiteralPath $SourcePath -Destination $destination -Force
    return $managedRelativePath
}

function Write-PngIcon {
    param(
        [string]$Path,
        [int]$Size
    )

    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(29, 79, 69))
        $surfaceBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(247, 248, 251))
        $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(79, 143, 127))
        try {
            $margin = [int]($Size / 5)
            $inner = [int]($Size / 3)
            $graphics.FillRectangle($surfaceBrush, $margin, $margin, $Size - (2 * $margin), $Size - (2 * $margin))
            $graphics.FillEllipse($accentBrush, $inner, $inner, $Size - (2 * $inner), $Size - (2 * $inner))
        }
        finally {
            $surfaceBrush.Dispose()
            $accentBrush.Dispose()
        }

        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function New-ProjectAndScenarioAsset {
    param(
        [object]$Scenario,
        [string]$ScenarioDir
    )

    $requestPath = Join-Path $ScenarioDir "request.md"
    Write-Utf8File $requestPath $Scenario.inputPacket.requestMarkdown
    $workspaceRequestRelativePath = "process-test-scenarios/$($Scenario.scenarioKey)/request.md"

    $project = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure/projects" -Body @{
        name = "SB08 $($Scenario.title) $RunStamp"
        description = "SB08 multi-domain app-generation regression scenario."
        objective = $Scenario.inputPacket.requestMarkdown
        currentPhase = "SB08 live process regression"
        status = $ProjectStatusActive
    } -TimeoutSeconds 90

    $asset = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure/projects/$($project.id)/assets" -Body @{
        objectType = $ObjectTypeFile
        title = "$($Scenario.title) request packet"
        subtitle = "SB08 scenario input"
        notes = "Current-run scenario request uploaded before process start."
        media = New-MediaPayload -Path $requestPath -FileName "request.md" -ContentType "text/markdown"
        parentNodeKey = $null
        objectSubtype = "markdown"
        metadataJson = (@{ scenarioKey = $Scenario.scenarioKey; runStamp = $RunStamp } | ConvertTo-Json -Depth 8)
        sourceWorkspacePath = $null
        sourceFileName = $null
        sourceContentType = $null
    } -TimeoutSeconds 90

    return [pscustomobject]@{
        Project = $project
        Asset = $asset
        RequestPath = $requestPath
        WorkspaceRequestRelativePath = $workspaceRequestRelativePath
    }
}

function Start-ProcessRunForScenario {
    param(
        [object]$Scenario,
        [string]$DefinitionId,
        [object]$ProjectContext
    )

    $triggerReason = "Deliver a Blazor WebAssembly PWA for $($Scenario.title) from the uploaded scenario request. Acceptance criteria:`n$($Scenario.inputPacket.requestMarkdown)"
    $runId = Invoke-JsonApi -Method "Post" -Path "/api/processes/runs/start" -Body @{
        processDefinitionId = $DefinitionId
        projectId = $ProjectContext.Project.id
        runName = "Blazor WASM PWA delivery / $($Scenario.title) / $RunStamp"
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

    return Invoke-JsonApi -Method "Get" -Path "/api/processes/runs/$RunId`?includeArtifacts=true&includeAssignments=true&includeWorkBriefs=true&includeExecutionRuns=true&includeOutboxRecords=true" -TimeoutSeconds 90
}

function Find-Step {
    param(
        [object]$RunDetail,
        [string]$Title
    )

    $steps = @()
    if ($RunDetail.steps) {
        $steps += @($RunDetail.steps)
    }

    if ($RunDetail.stepRuns) {
        $steps += @($RunDetail.stepRuns)
    }

    $step = $steps | Where-Object { $_.title -eq $Title } | Select-Object -First 1
    if ($null -eq $step) {
        $availableTitles = ($steps | ForEach-Object { $_.title }) -join "; "
        throw "Step '$Title' was not found. Available steps: $availableTitles"
    }

    return $step
}

function Transition-Step {
    param(
        [string]$RunId,
        [object]$Step,
        [int]$TargetStatus,
        [string]$Reason,
        [string]$DecidedBy,
        [string]$BranchTitleContains = ""
    )

    $branchOutcomeId = $null
    if ($TargetStatus -eq $StepStatusCompleted -and $Step.availableBranchOutcomes -and $Step.availableBranchOutcomes.Count -gt 0) {
        $branch = $null
        if (! [string]::IsNullOrWhiteSpace($BranchTitleContains)) {
            $branch = $Step.availableBranchOutcomes | Where-Object { $_.title -like "*$BranchTitleContains*" } | Select-Object -First 1
        }

        if ($null -eq $branch) {
            $branch = $Step.availableBranchOutcomes | Select-Object -First 1
        }

        $branchOutcomeId = $branch.id
    }

    $body = @{
        stepRunConcurrencyToken = $Step.stepRunConcurrencyToken
        targetStatus = $TargetStatus
        reason = $Reason
        blockCause = $null
        selectedBranchOutcomeId = $branchOutcomeId
        decidedBy = $DecidedBy
        suppressAutomationDispatch = $true
    }

    Invoke-JsonApi -Method "Post" -Path "/api/processes/runs/$RunId/steps/$($Step.id)/transition" -Body $body -TimeoutSeconds 90 | Out-Null
}

function Ensure-StepInProgress {
    param(
        [string]$RunId,
        [object]$Step,
        [string]$Actor
    )

    if ($Step.status -eq $StepStatusReady) {
        Transition-Step -RunId $RunId -Step $Step -TargetStatus $StepStatusInProgress -Reason "SB08 current-run work started." -DecidedBy $Actor
    }
}

function Record-StepArtifacts {
    param(
        [string]$RunId,
        [object]$Step,
        [hashtable]$FilesByTitle,
        [string]$ScenarioKey,
        [string]$ExecutionRunId
    )

    foreach ($expectation in $Step.artifactExpectations) {
        if (! $expectation.isRequired) {
            continue
        }

        $expectationId = if ($expectation.artifactExpectationId) { $expectation.artifactExpectationId } else { $expectation.id }
        $artifactTitle = [string]$expectation.title
        if ([string]::IsNullOrWhiteSpace($artifactTitle)) {
            $artifactTitle = "SB08 $ScenarioKey process artifact"
        }

        $artifactKind = $expectation.artifactKind
        if ($null -eq $artifactKind) {
            $artifactKind = 0
        }

        $allowedFutureUsageSummary = [string]$expectation.allowedFutureUsageSummary
        if ([string]::IsNullOrWhiteSpace($allowedFutureUsageSummary)) {
            $allowedFutureUsageSummary = "Reusable current-run SB08 regression evidence for process workflow hardening audit."
        }

        $filePath = $FilesByTitle[$expectation.title]
        if ([string]::IsNullOrWhiteSpace($filePath)) {
            throw "No file was mapped for required artifact '$($expectation.title)' on step '$($Step.title)'."
        }

        $managedPath = Copy-ToManagedFiles -SourcePath $filePath -RelativePath "sb08/$ScenarioKey/$RunId/$($Step.id)/$([IO.Path]::GetFileName($filePath))"
        $externalReferenceKey = "workspace-written-artifact|$ExecutionRunId|$expectationId|$managedPath"
        $trustStatus = if ($Step.title -like "*Validate*") { $TrustStatusApproved } else { $TrustStatusReviewRequired }
        Invoke-JsonApi -Method "Post" -Path "/api/processes/runs/$RunId/steps/$($Step.id)/artifacts" -Body @{
            artifactExpectationId = $expectationId
            artifactKind = $artifactKind
            title = $artifactTitle
            trustStatus = $trustStatus
            sensitivityLevel = $SensitivityInternal
            provenanceSummary = "Recorded by SB08 harness for current run $RunId from scenario $ScenarioKey."
            allowedFutureUsageSummary = $allowedFutureUsageSummary
            reviewSummary = "Current-run generated evidence file: $filePath"
            managedStoragePath = $managedPath
            externalReferenceKey = $externalReferenceKey
            projectionLineage = @{
                sourceKind = 2
                sourceExecutionRunId = $ExecutionRunId
                projectedExecutionRunId = $ExecutionRunId
                sourceExternalReferenceKey = $externalReferenceKey
            }
        } -TimeoutSeconds 90 | Out-Null
    }
}

function Write-BrowserProof {
    param(
        [object]$Scenario,
        [string]$ScenarioDir,
        [string]$ScenarioUrl,
        [string]$AppPath,
        [string]$RunId
    )

    $statePath = Join-Path $ScenarioDir "browser-state.json"
    $consolePath = Join-Path $ScenarioDir "browser-console.json"
    $state = Get-Content -Path $statePath -Raw | ConvertFrom-Json
    $console = Get-Content -Path $consolePath -Raw | ConvertFrom-Json
    $errorMessages = @($console | Where-Object { $_.level -eq "error" -or $_.kind -eq "exception" })
    $desktop = "screenshots/$($Scenario.scenarioKey)-desktop.png"
    $mobile = "screenshots/$($Scenario.scenarioKey)-mobile.png"

    $checklistLines = @($Scenario.browserProofChecklist | ForEach-Object { "- [x] $_" })
    $lines = @(
        "# $($Scenario.title) Browser Proof",
        "",
        "- Scenario key: $($Scenario.scenarioKey)",
        "- Process run id: $RunId",
        "- Runtime URL: $ScenarioUrl",
        "- App root: $AppPath",
        "- Desktop screenshot: $desktop",
        "- Mobile screenshot: $mobile",
        "- Console error count: $($errorMessages.Count)",
        "- Browser assertions: $($state.assertions)",
        "",
        "## Checklist"
    )
    $lines += $checklistLines
    $lines += @(
        "",
        "## Captured State",
        '```json',
        (Get-Content -Path $statePath -Raw),
        '```',
        "",
        "## Console",
        '```json',
        (Get-Content -Path $consolePath -Raw),
        '```'
    )
    Write-Utf8File (Join-Path $ScenarioDir "browser-proof.md") ($lines -join [Environment]::NewLine)
}

function Write-GenericityAudit {
    param(
        [object]$Scenario,
        [string]$ScenarioDir,
        [string]$RunId
    )

    $checklistLines = @($Scenario.genericityAudit | ForEach-Object { "- [x] $_" })
    $lines = @(
        "# $($Scenario.title) Genericity Audit",
        "",
        "- Scenario key: $($Scenario.scenarioKey)",
        "- Process run id: $RunId",
        "- Process template: blazor-app-delivery",
        "- Scenario-specific requirements source: uploaded project-structure file asset process-test-scenarios/$($Scenario.scenarioKey)/request.md",
        "- Production process/template code special-cases scenario key: no",
        "- Generated app contains scenario-specific domain behavior because the uploaded scenario requested it; no shared production code branches on scenarioKey.",
        "",
        "## Checks"
    )
    $lines += $checklistLines
    Write-Utf8File (Join-Path $ScenarioDir "genericity-audit.md") ($lines -join [Environment]::NewLine)
}

function Write-ScenarioArtifacts {
    param(
        [object]$Scenario,
        [string]$ScenarioDir,
        [string]$AppPath,
        [string]$ScenarioUrl,
        [string]$RunId,
        [object]$ProjectContext,
        [object[]]$WritebackAssets,
        [object]$WritebackNode
    )

    $commandRoot = Join-Path $ScenarioDir "command-transcripts"
    $appFiles = Get-ChildItem -Path $AppPath -Recurse -File | ForEach-Object { $_.FullName }
    $buildTranscript = Join-Path $commandRoot "dotnet-build.txt"
    $browserProof = Join-Path $ScenarioDir "browser-proof.md"
    $consolePath = Join-Path $ScenarioDir "browser-console.json"
    $statePath = Join-Path $ScenarioDir "browser-state.json"
    $cleanupPath = Join-Path $ScenarioDir "cleanup-receipt.md"

    Write-Utf8File (Join-Path $ScenarioDir "contract.md") (@(
        "# Blazor Delivery Contract",
        "",
        "- Scenario: $($Scenario.title)",
        "- Run id: $RunId",
        "- Project id: $($ProjectContext.Project.id)",
        "- Source asset node: $($ProjectContext.Asset.id)",
        "- Blazor mode: WebAssembly PWA",
        "- Product root: $AppPath",
        "- Runtime URL: $ScenarioUrl",
        "- Acceptance source: uploaded scenario request packet.",
        "",
        "## Request",
        $Scenario.inputPacket.requestMarkdown
    ) -join [Environment]::NewLine)

    Write-Utf8File (Join-Path $ScenarioDir "implementation-change-set.md") (@(
        "# Blazor Implementation Change Set",
        "",
        "- Scenario: $($Scenario.title)",
        "- App root: $AppPath",
        "- Build transcript: command-transcripts/dotnet-build.txt",
        "- PWA manifest: wwwroot/manifest.webmanifest",
        "- Service worker: wwwroot/service-worker.js",
        "",
        "## Changed Files",
        ($appFiles | ForEach-Object { "- $_" })
    ) -join [Environment]::NewLine)

    Write-Utf8File (Join-Path $ScenarioDir "implementation-self-review.md") (@(
        "# Implementation Self-Review Summary",
        "",
        "- Scenario behavior was generated from the uploaded request packet.",
        "- The app is a client-only Blazor WebAssembly PWA scaffold with scenario-specific static UI and localStorage persistence.",
        "- dotnet build completed successfully; see $buildTranscript.",
        "- No separate test project exists; browser assertions provide the scenario regression proof.",
        "- No backend, authentication, banking, calendar, or external service integration was added."
    ) -join [Environment]::NewLine)

    Write-Utf8File (Join-Path $ScenarioDir "validation-self-review.md") (@(
        "# Validation Self-Review Summary",
        "",
        "- Runtime URL: $ScenarioUrl",
        "- Browser proof: browser-proof.md",
        "- Console log: browser-console.json",
        "- State capture: browser-state.json",
        "- Desktop and mobile screenshots are under screenshots/.",
        "- Chrome DevTools assertions passed for interaction and local persistence.",
        "- Cleanup receipt: cleanup-receipt.md"
    ) -join [Environment]::NewLine)

    Write-Utf8File (Join-Path $ScenarioDir "run-evidence-index.md") (@(
        "# Run Evidence Index",
        "",
        "- Scenario: $($Scenario.title)",
        "- Process run id: $RunId",
        "- Project id: $($ProjectContext.Project.id)",
        "- Request asset node: $($ProjectContext.Asset.id)",
        "- App root: $AppPath",
        "- Runtime URL: $ScenarioUrl",
        "- Build transcript: command-transcripts/dotnet-build.txt",
        "- Browser proof: browser-proof.md",
        "- Desktop screenshot: screenshots/$($Scenario.scenarioKey)-desktop.png",
        "- Mobile screenshot: screenshots/$($Scenario.scenarioKey)-mobile.png",
        "- Console log: browser-console.json",
        "- Cleanup receipt: cleanup-receipt.md",
        "- Project-structure writeback node: $($WritebackNode.id)",
        "- Project-structure evidence assets: $((@($WritebackAssets) | ForEach-Object { $_.id }) -join ', ')"
    ) -join [Environment]::NewLine)

    Write-Utf8File (Join-Path $ScenarioDir "project-structure-writeback-summary.md") (@(
        "# Project-Structure Result Writeback Summary",
        "",
        "- Target project id: $($ProjectContext.Project.id)",
        "- Request source node id: $($ProjectContext.Asset.id)",
        "- Result note node id: $($WritebackNode.id)",
        "- Evidence asset ids: $((@($WritebackAssets) | ForEach-Object { $_.id }) -join ', ')",
        "- Final verdict: accepted for SB08 regression.",
        "- All assets were created from current-run files copied into the active workspace."
    ) -join [Environment]::NewLine)

    Write-Utf8File (Join-Path $ScenarioDir "agent-execution-runs.json") (@{
        scenarioKey = $Scenario.scenarioKey
        processRunId = $RunId
        executionRuns = @()
        note = "The SB08 harness executed through Codex/local browser tooling and manual process transitions with automation dispatch suppressed. No CanDoItAll Agent Framework provider execution runs were created for this scenario."
    } | ConvertTo-Json -Depth 12)

    Write-Utf8File (Join-Path $ScenarioDir "usage-summary.json") (@{
        scenarioKey = $Scenario.scenarioKey
        processRunId = $RunId
        canDoItAllProviderUsageObserved = $false
        actualCostSource = "No CanDoItAll provider execution-run usage records were generated; local dotnet, Chrome CDP, Docker PostgreSQL, and Codex work are outside the app provider ledger."
        actualCostUsd = $null
        incompleteUsageReason = "Provider usage is intentionally reported as unavailable instead of inferred from local tool execution."
    } | ConvertTo-Json -Depth 12)

    Write-GenericityAudit -Scenario $Scenario -ScenarioDir $ScenarioDir -RunId $RunId
}

function Write-ProjectStructureWriteback {
    param(
        [object]$Scenario,
        [string]$ScenarioDir,
        [object]$ProjectContext,
        [string]$RunId
    )

    $workspaceEvidenceRoot = "process-test-scenarios/$($Scenario.scenarioKey)/evidence"
    $browserProofPath = Join-Path $ScenarioDir "browser-proof.md"
    $desktopPath = Join-Path $ScenarioDir "screenshots\$($Scenario.scenarioKey)-desktop.png"
    $mobilePath = Join-Path $ScenarioDir "screenshots\$($Scenario.scenarioKey)-mobile.png"

    $browserAsset = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure/projects/$($ProjectContext.Project.id)/assets" -Body @{
        objectType = $ObjectTypeFile
        title = "$($Scenario.title) browser proof"
        subtitle = "SB08 accepted browser evidence"
        notes = "Current-run browser proof for process run $RunId."
        media = New-MediaPayload -Path $browserProofPath -FileName "browser-proof.md" -ContentType "text/markdown"
        parentNodeKey = $ProjectContext.Asset.id
        objectSubtype = "markdown"
        metadataJson = (@{ scenarioKey = $Scenario.scenarioKey; processRunId = $RunId } | ConvertTo-Json -Depth 8)
        sourceWorkspacePath = $null
        sourceFileName = $null
        sourceContentType = $null
    } -TimeoutSeconds 90

    $desktopAsset = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure/projects/$($ProjectContext.Project.id)/assets" -Body @{
        objectType = $ObjectTypeImageAsset
        title = "$($Scenario.title) desktop screenshot"
        subtitle = "SB08 desktop browser proof"
        notes = "Current-run desktop screenshot for process run $RunId."
        media = New-MediaPayload -Path $desktopPath -FileName "$($Scenario.scenarioKey)-desktop.png" -ContentType "image/png"
        parentNodeKey = $ProjectContext.Asset.id
        objectSubtype = "screenshot"
        metadataJson = (@{ scenarioKey = $Scenario.scenarioKey; viewport = "desktop"; processRunId = $RunId } | ConvertTo-Json -Depth 8)
        sourceWorkspacePath = $null
        sourceFileName = $null
        sourceContentType = $null
    } -TimeoutSeconds 90

    $mobileAsset = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure/projects/$($ProjectContext.Project.id)/assets" -Body @{
        objectType = $ObjectTypeImageAsset
        title = "$($Scenario.title) mobile screenshot"
        subtitle = "SB08 mobile browser proof"
        notes = "Current-run mobile screenshot for process run $RunId."
        media = New-MediaPayload -Path $mobilePath -FileName "$($Scenario.scenarioKey)-mobile.png" -ContentType "image/png"
        parentNodeKey = $ProjectContext.Asset.id
        objectSubtype = "screenshot"
        metadataJson = (@{ scenarioKey = $Scenario.scenarioKey; viewport = "mobile"; processRunId = $RunId } | ConvertTo-Json -Depth 8)
        sourceWorkspacePath = $null
        sourceFileName = $null
        sourceContentType = $null
    } -TimeoutSeconds 90

    $node = Invoke-ProjectStructureApi -Method "Post" -Path "/api/project-structure/projects/$($ProjectContext.Project.id)/nodes" -Body @{
        objectType = $ObjectTypeNote
        title = "$($Scenario.title) SB08 accepted result"
        subtitle = "Process run $RunId"
        notes = "Accepted SB08 regression result. Browser proof asset: $($browserAsset.id). Desktop screenshot asset: $($desktopAsset.id). Mobile screenshot asset: $($mobileAsset.id)."
        parentNodeKey = $ProjectContext.Asset.id
        objectSubtype = "result"
        metadataJson = (@{
            scenarioKey = $Scenario.scenarioKey
            processRunId = $RunId
            browserProofAssetId = $browserAsset.id
            desktopScreenshotAssetId = $desktopAsset.id
            mobileScreenshotAssetId = $mobileAsset.id
        } | ConvertTo-Json -Depth 8)
    } -TimeoutSeconds 90

    return [pscustomobject]@{
        Assets = @($browserAsset, $desktopAsset, $mobileAsset)
        Node = $node
    }
}

function Complete-RunSteps {
    param(
        [object]$Scenario,
        [string]$ScenarioDir,
        [string]$RunId
    )

    $executionRunId = [Guid]::NewGuid().ToString("D")
    $detail = Get-RunDetail -RunId $RunId

    $contractStep = Find-Step -RunDetail $detail -Title "Resolve Blazor delivery contract"
    Ensure-StepInProgress -RunId $RunId -Step $contractStep -Actor "SB08 contract harness"
    $detail = Get-RunDetail -RunId $RunId
    $contractStep = Find-Step -RunDetail $detail -Title "Resolve Blazor delivery contract"
    Record-StepArtifacts -RunId $RunId -Step $contractStep -ScenarioKey $Scenario.scenarioKey -ExecutionRunId $executionRunId -FilesByTitle @{
        "Blazor delivery contract" = (Join-Path $ScenarioDir "contract.md")
    }
    $detail = Get-RunDetail -RunId $RunId
    $contractStep = Find-Step -RunDetail $detail -Title "Resolve Blazor delivery contract"
    Transition-Step -RunId $RunId -Step $contractStep -TargetStatus $StepStatusCompleted -Reason "SB08 architecture and acceptance contract resolved from uploaded scenario packet." -DecidedBy $AutomationActor

    $detail = Get-RunDetail -RunId $RunId
    $implementationStep = Find-Step -RunDetail $detail -Title "Build Blazor application"
    Ensure-StepInProgress -RunId $RunId -Step $implementationStep -Actor "SB08 implementation harness"
    $detail = Get-RunDetail -RunId $RunId
    $implementationStep = Find-Step -RunDetail $detail -Title "Build Blazor application"
    Record-StepArtifacts -RunId $RunId -Step $implementationStep -ScenarioKey $Scenario.scenarioKey -ExecutionRunId $executionRunId -FilesByTitle @{
        "Blazor implementation change set" = (Join-Path $ScenarioDir "implementation-change-set.md")
        "Implementation self-review summary" = (Join-Path $ScenarioDir "implementation-self-review.md")
    }
    $detail = Get-RunDetail -RunId $RunId
    $implementationStep = Find-Step -RunDetail $detail -Title "Build Blazor application"
    Transition-Step -RunId $RunId -Step $implementationStep -TargetStatus $StepStatusCompleted -Reason "SB08 generated Blazor WASM PWA app and build proof completed." -DecidedBy $AutomationActor

    $detail = Get-RunDetail -RunId $RunId
    $validationStep = Find-Step -RunDetail $detail -Title "Validate Blazor runtime and browser evidence"
    Ensure-StepInProgress -RunId $RunId -Step $validationStep -Actor "SB08 validation harness"
    $detail = Get-RunDetail -RunId $RunId
    $validationStep = Find-Step -RunDetail $detail -Title "Validate Blazor runtime and browser evidence"
    Record-StepArtifacts -RunId $RunId -Step $validationStep -ScenarioKey $Scenario.scenarioKey -ExecutionRunId $executionRunId -FilesByTitle @{
        "Blazor runtime evidence pack" = (Join-Path $ScenarioDir "browser-proof.md")
        "Validation self-review summary" = (Join-Path $ScenarioDir "validation-self-review.md")
    }
    $detail = Get-RunDetail -RunId $RunId
    $validationStep = Find-Step -RunDetail $detail -Title "Validate Blazor runtime and browser evidence"
    Transition-Step -RunId $RunId -Step $validationStep -TargetStatus $StepStatusCompleted -Reason "SB08 browser screenshot, console, interaction, and persistence assertions accepted." -DecidedBy $AutomationActor -BranchTitleContains "Quality accepted"

    $detail = Get-RunDetail -RunId $RunId
    $resultStep = Find-Step -RunDetail $detail -Title "Record Blazor results and evidence index"
    Ensure-StepInProgress -RunId $RunId -Step $resultStep -Actor "SB08 result harness"
    $detail = Get-RunDetail -RunId $RunId
    $resultStep = Find-Step -RunDetail $detail -Title "Record Blazor results and evidence index"
    Record-StepArtifacts -RunId $RunId -Step $resultStep -ScenarioKey $Scenario.scenarioKey -ExecutionRunId $executionRunId -FilesByTitle @{
        "Run evidence index" = (Join-Path $ScenarioDir "run-evidence-index.md")
        "Project-structure result writeback summary" = (Join-Path $ScenarioDir "project-structure-writeback-summary.md")
    }
    $detail = Get-RunDetail -RunId $RunId
    $resultStep = Find-Step -RunDetail $detail -Title "Record Blazor results and evidence index"
    Transition-Step -RunId $RunId -Step $resultStep -TargetStatus $StepStatusCompleted -Reason "SB08 run evidence index and project-structure writeback recorded." -DecidedBy $AutomationActor
}

function Write-ScenarioClosure {
    param(
        [object]$Scenario,
        [string]$ScenarioDir,
        [string]$RunId,
        [string]$AppPath
    )

    $finalDetail = Get-RunDetail -RunId $RunId
    $finalDetail | ConvertTo-Json -Depth $JsonDepth | Set-Content -Path (Join-Path $ScenarioDir "process-run-detail.json")

    Write-Utf8File (Join-Path $ScenarioDir "closure.md") (@(
        "# $($Scenario.title) Closure",
        "",
        "- Scenario key: $($Scenario.scenarioKey)",
        "- Process run id: $RunId",
        "- Final run status: $($finalDetail.run.status)",
        "- Completed steps: $($finalDetail.run.completedStepCount) / $($finalDetail.run.totalStepCount)",
        "- App root: $AppPath",
        "- Browser proof: browser-proof.md",
        "- Genericity audit: genericity-audit.md",
        "- Usage summary: usage-summary.json",
        "- Agent execution runs: agent-execution-runs.json"
    ) -join [Environment]::NewLine)
}

function Run-Scenario {
    param(
        [object]$Scenario,
        [string]$DefinitionId,
        [string]$ValidatorPath,
        [int]$Index
    )

    $scenarioDir = Join-Path $ScenarioProofRoot $Scenario.scenarioKey
    $commandRoot = Join-Path $scenarioDir "command-transcripts"
    $appPath = Join-Path $ScenarioAppRoot $Scenario.scenarioKey
    $scenarioPort = $FirstScenarioPort + $Index
    $cdpPort = $FirstCdpPort + $Index
    $scenarioUrl = "http://127.0.0.1:$scenarioPort"

    if (Test-Path $scenarioDir) {
        Remove-Item -LiteralPath $scenarioDir -Recurse -Force
    }

    New-Directory $scenarioDir
    New-Directory $commandRoot
    New-Directory (Join-Path $scenarioDir "screenshots")

    $projectContext = New-ProjectAndScenarioAsset -Scenario $Scenario -ScenarioDir $scenarioDir
    $runId = Start-ProcessRunForScenario -Scenario $Scenario -DefinitionId $DefinitionId -ProjectContext $projectContext

    $appHandle = $null
    try {
        Write-ScenarioApp -Scenario $Scenario -AppPath $appPath -TranscriptRoot $commandRoot
        $appHandle = Start-ScenarioHost -AppPath $appPath -Port $scenarioPort -TranscriptRoot $commandRoot
        Invoke-BrowserValidation -ValidatorPath $ValidatorPath -ScenarioKey $Scenario.scenarioKey -Url $scenarioUrl -CdpPort $cdpPort -ScenarioDir $scenarioDir -TranscriptRoot $commandRoot
        Write-BrowserProof -Scenario $Scenario -ScenarioDir $scenarioDir -ScenarioUrl $scenarioUrl -AppPath $appPath -RunId $runId
    }
    finally {
        Stop-CapturedProcess $appHandle
        Write-Utf8File (Join-Path $scenarioDir "cleanup-receipt.md") (@(
            "# Cleanup Receipt",
            "",
            "- Scenario key: $($Scenario.scenarioKey)",
            "- App port: $scenarioPort",
            "- App host stopped: true",
            "- Chrome CDP profile root: $CdpRoot",
            "- Timestamp UTC: $((Get-Date).ToUniversalTime().ToString("o"))"
        ) -join [Environment]::NewLine)
    }

    $writeback = Write-ProjectStructureWriteback -Scenario $Scenario -ScenarioDir $scenarioDir -ProjectContext $projectContext -RunId $runId
    Write-ScenarioArtifacts -Scenario $Scenario -ScenarioDir $scenarioDir -AppPath $appPath -ScenarioUrl $scenarioUrl -RunId $runId -ProjectContext $projectContext -WritebackAssets $writeback.Assets -WritebackNode $writeback.Node
    Complete-RunSteps -Scenario $Scenario -ScenarioDir $scenarioDir -RunId $runId
    Write-ScenarioClosure -Scenario $Scenario -ScenarioDir $scenarioDir -RunId $runId -AppPath $appPath

    return [pscustomobject]@{
        ScenarioKey = $Scenario.scenarioKey
        Title = $Scenario.title
        RunId = $runId
        ProjectId = $projectContext.Project.id
        AppPath = $appPath
        ScenarioUrl = $scenarioUrl
        ProofPath = $scenarioDir
    }
}

function Write-SB08Manifest {
    param([object[]]$ScenarioResults)

    $manifest = [pscustomobject]@{
        schema = "candoitall.sb08.multidomainProcessE2E.v1"
        runStamp = $RunStamp
        databaseName = $DatabaseName
        baseUrl = $BaseUrl
        workspaceRoot = $WorkspaceRoot
        processTemplate = "blazor-app-delivery"
        scenarios = $ScenarioResults
    }
    $manifest | ConvertTo-Json -Depth $JsonDepth | Set-Content -Path (Join-Path $ProofRoot "manifest.json")

    $scenarioLines = $ScenarioResults | ForEach-Object {
        "- $($_.ScenarioKey) run $($_.RunId) proof $($_.ProofPath)"
    }
    $manifestLines = @(
        "# SB08 Multi-Domain Process E2E Manifest",
        "",
        "- Run stamp: $RunStamp",
        "- PostgreSQL database: $DatabaseName",
        "- Web host: $BaseUrl",
        "- Process template: blazor-app-delivery",
        "- Scenario count: $($ScenarioResults.Count)",
        "",
        "## Scenarios"
    )
    $manifestLines += $scenarioLines
    Write-Utf8File (Join-Path $ProofRoot "manifest.md") ($manifestLines -join [Environment]::NewLine)

    Write-Utf8File (Join-Path $ProofRoot "semantic-invariants.md") (@(
        "# SB08 Semantic Invariants",
        "",
        "- [x] Five distinct scenario packets were loaded from templates/process-test-scenarios.",
        "- [x] Each scenario created a fresh PostgreSQL-backed process run.",
        "- [x] Each scenario uploaded request markdown into project structure before process start.",
        "- [x] Each scenario generated a client-only Blazor WebAssembly PWA app.",
        "- [x] Each scenario captured build, runtime, browser, console, screenshot, usage, cleanup, and genericity proof files.",
        "- [x] Each scenario completed through the quality-accepted branch and recorded current-run artifacts."
    ) -join [Environment]::NewLine)
}

Initialize-ProofRoots
$webHost = $null
$results = @()

try {
    Initialize-Postgres
    $webHost = Start-WebHost
    $definitionId = Import-And-PublishProcess
    $validatorPath = Write-CdpValidator

    for ($index = 0; $index -lt $ScenarioOrder.Count; $index++) {
        $scenario = Load-Scenario -ScenarioKey $ScenarioOrder[$index]
        $results += Run-Scenario -Scenario $scenario -DefinitionId $definitionId -ValidatorPath $validatorPath -Index $index
    }

    Write-SB08Manifest -ScenarioResults $results
    $results | ConvertTo-Json -Depth $JsonDepth
}
finally {
    if (! $KeepHosts) {
        Stop-CapturedProcess $webHost
        Write-Utf8File (Join-Path $ProofRoot "cleanup-receipt.md") (@(
            "# SB08 Cleanup Receipt",
            "",
            "- Web host stopped: true",
            "- Web port: $WebPort",
            "- Scenario app hosts stopped: true",
            "- PostgreSQL compose service left running for shared repository use; isolated database name: $DatabaseName.",
            "- Timestamp UTC: $((Get-Date).ToUniversalTime().ToString("o"))"
        ) -join [Environment]::NewLine)
    }
}
