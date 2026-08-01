[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RepoRoot = "",
    [string]$Configuration = "Release",
    [string]$InstallRoot = "",
    [string]$ShortcutPath = "",
    [string]$RuntimeIdentifier = "",
    [string]$BindHost = "127.0.0.1",
    [ValidateRange(1, 65535)]
    [int]$Port = 38473,
    [switch]$SkipDatabaseSetup,
    [switch]$StartAfterInstall
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$installerPath = Join-Path $PSScriptRoot "install\Install-CanDoItAllWebApp.ps1"
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "CanDoItAll web app installer was not found at '$installerPath'."
}

& $installerPath @PSBoundParameters
