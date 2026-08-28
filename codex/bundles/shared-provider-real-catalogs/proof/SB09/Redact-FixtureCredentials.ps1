param()
$ErrorActionPreference = 'Stop'
$taskPattern = 'sk-[A-Za-z0-9_-]{12,}|eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+'
$taskRows = foreach ($taskFile in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'transcripts') -File) {
    $taskContent = [IO.File]::ReadAllText($taskFile.FullName)
    $taskMatches = [regex]::Matches($taskContent, $taskPattern).Count
    if ($taskMatches -eq 0) {
        continue
    }
    $taskBefore = (Get-FileHash -LiteralPath $taskFile.FullName).Hash
    [IO.File]::WriteAllText($taskFile.FullName, [regex]::Replace($taskContent, $taskPattern, 'REDACTED_FIXTURE_CREDENTIAL'))
    [pscustomobject]@{File=$taskFile.Name; Matches=$taskMatches; BeforeSha256=$taskBefore; AfterSha256=(Get-FileHash -LiteralPath $taskFile.FullName).Hash}
}
if (@($taskRows).Count -gt 0) {
    $taskRows | Export-Csv -NoTypeInformation -Append (Join-Path $PSScriptRoot 'redaction.csv')
}
Write-Output ('Mechanically redacted fixture credential-shaped values in ' + @($taskRows).Count + ' files. No values printed.')
