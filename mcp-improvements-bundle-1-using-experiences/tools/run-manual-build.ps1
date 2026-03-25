param(
    [string]$ProjectPath = "C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
    [string]$LogPath = "C:\repositories\CanDoItAll\mcp-improvements-bundle-1-using-experiences\artifacts\manual-build-web.log"
)

if (Test-Path $LogPath) {
    Remove-Item $LogPath -Force
}

$start = Get-Date
dotnet build $ProjectPath --configuration Debug *> $LogPath
$elapsedMs = [math]::Round(((Get-Date) - $start).TotalMilliseconds)
$item = Get-Item $LogPath
$lines = (Get-Content $LogPath | Measure-Object -Line).Lines

[pscustomobject]@{
    ElapsedMs = $elapsedMs
    LogPath = $LogPath
    Bytes = $item.Length
    Lines = $lines
} | ConvertTo-Json -Compress
