[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$MainRepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$searchRoots = @("src", "tests", "Tailwind")
$patterns = @("material-icons", "material-icons.css")
$findings = [System.Collections.Generic.List[string]]::new()

foreach ($searchRoot in $searchRoots) {
    $root = Join-Path $MainRepoRoot $searchRoot
    if (-not (Test-Path -LiteralPath $root)) {
        continue
    }

    Get-ChildItem -LiteralPath $root -File -Recurse |
        Where-Object { $_.Extension -in @(".cs", ".razor", ".css", ".js", ".json", ".md") } |
        ForEach-Object {
            $lineNumber = 0
            foreach ($line in Get-Content -LiteralPath $_.FullName) {
                $lineNumber++
                foreach ($pattern in $patterns) {
                    if ($line.Contains($pattern, [StringComparison]::Ordinal)) {
                        $relative = [System.IO.Path]::GetRelativePath($MainRepoRoot, $_.FullName)
                        $findings.Add("${relative}:$lineNumber:$line")
                        break
                    }
                }
            }
        }
}

if ($findings.Count -gt 0) {
    $findings | ForEach-Object { Write-Host $_ }
    throw "Legacy Material Icons contracts remain under active source/test roots."
}

Write-Host "No legacy Material Icons contracts were found."
