param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..\..\..')).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repo = (Resolve-Path -LiteralPath $RepoRoot).Path
$proofRoot = Join-Path $repo 'codex\bundles\process-module-architecture-v3\proof\SB02'
$transcriptRoot = Join-Path $proofRoot 'transcripts'
New-Item -ItemType Directory -Force -Path $transcriptRoot | Out-Null

function Resolve-RepositoryPath {
    param([Parameter(Mandatory)][string]$RelativePath)

    $fullPath = Join-Path $repo $RelativePath
    if (Test-Path -LiteralPath $fullPath) {
        return (Resolve-Path -LiteralPath $fullPath).Path
    }

    $parent = Split-Path -Parent $fullPath
    if (![string]::IsNullOrWhiteSpace($parent) -and (Test-Path -LiteralPath $parent)) {
        $resolvedParent = (Resolve-Path -LiteralPath $parent).Path
        return Join-Path $resolvedParent (Split-Path -Leaf $fullPath)
    }

    return $fullPath
}

function Assert-SafeRepositoryPath {
    param([Parameter(Mandatory)][string]$Path)

    $fullPath = if (Test-Path -LiteralPath $Path) {
        (Resolve-Path -LiteralPath $Path).Path
    }
    else {
        [System.IO.Path]::GetFullPath($Path)
    }

    if (!$fullPath.StartsWith($repo + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside repository root. Path='$fullPath' Repo='$repo'"
    }

    return $fullPath
}

function Get-RepositoryRelativePath {
    param([Parameter(Mandatory)][string]$FullPath)

    $path = [System.IO.Path]::GetFullPath($FullPath)
    if (!$path.StartsWith($repo + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside repository root. Path='$path' Repo='$repo'"
    }

    return $path.Substring($repo.Length + 1).Replace('\', '/')
}

function Remove-RepositoryDirectory {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Assert-SafeRepositoryPath (Resolve-RepositoryPath $RelativePath)
    if (Test-Path -LiteralPath $path) {
        try {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
        catch {
            if ([System.IO.Directory]::Exists($path)) {
                Get-ChildItem -LiteralPath $path -Force |
                    Where-Object { $_.Name -ne '.artifacts' } |
                    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            }
        }

        return $RelativePath
    }

    return $null
}

function Remove-RepositoryFile {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Assert-SafeRepositoryPath (Resolve-RepositoryPath $RelativePath)
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
        return $RelativePath
    }

    return $null
}

function Set-RepositoryFile {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Content
    )

    $path = Assert-SafeRepositoryPath (Resolve-RepositoryPath $RelativePath)
    $directory = Split-Path -Parent $path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    Set-Content -LiteralPath $path -Value $Content -Encoding UTF8
}

function Update-RepositoryFile {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][scriptblock]$Updater
    )

    $path = Assert-SafeRepositoryPath (Resolve-RepositoryPath $RelativePath)
    if (!(Test-Path -LiteralPath $path)) {
        return
    }

    $content = Get-Content -LiteralPath $path -Raw
    $updated = & $Updater $content
    if ($updated -ne $content) {
        Set-Content -LiteralPath $path -Value $updated -Encoding UTF8
    }
}

function Remove-ProjectReferencesByName {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string[]]$ProjectNamePatterns
    )

    Update-RepositoryFile $RelativePath {
        param([string]$content)

        $lines = $content -split "\r?\n"
        $kept = foreach ($line in $lines) {
            $remove = $false
            foreach ($pattern in $ProjectNamePatterns) {
                if ($line -match '<ProjectReference\s+Include=' -and $line -match [regex]::Escape($pattern)) {
                    $remove = $true
                    break
                }
            }

            if (!$remove) {
                $line
            }
        }

        ($kept -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine
    }
}

function Remove-SolutionProjectsByName {
    param([Parameter(Mandatory)][string[]]$ProjectNamePatterns)

    Update-RepositoryFile 'CanDoItAll.slnx' {
        param([string]$content)

        $lines = $content -split "\r?\n"
        $kept = foreach ($line in $lines) {
            $remove = $false
            foreach ($pattern in $ProjectNamePatterns) {
                if ($line -match '<Project\s+Path=' -and $line -match [regex]::Escape($pattern)) {
                    $remove = $true
                    break
                }
            }

            if (!$remove) {
                $line
            }
        }

        $text = $kept -join [Environment]::NewLine
        $insert = @'
    <Project Path="src/CanDoItAll.Processes.Abstractions/CanDoItAll.Processes.Abstractions.csproj" />
    <Project Path="src/CanDoItAll.Processes.Projections/CanDoItAll.Processes.Projections.csproj" />
    <Project Path="src/CanDoItAll.Git/CanDoItAll.Git.csproj" />
    <Project Path="src/CanDoItAll.Processes.Templates/CanDoItAll.Processes.Templates.csproj" />
    <Project Path="src/CanDoItAll.Processes.Builder/CanDoItAll.Processes.Builder.csproj" />
    <Project Path="src/CanDoItAll.Processes.Runtime/CanDoItAll.Processes.Runtime.csproj" />
    <Project Path="src/CanDoItAll.Processes.Persistence/CanDoItAll.Processes.Persistence.csproj" />
    <Project Path="src/CanDoItAll.Processes.Application/CanDoItAll.Processes.Application.csproj" />
    <Project Path="src/CanDoItAll.Components.Git/CanDoItAll.Components.Git.csproj" />
'@

        if ($text -notmatch 'CanDoItAll\.Processes\.Abstractions\.csproj') {
            $anchor = '    <Project Path="src/CanDoItAll.Processes.Drivers.Abstractions/CanDoItAll.Processes.Drivers.Abstractions.csproj" />'
            $text = $text.Replace($anchor, $anchor + [Environment]::NewLine + $insert.TrimEnd())
        }

        $text.TrimEnd() + [Environment]::NewLine
    }
}

function New-SdkProject {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Sdk,
        [string[]]$ProjectReferences = @(),
        [string[]]$PackageReferences = @(),
        [switch]$BrowserSupported
    )

    $projectReferenceText = ''
    if ($ProjectReferences.Count -gt 0) {
        $items = $ProjectReferences | ForEach-Object { "    <ProjectReference Include=`"$_`" />" }
        $projectReferenceText = @"

  <ItemGroup>
$($items -join [Environment]::NewLine)
  </ItemGroup>
"@
    }

    $packageReferenceText = ''
    if ($PackageReferences.Count -gt 0) {
        $items = $PackageReferences | ForEach-Object { "    <PackageReference Include=`"$($_.Split('|')[0])`" Version=`"$($_.Split('|')[1])`" />" }
        $packageReferenceText = @"

  <ItemGroup>
$($items -join [Environment]::NewLine)
  </ItemGroup>
"@
    }

    $supportedPlatformText = ''
    if ($BrowserSupported) {
        $supportedPlatformText = @"

  <ItemGroup>
    <SupportedPlatform Include="browser" />
  </ItemGroup>
"@
    }

    Set-RepositoryFile $RelativePath @"
<Project Sdk="$Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>$supportedPlatformText$packageReferenceText$projectReferenceText

</Project>
"@
}

$oldProjectNames = @(
    'CanDoItAll.Processes.Drivers.ArtifactEvidence',
    'CanDoItAll.Processes.Drivers.BusinessAnalysis',
    'CanDoItAll.Processes.Drivers.ObservationAggregation',
    'CanDoItAll.Processes.Drivers.OfficeEvidence',
    'CanDoItAll.Processes.Drivers.RuntimeEvidence',
    'CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence',
    'CanDoItAll.Processes.Drivers.TranscriptVerification',
    'CanDoItAll.Processes.Drivers.VerificationGateway'
)

$removedDirectories = @()
@(
    'src/CanDoItAll.Modules.Processes',
    'src/CanDoItAll.Processes.Core',
    'src/CanDoItAll.Processes.Contracts',
    'src/CanDoItAll.Processes.Drivers.Abstractions',
    'src/CanDoItAll.Processes.Drivers.ArtifactEvidence',
    'src/CanDoItAll.Processes.Drivers.BusinessAnalysis',
    'src/CanDoItAll.Processes.Drivers.ObservationAggregation',
    'src/CanDoItAll.Processes.Drivers.OfficeEvidence',
    'src/CanDoItAll.Processes.Drivers.RuntimeEvidence',
    'src/CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence',
    'src/CanDoItAll.Processes.Drivers.TranscriptVerification',
    'src/CanDoItAll.Processes.Drivers.VerificationGateway'
) | ForEach-Object {
    $removed = Remove-RepositoryDirectory $_
    if ($removed) {
        $removedDirectories += $removed
    }
}

$oldReferencePatterns = @(
    'CanDoItAll.Processes.Core',
    'CanDoItAll.Processes.Contracts',
    'CanDoItAll.Processes.Drivers.Abstractions',
    'CanDoItAll.Processes.Drivers.ArtifactEvidence',
    'CanDoItAll.Processes.Drivers.BusinessAnalysis',
    'CanDoItAll.Processes.Drivers.ObservationAggregation',
    'CanDoItAll.Processes.Drivers.OfficeEvidence',
    'CanDoItAll.Processes.Drivers.RuntimeEvidence',
    'CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence',
    'CanDoItAll.Processes.Drivers.TranscriptVerification',
    'CanDoItAll.Processes.Drivers.VerificationGateway'
)

$csprojFiles = Get-ChildItem -LiteralPath $repo -Recurse -Filter *.csproj -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notlike "*\codex\bundles\process-module-rewrite-reference-v1\*" -and
        $_.FullName -notlike "*\bin\*" -and
        $_.FullName -notlike "*\obj\*"
    }
foreach ($csproj in $csprojFiles) {
    $relative = Get-RepositoryRelativePath $csproj.FullName
    Remove-ProjectReferencesByName $relative $oldReferencePatterns
}

Remove-SolutionProjectsByName $oldProjectNames

$removedTests = @()
$testPatterns = 'CanDoItAll\.Modules\.Processes|CanDoItAll\.Processes\.Contracts|CanDoItAll\.Processes\.Core|CanDoItAll\.Processes\.Drivers|ProcessRunAutomationDispatchService|ProcessesService|ProcessObservationService|ProcessObservationCache|ProcessBranchOutcomeRouting|ProcessRecoveryRouter|ProcessStepRun|ProcessArtifactRecord|ProcessJournalEntry|ProcessDriverVerificationGateway'
Get-ChildItem -LiteralPath (Join-Path $repo 'tests') -Recurse -File |
    Where-Object {
        $_.FullName -notlike "*\bin\*" -and
        $_.FullName -notlike "*\obj\*" -and
        ($_.Extension -in '.cs', '.razor', '.txt', '.md', '.json', '.yaml', '.yml')
    } |
    ForEach-Object {
        $content = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
        if ($content -match $testPatterns -or $_.Name -match '^(Process|Processes|LiveProcess|BusinessPlanProcess|AppSmokeTests\.Process|AppSmokeTests\.ProjectScopedProcess|AppSmokeTests\.ProjectStructureProcesses|Sb08OperationalObservabilityBrowserTests)') {
            $relative = Get-RepositoryRelativePath $_.FullName
            Remove-Item -LiteralPath $_.FullName -Force
            $removedTests += $relative
        }
    }

@(
    'tests/CanDoItAll.Tests.Unit/TestData/ProcessDriverMultiDomainCorpus',
    'tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverReadonlyOrchestrationEvidencePipeline',
    'tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverRuntimeEvidenceVerifierIntegrationHardening',
    'tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverReadonlyReleaseCandidateStabilization',
    'tests/CanDoItAll.Tests.Unit/TestData/Architecture/ProcessDriverMultiDomainVerificationGateway'
) | ForEach-Object {
    $removed = Remove-RepositoryDirectory $_
    if ($removed) {
        $removedDirectories += $removed
    }
}

New-SdkProject 'src/CanDoItAll.Processes.Contracts/CanDoItAll.Processes.Contracts.csproj' 'Microsoft.NET.Sdk'
Set-RepositoryFile 'src/CanDoItAll.Processes.Contracts/ProcessContractVersions.cs' @'
namespace CanDoItAll.Processes.Contracts;

public static class ProcessContractVersions
{
    public const string Current = "processes.contracts.v1";
}
'@

New-SdkProject 'src/CanDoItAll.Processes.Abstractions/CanDoItAll.Processes.Abstractions.csproj' 'Microsoft.NET.Sdk' @('..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj')
Set-RepositoryFile 'src/CanDoItAll.Processes.Abstractions/ProcessIds.cs' @'
namespace CanDoItAll.Processes.Abstractions;

public readonly record struct ProcessDefinitionId(Guid Value);

public readonly record struct ProcessInstanceId(Guid Value);

public readonly record struct ProcessStepId(Guid Value);

public readonly record struct ProcessArtifactId(Guid Value);

public readonly record struct ProcessTemplateId(Guid Value);
'@

New-SdkProject 'src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj' 'Microsoft.NET.Sdk' @(
    '..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj',
    '..\CanDoItAll.Processes.Abstractions\CanDoItAll.Processes.Abstractions.csproj'
)
Set-RepositoryFile 'src/CanDoItAll.Processes.Core/ProcessGraphKernel.cs' @'
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Core;

public sealed record ProcessGraphNode(ProcessStepId Id, string Key);

public static class ProcessGraphKernel
{
    public static bool HasDuplicateKeys(IEnumerable<ProcessGraphNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Key))
            {
                continue;
            }

            if (!seen.Add(node.Key))
            {
                return true;
            }
        }

        return false;
    }
}
'@

New-SdkProject 'src/CanDoItAll.Processes.Drivers.Abstractions/CanDoItAll.Processes.Drivers.Abstractions.csproj' 'Microsoft.NET.Sdk' @(
    '..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj',
    '..\CanDoItAll.Processes.Abstractions\CanDoItAll.Processes.Abstractions.csproj',
    '..\CanDoItAll.Processes.Core\CanDoItAll.Processes.Core.csproj'
)
Set-RepositoryFile 'src/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs' @'
namespace CanDoItAll.Processes.Drivers.Abstractions;

public sealed record ProcessDriverDescriptor(
    string Key,
    string DisplayName,
    IReadOnlySet<string> CapabilityTags);
'@

New-SdkProject 'src/CanDoItAll.Processes.Projections/CanDoItAll.Processes.Projections.csproj' 'Microsoft.NET.Sdk' @(
    '..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj',
    '..\CanDoItAll.Processes.Abstractions\CanDoItAll.Processes.Abstractions.csproj',
    '..\CanDoItAll.Processes.Core\CanDoItAll.Processes.Core.csproj'
)
Set-RepositoryFile 'src/CanDoItAll.Processes.Projections/ProcessProjectionContracts.cs' @'
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Projections;

public sealed record ProcessDefinitionListProjection(
    ProcessDefinitionId Id,
    string Name,
    string Status,
    DateTimeOffset UpdatedAtUtc);
'@

New-SdkProject 'src/CanDoItAll.Git/CanDoItAll.Git.csproj' 'Microsoft.NET.Sdk'
Set-RepositoryFile 'src/CanDoItAll.Git/GitRepositoryPath.cs' @'
namespace CanDoItAll.Git;

public readonly record struct GitRepositoryPath(string Value)
{
    public override string ToString() => Value;
}
'@

New-SdkProject 'src/CanDoItAll.Processes.Templates/CanDoItAll.Processes.Templates.csproj' 'Microsoft.NET.Sdk' @(
    '..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj',
    '..\CanDoItAll.Processes.Abstractions\CanDoItAll.Processes.Abstractions.csproj',
    '..\CanDoItAll.Processes.Core\CanDoItAll.Processes.Core.csproj'
)
Set-RepositoryFile 'src/CanDoItAll.Processes.Templates/ProcessTemplateSchemaMarker.cs' @'
namespace CanDoItAll.Processes.Templates;

public static class ProcessTemplateSchemaMarker
{
    public const string CurrentSchema = "process-template.v1";
}
'@

New-SdkProject 'src/CanDoItAll.Processes.Builder/CanDoItAll.Processes.Builder.csproj' 'Microsoft.NET.Sdk' @(
    '..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj',
    '..\CanDoItAll.Processes.Abstractions\CanDoItAll.Processes.Abstractions.csproj',
    '..\CanDoItAll.Processes.Core\CanDoItAll.Processes.Core.csproj',
    '..\CanDoItAll.Processes.Templates\CanDoItAll.Processes.Templates.csproj',
    '..\CanDoItAll.Processes.Drivers.Abstractions\CanDoItAll.Processes.Drivers.Abstractions.csproj'
)
Set-RepositoryFile 'src/CanDoItAll.Processes.Builder/ProcessBuildPlan.cs' @'
namespace CanDoItAll.Processes.Builder;

public sealed record ProcessBuildPlan(string PlanHash, IReadOnlyList<string> StepKeys);
'@

New-SdkProject 'src/CanDoItAll.Processes.Runtime/CanDoItAll.Processes.Runtime.csproj' 'Microsoft.NET.Sdk' @(
    '..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj',
    '..\CanDoItAll.Processes.Abstractions\CanDoItAll.Processes.Abstractions.csproj',
    '..\CanDoItAll.Processes.Core\CanDoItAll.Processes.Core.csproj',
    '..\CanDoItAll.Processes.Builder\CanDoItAll.Processes.Builder.csproj',
    '..\CanDoItAll.Processes.Drivers.Abstractions\CanDoItAll.Processes.Drivers.Abstractions.csproj'
)
Set-RepositoryFile 'src/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs' @'
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public interface IProcessRuntimeStateStore
{
    ValueTask<bool> ExistsAsync(ProcessInstanceId instanceId, CancellationToken cancellationToken = default);
}
'@

New-SdkProject 'src/CanDoItAll.Processes.Persistence/CanDoItAll.Processes.Persistence.csproj' 'Microsoft.NET.Sdk' @(
    '..\CanDoItAll.Processes.Contracts\CanDoItAll.Processes.Contracts.csproj',
    '..\CanDoItAll.Processes.Abstractions\CanDoItAll.Processes.Abstractions.csproj',
    '..\CanDoItAll.Processes.Core\CanDoItAll.Processes.Core.csproj',
    '..\CanDoItAll.Processes.Runtime\CanDoItAll.Processes.Runtime.csproj',
    '..\CanDoItAll.Processes.Projections\CanDoItAll.Processes.Projections.csproj'
)
Set-RepositoryFile 'src/CanDoItAll.Processes.Persistence/ProcessPersistenceAssemblyMarker.cs' @'
namespace CanDoItAll.Processes.Persistence;

public static class ProcessPersistenceAssemblyMarker;
'@

New-SdkProject 'src/CanDoItAll.Processes.Application/CanDoItAll.Processes.Application.csproj' 'Microsoft.NET.Sdk' @(
    '..\CanDoItAll.Processes.Builder\CanDoItAll.Processes.Builder.csproj',
    '..\CanDoItAll.Processes.Runtime\CanDoItAll.Processes.Runtime.csproj',
    '..\CanDoItAll.Processes.Persistence\CanDoItAll.Processes.Persistence.csproj',
    '..\CanDoItAll.Processes.Templates\CanDoItAll.Processes.Templates.csproj',
    '..\CanDoItAll.Processes.Projections\CanDoItAll.Processes.Projections.csproj',
    '..\CanDoItAll.Git\CanDoItAll.Git.csproj',
    '..\CanDoItAll.Processes.Drivers.Abstractions\CanDoItAll.Processes.Drivers.Abstractions.csproj'
)
Set-RepositoryFile 'src/CanDoItAll.Processes.Application/ProcessesApplicationAssemblyMarker.cs' @'
namespace CanDoItAll.Processes.Application;

public static class ProcessesApplicationAssemblyMarker;
'@

New-SdkProject 'src/CanDoItAll.Components.Git/CanDoItAll.Components.Git.csproj' 'Microsoft.NET.Sdk.Razor' @('..\CanDoItAll.Git\CanDoItAll.Git.csproj') @('Microsoft.AspNetCore.Components.Web|10.0.5') -BrowserSupported
Set-RepositoryFile 'src/CanDoItAll.Components.Git/GitComponentsAssemblyMarker.cs' @'
namespace CanDoItAll.Components.Git;

public static class GitComponentsAssemblyMarker;
'@

New-SdkProject 'src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj' 'Microsoft.NET.Sdk.Razor' @(
    '..\CanDoItAll.Processes.Application\CanDoItAll.Processes.Application.csproj',
    '..\CanDoItAll.Processes.Projections\CanDoItAll.Processes.Projections.csproj',
    '..\CanDoItAll.Components.Git\CanDoItAll.Components.Git.csproj'
) @('Microsoft.AspNetCore.Components.Web|10.0.5') -BrowserSupported
Set-RepositoryFile 'src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs' @'
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.Processes;

public static class ProcessesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProcessesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(ProcessModuleRewriteState.Disabled);
        return services;
    }
}

public sealed record ProcessModuleRewriteState(bool IsEnabled)
{
    public static ProcessModuleRewriteState Disabled { get; } = new(false);
}

public static class ProcessesModuleAssemblyMarker;
'@
Set-RepositoryFile 'src/CanDoItAll.Modules.Processes/_Imports.razor' @'
@using CanDoItAll.Modules.Processes
@using Microsoft.AspNetCore.Components
'@
Set-RepositoryFile 'src/CanDoItAll.Modules.Processes/Pages/ProcessesPage.razor' @'
@page "/processes"

<PageTitle>Processes</PageTitle>

<section class="mx-auto max-w-3xl px-6 py-10">
    <h1 class="text-2xl font-semibold text-slate-900">Processes</h1>
    <p class="mt-3 text-sm leading-6 text-slate-600">The Process module is being rebuilt over the new application and projection boundaries.</p>
</section>
'@
Set-RepositoryFile 'src/CanDoItAll.Modules.Processes/Pages/LiveProcessesPage.razor' @'
@page "/processes/live"

<PageTitle>Live Processes</PageTitle>

<section class="mx-auto max-w-3xl px-6 py-10">
    <h1 class="text-2xl font-semibold text-slate-900">Live Processes</h1>
    <p class="mt-3 text-sm leading-6 text-slate-600">Live process projections are unavailable until the new runtime and projection stores are introduced.</p>
</section>
'@

Remove-RepositoryFile 'src/CanDoItAll.Web/Api/ProcessesApi.cs' | Out-Null
Remove-RepositoryFile 'src/CanDoItAll.Web/GlobalUsings.cs' | Out-Null
Update-RepositoryFile 'src/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs' {
    param([string]$content)
    $content.Replace("        group.MapProcessesApi();" + [Environment]::NewLine, '')
}
Update-RepositoryFile 'src/CanDoItAll.Web/Composition/ShellNavigation.cs' {
    param([string]$content)
    $content = $content -replace '\s*new\("Processes", "/processes", "account_tree", "Role-first process definitions, runtime orchestration, evidence, and improvement signals\.", PinnedByDefault: false\),', ''
    $content = $content -replace '\s*new\("Live Processes", "/processes/live", "monitor_heart", "Live projection of running processes, active agents, metrics, and tool usage\.", PinnedByDefault: false\),', ''
    $content
}
Update-RepositoryFile 'src/CanDoItAll.Web/Components/App.razor' {
    param([string]$content)
    $content.Replace('<link rel="stylesheet" href="@Assets["_content/CanDoItAll.Modules.Processes/css/live-processes.css"]" />' + [Environment]::NewLine, '')
}

Update-RepositoryFile 'src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs' {
    param([string]$content)
    $content = $content -replace '\s*private const string ProcessVerificationAuditRecordsMigrationId = "20260610113813_AddProcessVerificationAuditRecords";', ''
    $content = $content -replace '\s*private const string StepDispatchClaimIndexName = "IX_Processes_StepRuns_ProcessRunId_AutomationDispatchLeaseExpi~";', ''
    $content = $content -replace ',\s*ProcessVerificationAuditRecordsMigrationId', ''
    $content = $content -replace '\s*"Processes_Outbox",', ''
    $content = $content -replace '\s*private static readonly string\[\] ProcessStepDispatchClaimColumns =\s*\[\s*"AutomationDispatchAttemptCount",\s*"AutomationDispatchClaimToken",\s*"AutomationDispatchClaimedAtUtc",\s*"AutomationDispatchClaimedBy",\s*"AutomationDispatchLeaseExpiresAtUtc"\s*\];', ''
    $content = $content -replace '\s*new\("Processes_StepRuns", ProcessStepDispatchClaimColumns\),', ''
    $content = $content -replace '\s*new\("Processes_StepRuns",\s*\[\s*"BlockReasonCode",\s*"RecoveryOptionsJson",\s*"NextRecoveryAction"\s*\]\),', ''
    $content = $content -replace '\s*new\("Processes_ArtifactRecords",\s*\[\s*"ProjectionLineageJson",\s*"ProjectionIdentityHash"\s*\]\),', ''
    $content = $content -replace '\s*new\("Processes_StepDefinitions",\s*\[\s*"AllowedOperations",\s*"OperationTargetScope"\s*\]\),', ''
    $content = $content -replace '\s*new\("Processes_DefinitionVersions", \["ContractMode"\]\),', ''
    $content = $content -replace '\s*new\("Processes_ArtifactExpectations",\s*\[\s*"SubprocessChildArtifactExpectationId",\s*"WorkflowOutputId",\s*"WorkflowOutputKind",\s*"WorkflowOutputName"\s*\]\)', ''
    $content = $content -replace '\s*private static readonly string\[\] MergedBaselineIndexRequirements =\s*\[\s*"IX_Processes_ArtifactExpectations_SubprocessChildArtifactExpec~",\s*"IX_Processes_ArtifactRecords_ProcessRunId_ProjectionIdentityHa~"\s*\];', '    private static readonly string[] MergedBaselineIndexRequirements = [];'
    $content = $content -replace '\s*await EnsureProcessStepDispatchClaimIndexAsync\(dbContext, cancellationToken\);', ''
    $content = $content -replace '\s*await EnsureProcessClaimHotPathIndexesAsync\(dbContext, cancellationToken\);', ''
    $content = $content -replace '\s*private static Task EnsureProcessStepDispatchClaimIndexAsync\([\s\S]*?\);\s*private static Task EnsureProcessClaimHotPathIndexesAsync', '    private static Task EnsureAutomationClaimHotPathIndexesAsync'
    $content = $content -replace '\s*CREATE INDEX IF NOT EXISTS "IX_Processes_Outbox_PendingClaimOrder"[\s\S]*?WHERE "Status" = 0;\s*', ''
    $content
}

Remove-ProjectReferencesByName 'src/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj' @('CanDoItAll.Modules.Processes')
Update-RepositoryFile 'src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs' {
    param([string]$content)
    $content = $content.Replace('using CanDoItAll.Modules.Processes;' + [Environment]::NewLine, '')
    $content = $content -replace '\s*\[Inject\]\s*public ProcessesService ProcessesService \{ get; set; \} = default!;', ''
    $content = $content -replace '\s*private IReadOnlyList<ProcessDefinitionListItem> processDefinitions = \[\];', ''
    $content = $content -replace '\s*private bool areProcessDefinitionsLoaded;', ''
    $content = $content -replace '\s*private bool isLoadingProcessDefinitions;', ''
    $content = $content -replace '\s*private bool processDefinitionsRequested;', ''
    $content = $content -replace '\s*private string\? processDefinitionsErrorMessage;', ''
    $content = $content -replace '\s*private Task\? processDefinitionsLoadTask;', ''
    $content = $content -replace '\s*private Task RequestProcessDefinitionsAsync\(\)[\s\S]*?private Task EnsureProjectStructureProjectsLoadedAsync\(\)', '    private Task EnsureProjectStructureProjectsLoadedAsync()'
    $content = $content -replace '\s*private Task EnsureProcessDefinitionsLoadedAsync\(\)[\s\S]*?private async Task SaveAgentAsync\(\)', '    private async Task SaveAgentAsync()'
    $content = $content -replace '\s*if \(isEnabled\)\s*\{\s*processDefinitionsRequested = true;\s*_ = EnsureProcessDefinitionsLoadedAsync\(\);\s*\}', ''
    $content = $content -replace '\s*if \(isEnabled\)\s*\{\s*editorModel\.ProcessAccess\.CanRead = true;\s*processDefinitionsRequested = true;\s*_ = EnsureProcessDefinitionsLoadedAsync\(\);\s*\}', '        if (isEnabled)' + [Environment]::NewLine + '        {' + [Environment]::NewLine + '            editorModel.ProcessAccess.CanRead = true;' + [Environment]::NewLine + '        }'
    $content = $content -replace '\s*private bool HasProcessAccess\(Guid definitionId\)[\s\S]*?private void ClearProcesses\(\)[\s\S]*?\}', ''
    $content
}
Update-RepositoryFile 'src/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor' {
    param([string]$content)
    $content -replace '(?s)\s*<div class="cda-admin-subpanel md:col-span-2">.*?</div>\s*</Grid>', @'
                                <div class="cda-admin-subpanel md:col-span-2">
                                    <p class="cda-field-label mb-1">Allowed processes</p>
                                    <p class="text-sm leading-6 text-slate-600">
                                        @DescribeProcessScope(editorModel)
                                    </p>
                                    <div class="mt-3 cda-admin-empty">
                                        Process definition selection is unavailable while the Process module is rebuilt. Existing stored access metadata is preserved.
                                    </div>
                                </div>
                            </Grid>
'@
}

Remove-ProjectReferencesByName 'src/CanDoItAll.Modules.SchedulerPlanner/CanDoItAll.Modules.SchedulerPlanner.csproj' @('CanDoItAll.Modules.Processes')
Update-RepositoryFile 'src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs' {
    param([string]$content)
    $content = $content.Replace('using CanDoItAll.Modules.Processes;' + [Environment]::NewLine, '')
    $content = $content -replace ',\s*ProcessesService processesService,', ','
    $content = $content -replace 'ProcessesService processesService,\s*', ''
    $content = $content -replace 'var firstProcess = targets\.FirstOrDefault\(item => item\.Kind == SchedulerPlanTargetKind\.Process\);\s*var firstTarget = firstProcess \?\? targets\.FirstOrDefault\(\);', 'var firstTarget = targets.FirstOrDefault(item => item.Kind == SchedulerPlanTargetKind.Workflow);'
    $content = $content -replace 'TargetKind = firstTarget\?\.Kind \?\? SchedulerPlanTargetKind\.Process,', 'TargetKind = firstTarget?.Kind ?? SchedulerPlanTargetKind.Workflow,'
    $content = $content -replace '(?s)\s*var processTargets = \(await processesService\.ListDefinitionsAsync\(cancellationToken: cancellationToken\)\).*?\.ToArray\(\);\s*var workflowTargets', '        var workflowTargets'
    $content = $content -replace 'return processTargets\s*\.Concat\(workflowTargets\)\s*\.OrderBy\(item => item\.Kind\)\s*\.ThenBy\(item => item\.Name, StringComparer\.OrdinalIgnoreCase\)\s*\.ToArray\(\);', 'return workflowTargets' + [Environment]::NewLine + '            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)' + [Environment]::NewLine + '            .ToArray();'
    $content = $content -replace '\s*SchedulerPlanTargetKind\.Process => await LaunchProcessAsync\(plan, firedAtUtc, cancellationToken\),', ''
    $content = $content -replace '(?s)\s*private async Task<SchedulerTargetLaunchResult> LaunchProcessAsync\([\s\S]*?private async Task<SchedulerTargetLaunchResult> LaunchWorkflowAsync', '    private async Task<SchedulerTargetLaunchResult> LaunchWorkflowAsync'
    $content
}

Remove-ProjectReferencesByName 'src/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj' @(
    'CanDoItAll.Modules.Processes',
    'CanDoItAll.Processes.Contracts'
)
Update-RepositoryFile 'src/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs' {
    param([string]$content)
    $content = $content.Replace('using CanDoItAll.Modules.Processes;' + [Environment]::NewLine, '')
    $content = $content.Replace('        services.AddScoped<IProjectStructureProjectionContributor, ProcessProjectionContributor>();' + [Environment]::NewLine, '')
    $content = $content.Replace('        services.AddScoped<IProcessProjectStructureBridge, ProjectStructureProcessRunSyncBridge>();' + [Environment]::NewLine, '')
    $content = $content.Replace('        services.AddScoped<ProjectStructureProcessNodeService>();' + [Environment]::NewLine, '')
}
Update-RepositoryFile 'src/CanDoItAll.Modules.Workbench/ProjectNodes/ProjectNodeScopeBridge.cs' {
    param([string]$content)
    $content = $content.Replace('using CanDoItAll.Modules.Processes;' + [Environment]::NewLine, '')
    $content = $content -replace '(?s)\s*if \(TryParsePrefixedGuidNodeKey\(nodeKey, "process-definition:"[\s\S]*?return BuildProjectedResolution\(runProjectId, projectId, ProjectObjectType\.ProcessRun\);\s*\}', ''
    $content
}
Update-RepositoryFile 'src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchRelationService.cs' {
    param([string]$content)
    $content = $content.Replace('using CanDoItAll.Modules.Processes;' + [Environment]::NewLine, '')
    $content = $content -replace '(?s)\s*existingNodes = await AugmentProcessProjectionNodesAsync\([\s\S]*?cancellationToken\);', ''
    $content = $content -replace '(?s)\s*private static async Task<IReadOnlyList<ProjectObjectRecord>> AugmentProcessProjectionNodesAsync\([\s\S]*?private static Guid\? TryResolveProcessDefinitionId', '    private static Guid? TryResolveProcessDefinitionId'
    $content
}
Update-RepositoryFile 'src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs' {
    param([string]$content)
    $content -replace '(?s)\s*internal sealed class ProcessProjectionContributor : IProjectStructureProjectionContributor[\s\S]*$', ''
}
Set-RepositoryFile 'src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessNodeService.cs' @'
namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureProcessNodeService
{
    public Task<ProjectStructureProcessNodeStartResult> StartAsync(
        Guid projectId,
        string nodeId,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentContext agent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(agent);

        throw new ProjectStructureAgentException(
            410,
            "ProcessModuleRewriteInProgress",
            "Project-structure process launching is unavailable until the rebuilt Process application layer is introduced.");
    }
}
'@
Set-RepositoryFile 'src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunSyncBridge.cs' @'
namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureProcessRunSyncBridge;
'@
Update-RepositoryFile 'src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.OverlayStates.cs' {
    param([string]$content)
    $content = $content.Replace('using CanDoItAll.Modules.Processes;' + [Environment]::NewLine, '')
    $content = $content.Replace('    public ProcessRunEstimateResult? Estimate { get; init; }', '    public ProjectStructureProcessEstimateSummary? Estimate { get; init; }')
    $content += @'

public sealed record ProjectStructureProcessEstimateSummary(
    decimal EstimatedCostUsd,
    int EstimatedElapsedMinutes,
    int EstimatedTouchMinutes,
    string ConfidenceLabel,
    string SourceLabel,
    string Summary);
'@
    $content
}
Set-RepositoryFile 'src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Processes.cs' @'
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.Workbench.Pages;

public partial class ProjectStructurePage
{
    private ProjectStructureProcessLinkDialogState? processLinkDialog;
    private ProjectStructureProcessStartDialogState? processStartDialog;

    private Task OpenLinkProcessDialogAsync(ProjectStructureNode node)
    {
        processLinkDialog = new ProjectStructureProcessLinkDialogState(
            node.Id,
            node.Title,
            [],
            null,
            "Process linking is unavailable while the Process module is rebuilt.");
        return InvokeAsync(StateHasChanged);
    }

    private void CloseProcessLinkDialog()
    {
        processLinkDialog = null;
    }

    private void HandleProcessLinkSelectionChanged(ChangeEventArgs args)
    {
    }

    private Task ExecuteProcessLinkAsync()
    {
        processLinkDialog = processLinkDialog with
        {
            Error = "Process linking is unavailable while the Process module is rebuilt."
        };
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenStartProcessDialogAsync(ProjectStructureNode node)
    {
        processStartDialog = new ProjectStructureProcessStartDialogState(
            ProjectId,
            Guid.Empty,
            node.Id,
            node.Title,
            null,
            string.Empty,
            null,
            ProjectStructureProcessStartStage.Confirm,
            false,
            false,
            "Process launching is unavailable while the Process module is rebuilt.",
            [],
            string.Empty,
            DateTimeOffset.UtcNow,
            false,
            string.Empty)
        {
            EstimateOnlyMode = false
        };
        return InvokeAsync(StateHasChanged);
    }

    private Task OpenEstimateProcessDialogAsync(ProjectStructureNode node)
    {
        return OpenStartProcessDialogAsync(node);
    }

    private void CloseProcessStartDialog()
    {
        processStartDialog = null;
    }

    private Task ReviewAndStartProcessAsync()
    {
        return ExecuteProcessStartAsync();
    }

    private Task ExecuteProcessStartAsync()
    {
        if (processStartDialog is not null)
        {
            processStartDialog = processStartDialog with
            {
                Error = "Process launching is unavailable while the Process module is rebuilt."
            };
        }

        return InvokeAsync(StateHasChanged);
    }

    private Task SelectProcessStartCandidateAsync(ProjectStructureProcessStartCandidateSelection selection)
    {
        return Task.CompletedTask;
    }

    private Task OpenManualProcessStartAgentPickerAsync(Guid launchPlanRoleId)
    {
        return Task.CompletedTask;
    }

    private Task HandleProcessStartAssignmentsReviewedChanged(ChangeEventArgs args)
    {
        if (processStartDialog is not null)
        {
            var isChecked = args.Value is bool value && value;
            processStartDialog = processStartDialog with
            {
                AssignmentsReviewed = isChecked
            };
        }

        return InvokeAsync(StateHasChanged);
    }

    private Task RequestHrManagerMatchAsync()
    {
        return ExecuteProcessStartAsync();
    }

    private Task CancelHrManagerMatchAsync()
    {
        return Task.CompletedTask;
    }

    private Task ExecuteHrManagerMatchAsync()
    {
        return ExecuteProcessStartAsync();
    }
}
'@

Remove-ProjectReferencesByName 'src/CanDoItAll.Components.WebGlSandbox/CanDoItAll.Components.WebGlSandbox.csproj' @('CanDoItAll.Modules.Processes')
Remove-RepositoryFile 'src/CanDoItAll.Components.WebGlSandbox/ProcessWebGlSandboxSession.cs' | Out-Null
Remove-RepositoryFile 'src/CanDoItAll.Components.WebGlSandbox/Components/Pages/ProcessWorkbench.razor' | Out-Null
Update-RepositoryFile 'src/CanDoItAll.Components.WebGlSandbox/_Imports.razor' {
    param([string]$content)
    $content.Replace('@using CanDoItAll.Modules.Processes' + [Environment]::NewLine, '')
}
Set-RepositoryFile 'src/CanDoItAll.Components.WebGlSandbox/Program.cs' @'
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.WebGlSandbox.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/_dev/runtime", () => Results.Json(new
{
    isReady = true,
    application = "webgl-sandbox"
}));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
'@

Remove-ProjectReferencesByName 'src/Space3D/CanDoItAll.Space3D.Mouse.Sandbox/CanDoItAll.Space3D.Mouse.Sandbox.csproj' @('CanDoItAll.Modules.Processes')
Remove-RepositoryFile 'src/Space3D/CanDoItAll.Space3D.Mouse.Sandbox/Space3DProcessWorkbenchSession.cs' | Out-Null
Remove-RepositoryFile 'src/Space3D/CanDoItAll.Space3D.Mouse.Sandbox/Components/Pages/Space3DProcessWorkbench.razor' | Out-Null
Set-RepositoryFile 'src/Space3D/CanDoItAll.Space3D.Mouse.Sandbox/Program.cs' @'
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Space3D.Mouse.Sandbox.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCanDoItAllBaseLib();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/_dev/runtime", () => Results.Json(new
{
    isReady = true,
    application = "space3d-mouse-sandbox"
}));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
}
'@

Remove-ProjectReferencesByName 'tools/CanDoItAll.ScenarioSeeder/CanDoItAll.ScenarioSeeder.csproj' @(
    'CanDoItAll.Modules.Processes',
    'CanDoItAll.Processes.Contracts'
)
Remove-RepositoryFile 'tools/CanDoItAll.ScenarioSeeder/GlobalUsings.cs' | Out-Null
Remove-RepositoryFile 'tools/CanDoItAll.ScenarioSeeder/ProcessCatalog.Part1.cs' | Out-Null
Remove-RepositoryFile 'tools/CanDoItAll.ScenarioSeeder/ProcessCatalog.Part2.cs' | Out-Null
Remove-RepositoryFile 'tools/CanDoItAll.ScenarioSeeder/ProcessCatalog.Part3.cs' | Out-Null
Set-RepositoryFile 'tools/CanDoItAll.ScenarioSeeder/AgentFrameworkIntegrationSimulationSeeder.cs' @'
namespace CanDoItAll.ScenarioSeeder;

internal sealed class AgentFrameworkIntegrationSimulationSeeder
{
    public Task<object> SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<object>(new
        {
            Status = "Unavailable",
            Reason = "Process scenario seeding is disabled while the Process module is rebuilt."
        });
    }

    public sealed record PartySpec(
        string ExternalKey,
        object PartyType,
        string DisplayName,
        string? LegalName,
        string Summary,
        string Responsibilities,
        string LaneKey,
        string EscalationRule,
        string? Email,
        IReadOnlyList<object> Roles);

    public sealed record GraphNodeSpec(
        string Key,
        object ObjectType,
        string ObjectSubtype,
        string Title,
        string Subtitle,
        string Status,
        double PositionX,
        double PositionY,
        string? ParentKey,
        string Notes,
        string SourceHint = "",
        bool IsSystemManaged = true);

    public sealed record GraphLinkSpec(
        string SourceKey,
        string TargetKey,
        object LinkKind);
}
'@

$summary = [ordered]@{
    RemovedDirectories = $removedDirectories
    RemovedTests = $removedTests
    SkeletonProjects = @(
        'src/CanDoItAll.Processes.Contracts',
        'src/CanDoItAll.Processes.Abstractions',
        'src/CanDoItAll.Processes.Core',
        'src/CanDoItAll.Processes.Drivers.Abstractions',
        'src/CanDoItAll.Processes.Projections',
        'src/CanDoItAll.Git',
        'src/CanDoItAll.Processes.Templates',
        'src/CanDoItAll.Processes.Builder',
        'src/CanDoItAll.Processes.Runtime',
        'src/CanDoItAll.Processes.Persistence',
        'src/CanDoItAll.Processes.Application',
        'src/CanDoItAll.Components.Git',
        'src/CanDoItAll.Modules.Processes'
    )
}

$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $transcriptRoot 'reset-summary.json') -Encoding UTF8
$summary | ConvertTo-Json -Depth 6

