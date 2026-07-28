param(
    [Parameter(Mandatory = $true)]
    [string]$DiscoveryDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

$excludedFiles = @(
    "direct-maf-package-references.txt",
    "dotnet-info.txt",
    "metadata.txt"
)

function Get-Classification {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepositoryPath
    )

    $normalizedPath = $RepositoryPath.Replace("/", "\")

    $classification = switch -Regex ($normalizedPath) {
        "^src\\" {
            [pscustomobject]@{
                Scope = "production"
                Liveness = "live"
                Usage = "implementation-reference"
            }
            break
        }
        "^tests\\" {
            [pscustomobject]@{
                Scope = "test"
                Liveness = "live-test"
                Usage = "test-reference"
            }
            break
        }
        "^(samples|examples)\\" {
            [pscustomobject]@{
                Scope = "sample"
                Liveness = "sample"
                Usage = "sample-reference"
            }
            break
        }
        "^(docs|codex)\\" {
            [pscustomobject]@{
                Scope = "documentation"
                Liveness = "documentation"
                Usage = "documentation-reference"
            }
            break
        }
        "^(tools|scripts)\\" {
            [pscustomobject]@{
                Scope = "tooling"
                Liveness = "live-tooling"
                Usage = "tooling-reference"
            }
            break
        }
        default {
            [pscustomobject]@{
                Scope = "repository-config"
                Liveness = "live-config"
                Usage = "build-or-config-reference"
            }
        }
    }

    return $classification
}

$rows = [System.Collections.Generic.List[object]]::new()

Get-ChildItem -LiteralPath $DiscoveryDirectory -Filter "*.txt" -File |
    Where-Object Name -NotIn $excludedFiles |
    Sort-Object Name |
    ForEach-Object {
        $sourceFile = $_.Name
        $pattern = $null

        foreach ($line in Get-Content -LiteralPath $_.FullName) {
            if ($line -match "^===== PATTERN: (?<pattern>.+) =====$") {
                $pattern = $Matches.pattern
                continue
            }

            if ([string]::IsNullOrWhiteSpace($line) -or $line -eq "<no matches>") {
                continue
            }

            if ($line -notmatch "^(?<path>.+?):(?<lineNumber>\d+):(?<text>.*)$") {
                throw "Unrecognized discovery line in ${sourceFile}: $line"
            }

            $classification = Get-Classification -RepositoryPath $Matches.path
            $rows.Add([pscustomobject]@{
                DiscoveryFile = $sourceFile
                Pattern = $pattern
                RepositoryPath = $Matches.path
                LineNumber = [int]$Matches.lineNumber
                Scope = $classification.Scope
                Liveness = $classification.Liveness
                Usage = $classification.Usage
                Match = $Matches.text.Trim()
            })
        }
    }

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$rows | Export-Csv -LiteralPath $OutputPath -NoTypeInformation -Encoding utf8

$summaryPath = [System.IO.Path]::ChangeExtension($OutputPath, ".summary.csv")
$rows |
    Group-Object DiscoveryFile, Scope, Usage |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            DiscoveryFile = $_.Group[0].DiscoveryFile
            Scope = $_.Group[0].Scope
            Usage = $_.Group[0].Usage
            MatchCount = $_.Count
        }
    } |
    Export-Csv -LiteralPath $summaryPath -NoTypeInformation -Encoding utf8

Write-Output "Classified $($rows.Count) discovery matches."
Write-Output "Detail: $OutputPath"
Write-Output "Summary: $summaryPath"
