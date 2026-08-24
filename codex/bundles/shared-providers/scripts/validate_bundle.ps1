param(
    [Parameter(Position = 0)]
    [string]$BundlePath = (Resolve-Path (Join-Path $PSScriptRoot ".."))
)

$ErrorActionPreference = "Stop"
python (Join-Path $PSScriptRoot "validate_bundle.py") $BundlePath
exit $LASTEXITCODE
