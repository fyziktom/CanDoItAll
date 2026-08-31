$ErrorActionPreference = 'Stop'
$repo = 'C:\repositories\CanDoItAll'
$proof = Join-Path $repo 'codex\bundles\agent-startup-performance\proof\SB03\ui'
$outFile = Join-Path $proof 'file-fixtures-final.json'
if (Test-Path -LiteralPath $outFile) { throw 'Immutable final fixture proof exists.' }
$before = Get-Content -LiteralPath (Join-Path $proof 'file-fixtures-preflight.json') -Raw | ConvertFrom-Json
$referenceFile = Join-Path $proof 'reference-facts.json'
$freezeFile = Join-Path $repo 'codex\bundles\agent-startup-performance\proof\frozen-integration\source-freeze.json'
if ((Get-FileHash -LiteralPath $referenceFile -Algorithm SHA256).Hash -ne $before.ReferenceFactsSha256 -or (Get-FileHash -LiteralPath $freezeFile -Algorithm SHA256).Hash -ne $before.SourceFreezeSha256) { throw 'Reference or frozen source authority changed.' }
$roots = @{native='C:\Users\lucys\AppData\Local\CanDoItAll\workspace\runtime-overrides\ff24611dad478ec960349d9ad11d1017';client='C:\repositories\CanDoItAll\.artifacts\shared-providers-e2e\client-a\data\workspace'}
$assets = @()
foreach ($asset in $before.Assets) {
    $path = Join-Path $roots[$asset.Host] $asset.WorkspaceRelativePath
    $item = Get-Item -LiteralPath $path
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Approved source became a reparse point.' }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($hash -ne $asset.Sha256 -or $item.Length -ne $asset.Bytes) { throw 'Approved original asset changed.' }
    $assets += [ordered]@{Host=$asset.Host;WorkspaceRelativePath=$asset.WorkspaceRelativePath;Sha256=$hash;Bytes=$item.Length;MatchesReference=$true}
}
$sources = @()
foreach ($source in $before.Sources) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $repo $source.Path) -Algorithm SHA256).Hash
    if ($hash -ne $source.Sha256) { throw 'Frozen source changed.' }
    $sources += [ordered]@{Path=$source.Path;Sha256=$hash;MatchesFreeze=$true}
}
if ($assets.Count -ne 4 -or $sources.Count -ne 13) { throw 'Unexpected proof inventory.' }
$app = Get-CimInstance Win32_Process -Filter 'ProcessId = 60000'
$parent = Get-CimInstance Win32_Process -Filter 'ProcessId = 47476'
$wrapper = Get-CimInstance Win32_Process -Filter 'ProcessId = 62088'
if (!$app -or !$parent -or !$wrapper -or $app.ParentProcessId -ne 47476 -or $parent.ParentProcessId -ne 62088 -or $app.ExecutablePath -ne $before.Native.AppPath -or [DateTimeOffset](Get-Process -Id 60000).StartTime -ne [DateTimeOffset]$before.Native.AppStartedUtc) { throw 'Native candidate identity changed.' }
$dll = [IO.Path]::ChangeExtension($app.ExecutablePath, '.dll')
$dllHash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
if ($dllHash -ne $before.Native.DllSha256) { throw 'Native candidate DLL changed.' }
$bindings = @(Get-NetTCPConnection -State Listen -LocalPort 5032 | Select-Object LocalAddress,LocalPort,OwningProcess)
if ($bindings.Count -ne 2 -or @($bindings | Where-Object { $_.OwningProcess -ne 60000 -or $_.LocalAddress -notin @('127.0.0.1','::1') }).Count -ne 0) { throw 'Native listener identity changed.' }
$format = '{"Id":{{json .Id}},"Image":{{json .Image}},"StartedAt":{{json .State.StartedAt}},"Running":{{json .State.Running}},"Health":{{json .State.Health.Status}},"ReadonlyRootfs":{{json .HostConfig.ReadonlyRootfs}},"User":{{json .Config.User}}}'
$client = (& docker inspect --format $format $before.Client.ContainerId | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0 -or $client.Id -ne $before.Client.ContainerId -or $client.Image -ne $before.Client.ImageId -or [DateTimeOffset]$client.StartedAt -ne [DateTimeOffset]$before.Client.StartedAtUtc -or !$client.Running -or $client.Health -ne 'healthy' -or !$client.ReadonlyRootfs -or $client.User -ne '1654:1654') { throw 'Client candidate identity/security/health changed.' }
$publisher = (& docker inspect --format $format $before.Publisher.ContainerId | ConvertFrom-Json)
if ($LASTEXITCODE -ne 0 -or $publisher.Id -ne $before.Publisher.ContainerId -or $publisher.Image -ne $before.Publisher.ImageId -or [DateTimeOffset]$publisher.StartedAt -ne [DateTimeOffset]$before.Publisher.StartedAtUtc -or !$publisher.Running) { throw 'Publisher identity changed.' }
[ordered]@{CapturedUtc=[DateTimeOffset]::UtcNow;Purpose='Final read-only safety verification after user-approved source-file UI, approval and rejection tests';ReferenceFactsSha256=$before.ReferenceFactsSha256;AssetCount=4;AllFourAssetHashesAndSizesMatch=$true;Assets=$assets;SourceFreezeSha256=$before.SourceFreezeSha256;FrozenSourceCount=13;AllThirteenSourceHashesMatch=$true;Sources=$sources;Native=[ordered]@{AppPid=60000;ParentPid=47476;WrapperPid=62088;AppStartedUtc=(Get-Process -Id 60000).StartTime.ToUniversalTime();AppPath=$app.ExecutablePath;DllSha256=$dllHash;Bindings=$bindings;MatchesRecordedCandidate=$true};Client=[ordered]@{ContainerId=$client.Id;ImageId=$client.Image;StartedAtUtc=$client.StartedAt;Health=$client.Health;ReadonlyRootfs=$client.ReadonlyRootfs;User=$client.User;MatchesRecordedCandidate=$true};Publisher=[ordered]@{ContainerId=$publisher.Id;ImageId=$publisher.Image;StartedAtUtc=$publisher.StartedAt;MatchesOriginalBaseline=$true};AppApiCalls=$false;ProviderCalls=$false;SqlQueries=$false;TestsOrBuilds=$false;SourceOrAssetWrites=$false;UnrelatedDataFilesRead=$false;Limits='Original four source assets and frozen code are unchanged. The approved conversion produced a separate derived artifact; this record does not assert absence of all workspace writes or re-measure startup.'} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outFile -Encoding utf8NoBOM
[ordered]@{FinalProof=$outFile;FourAssetHashesMatch=$true;ThirteenFrozenSourcesMatch=$true;NativeCandidateUnchanged=$true;ClientCandidateUnchanged=$true;PublisherUnchanged=$true} | ConvertTo-Json -Compress

