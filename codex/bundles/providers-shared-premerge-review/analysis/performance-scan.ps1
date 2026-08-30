param(
    [string] $Repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
)

$ErrorActionPreference = 'Stop'
$scopes = @(
    'src/MAF/ProviderHistory',
    'src/Integration/CanDoItAll.SharedProviders.Abstractions',
    'src/Integration/CanDoItAll.SharedProviders.Http',
    'src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders',
    'src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/History',
    'src/MAF/Common/CanDoItAll.AgentFramework.Persistence/History'
)
$recipes = [ordered]@{
    'critical.indexof_string_no_comparison' = '\.IndexOf\("[^"]+"\)'
    'critical.substring' = '\.Substring\('
    'critical.startswith_endswith_literal_no_comparison' = '\.(StartsWith|EndsWith)\("[^"]+"\)'
    'critical.contains_literal_no_comparison' = '\.Contains\("[^"]+"\)'
    'async.async_void' = 'async void'
    'async.blocking_candidates' = '\.Result\b|\.Wait\(|GetAwaiter\(\)\.GetResult\('
    'async.task_run' = 'Task\.Run\('
    'async.value_task' = '\bValueTask\b'
    'async.blocking_collection' = 'BlockingCollection<'
    'memory.case_without_culture' = '\.(ToLower|ToUpper)\(\)'
    'memory.three_replace_chain' = '\.Replace\(.*\.Replace\(.*\.Replace\('
    'memory.params' = 'params '
    'memory.linq_char' = '\.(All|Any)\(char\.'
    'memory.stackalloc' = '\bstackalloc\b'
    'memory.byte_char_arrays' = 'new (byte|char)\['
    'memory.array_pool' = 'ArrayPool<'
    'memory.string_format' = 'string\.Format\('
    'memory.plus_equals_candidates' = '\+='
    'memory.indexofany' = '\.IndexOfAny\('
    'memory.searchvalues' = '\bSearchValues\b'
    'regex.compiled' = 'RegexOptions\.Compiled'
    'regex.generated' = 'GeneratedRegex'
    'regex.new_regex' = 'new Regex\('
    'regex.all_regex_declarations' = '\bRegex\b'
    'regex.nonbacktracking' = 'RegexOptions\.NonBacktracking'
    'regex.match_success_or_next' = '\.Success\b|\.NextMatch\('
    'collections.static_dictionary' = 'static readonly Dictionary<'
    'collections.static_frozen_dictionary' = 'static readonly FrozenDictionary<'
    'collections.new_list' = 'new List<'
    'collections.new_dictionary' = 'new Dictionary<'
    'collections.current_culture_comparer' = 'StringComparer\.CurrentCulture'
    'collections.linq_chains' = '\.(Select|Where|Cast|Take|Aggregate)\('
    'collections.containskey' = '\.ContainsKey\('
    'collections.trygetvalue' = '\.TryGetValue\('
    'io.new_httpclient' = 'new HttpClient\('
    'io.new_serializer_options' = 'new JsonSerializerOptions'
    'io.serializer_calls' = 'JsonSerializer\.(Serialize|Deserialize)'
    'io.json_source_generation' = 'JsonSerializable|JsonSerializerContext'
    'io.file_stream_constructors' = 'new FileStream\('
    'io.async_file_options' = 'FileOptions\.Asynchronous|useAsync: true'
    'io.response_headers_read' = 'HttpCompletionOption\.ResponseHeadersRead'
    'io.http_send_get' = '\.(SendAsync|GetAsync)\('
    'io.stream_legacy_read_write' = '\.(ReadAsync|WriteAsync)\([^,]+,\s*0\s*,'
    'structural.unsealed_class' = '^\s*((public|internal|private|protected|file)\s+)?(partial\s+)?class '
    'structural.sealed_class' = 'sealed class'
    'structural.equatable' = ': IEquatable'
    'inline.indexof_literal' = '\.IndexOf\("'
    'inline.comparison_candidates' = '\.(StartsWith|EndsWith|Contains)\s*\('
    'inline.replace' = '\.Replace\('
    'inline.linq_select_where_order_group' = '\.Select|\.Where|\.OrderBy|\.GroupBy'
    'inline.all_any' = '\.All|\.Any'
    'inline.public_internal_class' = 'public class |internal class '
    'inverse.stringcomparison' = 'StringComparison\.'
    'inverse.ordinal_comparer' = 'StringComparer\.Ordinal'
    'inverse.params_span' = 'params (ReadOnly)?Span<'
}

Push-Location -LiteralPath $Repository
try {
    $files = @(rg --files @scopes -g '*.cs' -g '!bin/**' -g '!obj/**')
    if ($LASTEXITCODE -ne 0) {
        throw 'Cannot enumerate scan scope.'
    }
    $rows = [System.Collections.Generic.List[object]]::new()
    $matches = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in $recipes.GetEnumerator()) {
        $raw = @(rg --json -g '*.cs' -g '!**/bin/**' -g '!**/obj/**' -- $entry.Value @scopes)
        if ($LASTEXITCODE -gt 1) {
            throw ('rg failed for ' + $entry.Key)
        }
        $count = 0
        foreach ($line in $raw) {
            $item = $line | ConvertFrom-Json
            if ($item.type -ne 'match') {
                continue
            }
            if (($entry.Key -in @('collections.new_list', 'collections.new_dictionary', 'io.new_serializer_options')) -and $item.data.lines.text -match 'static|readonly') {
                continue
            }
            if ($entry.Key -eq 'async.async_void' -and $item.data.lines.text -match 'event') {
                continue
            }
            $count++
            $matches.Add([pscustomobject]@{
                Recipe = $entry.Key
                File = $item.data.path.text.Replace('\', '/')
                Line = $item.data.line_number
            })
        }
        $rows.Add([pscustomobject]@{ Recipe = $entry.Key; MatchingLines = $count; Regex = $entry.Value })
    }
    $destination = Join-Path $Repository 'codex/bundles/providers-shared-premerge-review/analysis'
    $rows | Export-Csv -LiteralPath (Join-Path $destination 'performance-scan-counts.csv') -NoTypeInformation
    $matches | Export-Csv -LiteralPath (Join-Path $destination 'performance-scan-locations.csv') -NoTypeInformation
    [pscustomobject]@{
        Head = (git rev-parse HEAD)
        ScannedFileCount = $files.Count
        Scope = $scopes
        Files = @($files | ForEach-Object { $_.Replace('\', '/') } | Sort-Object)
        CountingRule = 'Matching source lines, not occurrences; rg recipes adapted from skill grep. Generated build outputs excluded. Raw candidate counts are not confirmed findings.'
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'performance-scan-scope.json')
    $rows | Format-Table Recipe, MatchingLines -AutoSize
} finally {
    Pop-Location
}
