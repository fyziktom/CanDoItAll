[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepositoryRoot).Path
$outputRoot = Join-Path $root ".artifacts/maf-1.15-discovery"
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$excludedSegments = @(
    [IO.Path]::DirectorySeparatorChar + ".git" + [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::DirectorySeparatorChar + "bin" + [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::DirectorySeparatorChar + "obj" + [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::DirectorySeparatorChar + ".artifacts" + [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::DirectorySeparatorChar + "ExternalPackages" + [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::DirectorySeparatorChar + "node_modules" + [IO.Path]::DirectorySeparatorChar
)

$allowedExtensions = @(
    ".cs", ".csproj", ".props", ".targets", ".json", ".yaml", ".yml", ".md"
)

$ripgrep = Get-Command rg -ErrorAction SilentlyContinue
if ($null -ne $ripgrep) {
    Push-Location $root
    try {
        $relativeFiles = & $ripgrep.Source --files --hidden `
            --glob "*.cs" `
            --glob "*.csproj" `
            --glob "*.props" `
            --glob "*.targets" `
            --glob "*.json" `
            --glob "*.yaml" `
            --glob "*.yml" `
            --glob "*.md" `
            --glob "!**/.git/**" `
            --glob "!**/bin/**" `
            --glob "!**/obj/**" `
            --glob "!**/.artifacts/**" `
            --glob "!**/ExternalPackages/**" `
            --glob "!**/node_modules/**"
        if ($LASTEXITCODE -ne 0) {
            throw "rg file discovery failed with exit code $LASTEXITCODE."
        }

        $files = @($relativeFiles | ForEach-Object {
            [IO.FileInfo]::new((Join-Path $root $_))
        })
    }
    finally {
        Pop-Location
    }
}
else {
    $files = Get-ChildItem -Path $root -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
        $path = $_.FullName
        $isExcluded = $false
        foreach ($segment in $excludedSegments) {
            if ($path.IndexOf($segment, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $isExcluded = $true
                break
            }
        }

        -not $isExcluded -and $allowedExtensions.Contains($_.Extension)
    }
}

$groups = [ordered]@{
    "packages" = @(
        "Microsoft.Agents.AI",
        "Microsoft.Extensions.AI",
        "PackageReference",
        "PackageVersion"
    )
    "agent-pipeline" = @(
        "ChatClientAgentOptions",
        "UseProvidedChatClientAsIs",
        "DisableApprovalResponseBinding",
        "DisableApprovalNotRequiredFunctionBypassing",
        "EnableNonApprovalRequiredFunctionBypassing",
        "FunctionInvokingChatClient",
        "UseApprovalResponseBinding",
        "UseApprovalNotRequiredFunctionBypassing",
        "AsBuilder()",
        "BuildAIAgent",
        "AsAIAgent"
    )
    "approvals" = @(
        "ToolApprovalRequestContent",
        "ToolApprovalResponseContent",
        "ApprovalRequiredAIFunction",
        "ToolApprovalAgent",
        "ToolAutoApprovalRuleContext",
        "PendingToolApprovalRecord",
        "RespondToPendingApprovals",
        "CreateResponse(",
        "ApprovalId",
        "CallId"
    )
    "sessions" = @(
        "SerializeSessionAsync",
        "DeserializeSessionAsync",
        "AgentSessionStateBag",
        "ChatClientAgentSession",
        "SerializedSessionStateJson",
        "conversationId",
        "RequestScopedSessionContentScrubber",
        "ShouldReplayTranscriptAfterApproval",
        "RequirePerServiceCallChatHistoryPersistence"
    )
    "workflows" = @(
        "AgentWorkflowBuilder",
        "WorkflowHostAgent",
        "WorkflowSession",
        "WorkflowOutputEvent",
        "AsAIAgent",
        "ToAgentResponse",
        "MessageMerger",
        "HandoffDepthGuard",
        "EmitAgentResponseEvents",
        "EmitAgentResponseUpdateEvents",
        "includeWorkflowOutputsInResponse"
    )
    "checkpointing" = @(
        "CheckpointManager",
        "ICheckpointStore",
        "WorkflowBackedAgentExecutionCheckpointBridge",
        "ExternalRequest",
        "RequestPort",
        "Resume"
    )
    "file-harness" = @(
        "HarnessAgent",
        "HarnessAgentOptions",
        "Microsoft.Agents.AI.Harness",
        "FileAccessStore",
        "FileAccessProvider",
        "FileAccessProviderOptions",
        "DisableFileAccess",
        "FileMemoryProvider",
        "FileSystemAgentFileStore",
        "LocalCodeAct"
    )
    "custom-filetools" = @(
        "IWorkspaceFileService",
        "WorkspaceFileService",
        "IWorkspacePathResolutionService",
        "IWorkspaceCommandExecutionService",
        "IWorkspaceArtifactToolService",
        "CanDoItAll.FileTools",
        "WorkspaceScopeDescriptor",
        "ExternalTarget"
    )
    "hosting-protocols" = @(
        "Microsoft.Agents.AI.A2A",
        "Microsoft.Agents.AI.Hosting.A2A",
        "AddAgentFrameworkA2AHosting",
        "AGUI",
        "AddAGUI",
        "MapAGUI",
        "OpenAIResponses",
        "HostedAgentState",
        "HostedWorkflowState",
        "HostedWorkflowRunResult",
        "DeleteSessionAsync",
        "autoSend"
    )
    "optional" = @(
        "CompactionProvider",
        "CompactionStrategy",
        "FileMemoryProvider",
        "MessageInjectingChatClient",
        "EnableMessageInjection",
        "CosmosChatHistoryProvider",
        "TodoProvider",
        "AgentModeProvider"
    )
    "merge-snapshot" = @(
        "MafAgentResponseSnapshotter",
        "AgentResponseUpdate",
        "CreatedAt",
        "MessageId",
        "ResponseId",
        "OrderBy(",
        "GroupBy(",
        "Distinct("
    )
}

foreach ($entry in $groups.GetEnumerator()) {
    $target = Join-Path $outputRoot ($entry.Key + ".txt")
    $lines = New-Object System.Collections.Generic.List[string]
    $combinedPattern = ($entry.Value | ForEach-Object {
        [Regex]::Escape($_)
    }) -join "|"
    $groupMatches = @($files | Select-String -Pattern $combinedPattern)

    foreach ($pattern in $entry.Value) {
        $lines.Add("===== PATTERN: $pattern =====")
        $patternMatches = @($groupMatches | Where-Object {
            $_.Line.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0
        })
        foreach ($match in $patternMatches) {
            $relative = [IO.Path]::GetRelativePath($root, $match.Path)
            $lines.Add(("{0}:{1}:{2}" -f $relative, $match.LineNumber, $match.Line.Trim()))
        }

        if ($patternMatches.Count -eq 0) {
            $lines.Add("<no matches>")
        }

        $lines.Add("")
    }

    $lines | Set-Content -Path $target -Encoding utf8
}

$metadata = New-Object System.Collections.Generic.List[string]
$metadata.Add("repositoryRoot=$root")
$metadata.Add("capturedAtUtc=$([DateTimeOffset]::UtcNow.ToString('O'))")

Push-Location $root
try {
    $metadata.Add("gitHead=$(git rev-parse HEAD)")
    $metadata.Add("gitBranch=$(git branch --show-current)")
    $metadata.Add("gitStatusBegin")
    foreach ($line in (git status --short)) {
        $metadata.Add([string]$line)
    }
    $metadata.Add("gitStatusEnd")

    dotnet --info | Set-Content -Path (Join-Path $outputRoot "dotnet-info.txt") -Encoding utf8

    $projectFiles = $files | Where-Object {
        $_.Extension -eq ".csproj"
    }

    $packageLines = New-Object System.Collections.Generic.List[string]
    foreach ($project in $projectFiles) {
        $matches = Select-String -Path $project.FullName -Pattern "Microsoft.Agents.AI" -SimpleMatch
        foreach ($match in $matches) {
            $relative = [IO.Path]::GetRelativePath($root, $match.Path)
            $packageLines.Add(("{0}:{1}:{2}" -f $relative, $match.LineNumber, $match.Line.Trim()))
        }
    }

    $packageLines | Set-Content -Path (Join-Path $outputRoot "direct-maf-package-references.txt") -Encoding utf8
}
finally {
    Pop-Location
}

$metadata | Set-Content -Path (Join-Path $outputRoot "metadata.txt") -Encoding utf8

Write-Host "MAF 1.15 discovery output: $outputRoot"
