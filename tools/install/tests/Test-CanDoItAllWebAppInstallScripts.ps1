[CmdletBinding()]
param(
    [string]$RepositoryRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-PowerShellFileParses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $Path,
        [ref]$tokens,
        [ref]$parseErrors) | Out-Null

    if ($parseErrors.Count -gt 0) {
        $details = ($parseErrors | ForEach-Object { $_.Message }) -join [Environment]::NewLine
        throw "PowerShell parsing failed for '$Path':$([Environment]::NewLine)$details"
    }
}

function Assert-PreviewCreatesNothing {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Operation,
        [Parameter(Mandatory = $true)]
        [string]$InstallRoot,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Assert-Condition `
        -Condition (-not (Test-Path -LiteralPath $InstallRoot)) `
        -Message "$Description preview root already exists: $InstallRoot"

    $result = & $Operation
    $preview = @($result) | Where-Object { [string]$_.Status -eq "Preview" } | Select-Object -Last 1
    Assert-Condition `
        -Condition ($null -ne $preview) `
        -Message "$Description did not return a Preview result."
    Assert-Condition `
        -Condition (-not (Test-Path -LiteralPath $InstallRoot)) `
        -Message "$Description -WhatIf created '$InstallRoot'."

    return $preview
}

function Assert-Throws {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Operation,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $didThrow = $false
    try {
        & $Operation | Out-Null
    }
    catch {
        $didThrow = $true
    }

    Assert-Condition -Condition $didThrow -Message "$Description did not reject the unsafe input."
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

$databaseInstallerPath = Join-Path $RepositoryRoot "tools\install\Install-CanDoItAllWebAppDatabase.ps1"
$webInstallerPath = Join-Path $RepositoryRoot "tools\install\Install-CanDoItAllWebApp.ps1"
$compatibilityInstallerPath = Join-Path $RepositoryRoot "tools\Install-CanDoItAllWebApp.ps1"

foreach ($path in @($databaseInstallerPath, $webInstallerPath, $compatibilityInstallerPath)) {
    Assert-Condition -Condition (Test-Path -LiteralPath $path -PathType Leaf) -Message "Required install script was not found: $path"
    Assert-PowerShellFileParses -Path $path
}

$databaseInstallerText = Get-Content -LiteralPath $databaseInstallerPath -Raw
$webInstallerText = Get-Content -LiteralPath $webInstallerPath -Raw

Assert-Condition `
    -Condition ($databaseInstallerText -match 'CmdletBinding\([^\)]*SupportsShouldProcess\s*=\s*\$true') `
    -Message "Database installer must support -WhatIf."
Assert-Condition `
    -Condition ($databaseInstallerText -notmatch '(?i)docker\s+compose') `
    -Message "Installed database setup must not use development Docker Compose."
Assert-Condition `
    -Condition ($databaseInstallerText.Contains("postgres:16.14-alpine")) `
    -Message "Database installer must pin the PostgreSQL 16.14 Docker image."
Assert-Condition `
    -Condition ($databaseInstallerText.Contains("sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777")) `
    -Message "Database installer must pin the PostgreSQL Docker image by immutable digest."
Assert-Condition `
    -Condition ($databaseInstallerText.Contains("8A7F54C1968D5D49BDCD3F66B1291F736C74B8CB6A26E9874771FCC7837DBF38")) `
    -Message "Database installer must verify the pinned EDB archive hash."
Assert-Condition `
    -Condition ($webInstallerText.Contains('$env:Database__Provider = "PostgreSql"')) `
    -Message "Installed launcher must set Database__Provider."
Assert-Condition `
    -Condition ($webInstallerText.Contains('$env:Database__ConnectionString')) `
    -Message "Installed launcher must set Database__ConnectionString."

Assert-Condition `
    -Condition ($webInstallerText.Contains('$hostName = [string]$manifest.host')) `
    -Message "Installed launcher must derive its database host from the managed manifest."
Assert-Condition `
    -Condition ($webInstallerText.Contains('$databasePort = [int]$manifest.port')) `
    -Message "Installed launcher must derive its database port from the managed manifest."
Assert-Condition `
    -Condition ($webInstallerText.Contains('$databaseName = [string]$manifest.databaseName')) `
    -Message "Installed launcher must derive its database name from the managed manifest."
Assert-Condition `
    -Condition ($webInstallerText.Contains('$username = [string]$manifest.appUsername')) `
    -Message "Installed launcher must derive its database username from the managed manifest."
Assert-Condition `
    -Condition ($webInstallerText.Contains('$appPasswordPath = Resolve-InstalledPath -Root $databaseRoot -PathValue ([string]$manifest.appPasswordFile)')) `
    -Message "Installed launcher must resolve its application-password path from the managed manifest."
Assert-Condition `
    -Condition ($webInstallerText.Contains('$secureValue = ConvertTo-SecureString -String $protectedValue')) `
    -Message "Installed launcher must decrypt the current-user protected application credential."
Assert-Condition `
    -Condition ($webInstallerText.Contains('$appPassword = Read-ProtectedSecret -Path $appPasswordPath')) `
    -Message "Installed launcher must read the protected application password."

$webInstallerTokens = $null
$webInstallerAstErrors = $null
$webInstallerAst = [System.Management.Automation.Language.Parser]::ParseFile(
    $webInstallerPath,
    [ref]$webInstallerTokens,
    [ref]$webInstallerAstErrors)
if ($webInstallerAstErrors.Count -gt 0) {
    $details = ($webInstallerAstErrors | ForEach-Object { $_.Message }) -join [Environment]::NewLine
    throw "Could not load production launcher functions from '$webInstallerPath':$([Environment]::NewLine)$details"
}

foreach ($functionName in @(
        "ConvertTo-PowerShellSingleQuotedLiteral",
        "Get-LauncherScriptContent",
        "Format-ShortcutArguments",
        "Invoke-LauncherWithMutexHandoff")) {
    $definitions = @($webInstallerAst.FindAll({
        param($node)

        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
            [string]$node.Name -eq $functionName
    }, $true))
    Assert-Condition `
        -Condition ($definitions.Count -eq 1) `
        -Message "Expected exactly one production '$functionName' definition, but found $($definitions.Count)."
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}

$launcherCases = @(
    [pscustomobject]@{
        Description = "IPv4"
        BindHost = "127.0.0.1"
        BindUrlHost = "127.0.0.1"
        ExpectedBindUrl = "http://127.0.0.1:38473"
        ExpectedLaunchUrl = "http://127.0.0.1:38473"
        ExpectedListenerAddress = "127.0.0.1"
        ExpectedDesktopLaunch = "true"
    },
    [pscustomobject]@{
        Description = "IPv6"
        BindHost = "::1"
        BindUrlHost = "[::1]"
        ExpectedBindUrl = "http://[::1]:38473"
        ExpectedLaunchUrl = "http://[::1]:38473"
        ExpectedListenerAddress = "::1"
        ExpectedDesktopLaunch = "true"
    },
    [pscustomobject]@{
        Description = "IPv4 wildcard"
        BindHost = "0.0.0.0"
        BindUrlHost = "0.0.0.0"
        ExpectedBindUrl = "http://0.0.0.0:38473"
        ExpectedLaunchUrl = "http://127.0.0.1:38473"
        ExpectedListenerAddress = "0.0.0.0"
        ExpectedDesktopLaunch = "false"
    },
    [pscustomobject]@{
        Description = "IPv6 wildcard"
        BindHost = "::"
        BindUrlHost = "[::]"
        ExpectedBindUrl = "http://[::]:38473"
        ExpectedLaunchUrl = "http://[::1]:38473"
        ExpectedListenerAddress = "::"
        ExpectedDesktopLaunch = "false"
    },
    [pscustomobject]@{
        Description = "localhost"
        BindHost = "localhost"
        BindUrlHost = "localhost"
        ExpectedBindUrl = "http://localhost:38473"
        ExpectedLaunchUrl = "http://127.0.0.1:38473"
        ExpectedListenerAddress = "127.0.0.1"
        ExpectedDesktopLaunch = "true"
    }
)

foreach ($launcherCase in $launcherCases) {
    $launcherText = Get-LauncherScriptContent `
        -BindHost $launcherCase.BindHost `
        -BindUrlHost $launcherCase.BindUrlHost `
        -Port 38473
    Assert-Condition `
        -Condition ($launcherText -cnotmatch '__[A-Z0-9_]+__') `
        -Message "Generated $($launcherCase.Description) launcher left an unresolved template placeholder."
    Assert-Condition `
        -Condition ($launcherText.Contains("`$bindUrl = '$($launcherCase.ExpectedBindUrl)'")) `
        -Message "Generated $($launcherCase.Description) launcher did not set the exact bind URL."
    Assert-Condition `
        -Condition ($launcherText.Contains("`$launchUrl = '$($launcherCase.ExpectedLaunchUrl)'")) `
        -Message "Generated $($launcherCase.Description) launcher did not set the exact launch URL."
    Assert-Condition `
        -Condition ($launcherText.Contains("`$expectedListenerAddress = '$($launcherCase.ExpectedListenerAddress)'")) `
        -Message "Generated $($launcherCase.Description) launcher did not set the exact listener address."
    Assert-Condition `
        -Condition ($launcherText.Contains('$appPort = 38473')) `
        -Message "Generated $($launcherCase.Description) launcher did not set the exact application port."
    $expectedDesktopLaunch = "`$env:FileTools__DesktopLaunch__Enabled = `"$($launcherCase.ExpectedDesktopLaunch)`""
    Assert-Condition `
        -Condition ($launcherText.Contains($expectedDesktopLaunch)) `
        -Message "Generated $($launcherCase.Description) launcher did not set the expected desktop-launch flag."
    Assert-Condition `
        -Condition ($launcherText.Contains('$healthUrl = "$launchUrl/health"')) `
        -Message "Generated $($launcherCase.Description) launcher must probe its exact launch URL."
    Assert-Condition `
        -Condition ($launcherText.Contains('$env:ASPNETCORE_URLS = $bindUrl')) `
        -Message "Generated $($launcherCase.Description) launcher must bind with its configured bind URL."

    foreach ($connectionSnippet in @(
        '"Host=$(Format-ConnectionStringValue -Value $hostName)"',
        '"Port=$databasePort"',
        '"Database=$(Format-ConnectionStringValue -Value $databaseName)"',
        '"Username=$(Format-ConnectionStringValue -Value $username)"',
        '"Password=$(Format-ConnectionStringValue -Value $appPassword)"')) {
        Assert-Condition `
            -Condition ($launcherText.Contains($connectionSnippet)) `
            -Message "Generated $($launcherCase.Description) launcher is missing manifest-driven connection snippet '$connectionSnippet'."
    }

    $launcherTokens = $null
    $launcherErrors = $null
    [System.Management.Automation.Language.Parser]::ParseInput(
        $launcherText,
        [ref]$launcherTokens,
        [ref]$launcherErrors) | Out-Null
    if ($launcherErrors.Count -gt 0) {
        $details = ($launcherErrors | ForEach-Object { $_.Message }) -join [Environment]::NewLine
        throw "Generated $($launcherCase.Description) launcher parsing failed:$([Environment]::NewLine)$details"
    }
}

$handoffTestRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("cia-h-" + [System.IO.Path]::GetRandomFileName())
$handoffLauncherPath = Join-Path $handoffTestRoot "Fake-Launcher.ps1"
$handoffMutex = $null
$handoffMutexAcquired = $false
$handoffMigrationMayHaveStarted = $false
try {
    New-Item -ItemType Directory -Path $handoffTestRoot | Out-Null
    @'
param(
    [switch]$InstallerHandoff,
    [string]$InstallerHandoffEvent = "",
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
$mutex = New-Object System.Threading.Mutex($false, "Global\CanDoItAll.WebApp.Install.v1")
$readyEvent = $null
$acquired = $false
try {
    if (-not $InstallerHandoff.IsPresent) {
        throw "Expected installer handoff mode."
    }
    $readyEvent = [System.Threading.EventWaitHandle]::OpenExisting($InstallerHandoffEvent)
    try {
        $acquired = [System.Threading.WaitHandle]::SignalAndWait($readyEvent, $mutex, 30000, $false)
    }
    catch [System.Threading.AbandonedMutexException] {
        $acquired = $true
    }
    if (-not $acquired) {
        throw "Timed out accepting the installer mutex."
    }
}
catch {
    $_ | Out-String | Set-Content -LiteralPath (Join-Path $PSScriptRoot "handoff-error.txt") -Encoding UTF8
    throw
}
finally {
    if ($acquired) {
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
    if ($null -ne $readyEvent) {
        $readyEvent.Dispose()
    }
}
'@ | Set-Content -LiteralPath $handoffLauncherPath -Encoding UTF8

    $handoffMutex = New-Object System.Threading.Mutex($false, "Global\CanDoItAll.WebApp.Install.v1")
    $handoffMutexAcquired = $handoffMutex.WaitOne(0)
    Assert-Condition `
        -Condition $handoffMutexAcquired `
        -Message "Could not acquire the installer mutex for the launcher handoff test."

    try {
        Invoke-LauncherWithMutexHandoff `
            -PowerShellPath ([string](Get-Command powershell -CommandType Application).Source) `
            -LauncherPath $handoffLauncherPath `
            -WorkingDirectory $handoffTestRoot `
            -InstallMutex $handoffMutex `
            -MutexAcquired ([ref]$handoffMutexAcquired) `
            -MigrationMayHaveStarted ([ref]$handoffMigrationMayHaveStarted) `
            -NoBrowser
    }
    catch {
        $handoffErrorPath = Join-Path $handoffTestRoot "handoff-error.txt"
        $handoffDetails = if (Test-Path -LiteralPath $handoffErrorPath -PathType Leaf) {
            Get-Content -LiteralPath $handoffErrorPath -Raw
        }
        else {
            "The child did not write diagnostic output."
        }
        throw "$($_.Exception.Message)$([Environment]::NewLine)$handoffDetails"
    }

    Assert-Condition `
        -Condition (-not $handoffMutexAcquired -and $handoffMigrationMayHaveStarted) `
        -Message "Launcher handoff did not atomically transfer the installer mutex."
}
finally {
    if ($handoffMutexAcquired -and $null -ne $handoffMutex) {
        $handoffMutex.ReleaseMutex()
    }
    if ($null -ne $handoffMutex) {
        $handoffMutex.Dispose()
    }
    if (Test-Path -LiteralPath $handoffTestRoot -PathType Container) {
        [System.IO.Directory]::Delete($handoffTestRoot, $true)
    }
}

$previewRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("cia-p-" + [System.IO.Path]::GetRandomFileName())
$databasePreviewRoot = Join-Path $previewRoot "database"
$webPreviewRoot = Join-Path $previewRoot "web"
$ipv6PreviewRoot = Join-Path $previewRoot "web-ipv6"
$compatibilityPreviewRoot = Join-Path $previewRoot "compatibility"

$databasePreview = Assert-PreviewCreatesNothing `
    -Description "Database installer" `
    -InstallRoot $databasePreviewRoot `
    -Operation { & $databaseInstallerPath -InstallRoot $databasePreviewRoot -WhatIf }
Assert-Condition `
    -Condition ([string]$databasePreview.Host -eq "127.0.0.1" -and
        [int]$databasePreview.Port -eq 55432 -and
        [string]$databasePreview.DatabaseName -eq "candoitall" -and
        [string]$databasePreview.AppUsername -eq "candoitall_app") `
    -Message "Database installer preview did not expose the default installed-app connection contract."

$null = Assert-PreviewCreatesNothing `
    -Description "Web installer" `
    -InstallRoot $webPreviewRoot `
    -Operation {
        & $webInstallerPath `
            -RepoRoot $RepositoryRoot `
            -InstallRoot $webPreviewRoot `
            -ShortcutPath (Join-Path $webPreviewRoot "CanDoItAll.lnk") `
            -WhatIf
    }

$null = Assert-PreviewCreatesNothing `
    -Description "Compatibility web installer" `
    -InstallRoot $compatibilityPreviewRoot `
    -Operation {
        & $compatibilityInstallerPath `
            -RepoRoot $RepositoryRoot `
            -InstallRoot $compatibilityPreviewRoot `
            -ShortcutPath (Join-Path $compatibilityPreviewRoot "CanDoItAll.lnk") `
            -WhatIf
    }

$ipv6Result = & $webInstallerPath `
    -RepoRoot $RepositoryRoot `
    -InstallRoot $ipv6PreviewRoot `
    -ShortcutPath (Join-Path $ipv6PreviewRoot "CanDoItAll.lnk") `
    -BindHost "::1" `
    -WhatIf
$ipv6Preview = @($ipv6Result) | Where-Object { [string]$_.Status -eq "Preview" } | Select-Object -Last 1
Assert-Condition `
    -Condition ($null -ne $ipv6Preview -and [string]$ipv6Preview.LaunchUrl -eq "http://[::1]:38473") `
    -Message "Web installer did not format the IPv6 loopback launch URL correctly."
Assert-Condition `
    -Condition (-not (Test-Path -LiteralPath $ipv6PreviewRoot)) `
    -Message "IPv6 web installer preview created '$ipv6PreviewRoot'."

$pendingStateTestRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("cia-s-" + [System.IO.Path]::GetRandomFileName())
$pendingStateDatabaseRoot = Join-Path $pendingStateTestRoot "runtime\database"
$pendingStatePath = Join-Path $pendingStateDatabaseRoot "database-engine.pending"
try {
    New-Item -ItemType Directory -Path $pendingStateDatabaseRoot -Force | Out-Null
    [pscustomobject]@{
        schemaVersion = 1
        engine = "native"
        port = 55439
        databaseName = "pending_database"
        appUsername = "pending_app"
    } | ConvertTo-Json | Set-Content -LiteralPath $pendingStatePath -Encoding UTF8

    $pendingPreviewOutput = & $databaseInstallerPath -InstallRoot $pendingStateTestRoot -WhatIf
    $pendingPreview = @($pendingPreviewOutput) |
        Where-Object { [string]$_.Status -eq "Preview" } |
        Select-Object -Last 1
    Assert-Condition `
        -Condition ($null -ne $pendingPreview -and
            [string]$pendingPreview.Engine -eq "native" -and
            [int]$pendingPreview.Port -eq 55439 -and
            [string]$pendingPreview.DatabaseName -eq "pending_database" -and
            [string]$pendingPreview.AppUsername -eq "pending_app") `
        -Message "Interrupted database setup did not preserve its custom connection contract."

    Assert-Throws `
        -Description "Conflicting interrupted database port" `
        -Operation {
            & $databaseInstallerPath `
                -InstallRoot $pendingStateTestRoot `
                -Port 55440 `
                -WhatIf
        }
}
finally {
    if (Test-Path -LiteralPath $pendingStateTestRoot -PathType Container) {
        [System.IO.Directory]::Delete($pendingStateTestRoot, $true)
    }
}

Assert-Throws `
    -Description "Repository-overlapping InstallRoot" `
    -Operation {
        & $webInstallerPath `
            -RepoRoot $RepositoryRoot `
            -InstallRoot (Join-Path $RepositoryRoot "src") `
            -ShortcutPath (Join-Path $previewRoot "unsafe-root.lnk") `
            -WhatIf
    }

Assert-Throws `
    -Description "PowerShell source-like BindHost" `
    -Operation {
        & $webInstallerPath `
            -RepoRoot $RepositoryRoot `
            -InstallRoot (Join-Path $previewRoot "unsafe-bind") `
            -ShortcutPath (Join-Path $previewRoot "unsafe-bind.lnk") `
            -BindHost '127.0.0.1"; Write-Host injected; #' `
            -WhatIf
    }

$junctionTestRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("cia-j-" + [System.IO.Path]::GetRandomFileName())
$junctionInstallRoot = Join-Path $junctionTestRoot "linked-install"
$junctionRepoRoot = Join-Path $junctionTestRoot "linked-repository"
try {
    New-Item -ItemType Directory -Path $junctionTestRoot | Out-Null
    New-Item `
        -ItemType Junction `
        -Path $junctionInstallRoot `
        -Target (Join-Path $RepositoryRoot "src") | Out-Null
    New-Item `
        -ItemType Junction `
        -Path $junctionRepoRoot `
        -Target $RepositoryRoot | Out-Null

    Assert-Throws `
        -Description "Database installer Junction InstallRoot" `
        -Operation {
            & $databaseInstallerPath `
                -InstallRoot $junctionInstallRoot `
                -WhatIf
        }

    Assert-Throws `
        -Description "Junction InstallRoot" `
        -Operation {
            & $webInstallerPath `
                -RepoRoot $RepositoryRoot `
                -InstallRoot $junctionInstallRoot `
                -ShortcutPath (Join-Path $previewRoot "unsafe-junction.lnk") `
                -WhatIf
        }

    Assert-Throws `
        -Description "Junction RepoRoot" `
        -Operation {
            & $webInstallerPath `
                -RepoRoot $junctionRepoRoot `
                -InstallRoot (Join-Path $junctionTestRoot "safe-install") `
                -ShortcutPath (Join-Path $previewRoot "unsafe-repo-junction.lnk") `
                -WhatIf
        }
}
finally {
    foreach ($junctionPath in @($junctionInstallRoot, $junctionRepoRoot)) {
        $junctionItem = Get-Item -LiteralPath $junctionPath -Force -ErrorAction SilentlyContinue
        if ($null -ne $junctionItem) {
            $junctionItem.Delete()
        }
    }
    if (Test-Path -LiteralPath $junctionTestRoot -PathType Container) {
        [System.IO.Directory]::Delete($junctionTestRoot)
    }
}

Assert-Condition `
    -Condition (-not (Test-Path -LiteralPath $previewRoot)) `
    -Message "Install previews created their shared preview root."

[pscustomobject]@{
    Status = "Passed"
    PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    DatabaseInstaller = $databaseInstallerPath
    WebInstaller = $webInstallerPath
    GeneratedLauncherParsed = $true
    WhatIfWasNonMutating = $true
}
