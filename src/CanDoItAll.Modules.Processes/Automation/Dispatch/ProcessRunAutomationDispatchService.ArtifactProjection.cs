using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private async Task ProjectExecutionArtifactsAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        ProcessStepRunStatus completionStatus,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
        var writeCoordinator = new ProcessArtifactProjectionWriteCoordinator(
            storagePlacementService,
            RecordArtifactAsync);
        var recordOnlyCoordinator = new ProcessArtifactProjectionRecordOnlyCoordinator(RecordArtifactAsync);
        var context = new ArtifactProjectionCoordinatorContext(
            candidate,
            detail,
            responseText,
            workspaceRoot,
            workspaceScope,
            writeCoordinator,
            recordOnlyCoordinator,
            logger,
            completionStatus,
            cancellationToken,
            lineage);

        var executionCoordinator = new ProcessExecutionArtifactProjectionCoordinator(this);
        var processMockCoordinator = new ProcessMockArtifactProjectionCoordinator(this);
        var workspaceWrittenCoordinator = new ProcessWorkspaceWrittenArtifactProjectionCoordinator(this);
        var existingManagedCoordinator = new ProcessExistingManagedArtifactProjectionCoordinator(this);
        var responseTextCoordinator = new ProcessResponseTextArtifactProjectionCoordinator(this, existingManagedCoordinator);
        var providerNativeBrowserCoordinator = new ProcessProviderNativeBrowserArtifactProjectionCoordinator(this);
        var completedDecisionCoordinator = new ProcessCompletedDecisionArtifactCoordinator(this);

        await executionCoordinator.ProjectAsync(context, dispatchClaim);
        await processMockCoordinator.ProjectAsync(context);
        await workspaceWrittenCoordinator.ProjectAsync(context);
        await existingManagedCoordinator.ProjectAsync(context);
        await responseTextCoordinator.ProjectAsync(context);
        await providerNativeBrowserCoordinator.ProjectAsync(context);
        await completedDecisionCoordinator.ProjectAsync(context);
    }

    private static IReadOnlyList<string> ResolveSuccessfulWorkspaceFileMutationReceiptPaths(ProcessAutomationExecutionRunDetail detail)
    {
        return ProcessAutomationReceiptObservationHelper.ResolveSuccessfulReceipts(detail)
            .Where(IsSuccessfulWorkspaceFileMutationReceipt)
            .SelectMany(ResolveManagedWorkspacePathsFromReceipt)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ResolveExpectedManagedArtifactRelativePaths(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        DispatchArtifactExpectation expectedArtifact)
    {
        var paths = new List<string>();
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var declaredRelativePath))
        {
            AddManagedArtifactPath(paths, workspaceScope, declaredRelativePath);
        }

        if (CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact))
        {
            AddManagedArtifactPath(
                paths,
                workspaceScope,
                BuildFallbackResponseTextArtifactRelativePath(candidate, expectedArtifact));
        }

        return paths;
    }

    private static void AddManagedArtifactPath(
        ICollection<string> paths,
        WorkspaceScopeDescriptor workspaceScope,
        string relativePath)
    {
        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            IsExternalTargetAliasPath(normalizedPath))
        {
            return;
        }

        var scopedPath = ResolveScopedManagedRelativePath(workspaceScope, normalizedPath);
        if (string.IsNullOrWhiteSpace(scopedPath) ||
            paths.Contains(scopedPath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        paths.Add(scopedPath);
    }

    private static bool ExistingManagedArtifactFileMatches(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        DispatchArtifactExpectation expectedArtifact,
        string workspaceRoot,
        string relativePath)
    {
        if (!TryResolveArtifactFullPath(workspaceRoot, relativePath, out var fullPath, out _) ||
            !File.Exists(fullPath))
        {
            return false;
        }

        string? textContent = null;
        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length is > 0 and <= 512 * 1024)
            {
                var bytes = File.ReadAllBytes(fullPath);
                textContent = TryDecodeTextArtifactContent(
                    new ProcessAutomationExecutionArtifact(
                        Guid.Empty,
                        Guid.Empty,
                        "generated-output",
                        expectedArtifact.Title,
                        relativePath,
                        GuessContentTypeFromPath(fullPath),
                        "managed-workspace-file",
                        "Existing managed workspace artifact.",
                        DateTimeOffset.MinValue),
                    fullPath,
                    bytes);
            }
        }
        catch (Exception)
        {
            textContent = null;
        }

        var syntheticArtifact = new ProcessAutomationExecutionArtifact(
            Guid.Empty,
            Guid.Empty,
            "generated-output",
            expectedArtifact.Title,
            relativePath,
            GuessContentTypeFromPath(fullPath),
            "managed-workspace-file",
            "Existing managed workspace artifact.",
            DateTimeOffset.MinValue);
        var matchedExpectationId = MatchExpectedArtifactId(expectedArtifacts, syntheticArtifact, textContent);
        return matchedExpectationId == expectedArtifact.Id;
    }

    private static bool HasProjectedArtifactExpectationExternalReference(
        IEnumerable<string> externalReferenceKeys,
        Guid artifactExpectationId)
    {
        var marker = $"|{artifactExpectationId:D}|";
        var suffix = $"|{artifactExpectationId:D}";
        return externalReferenceKeys.Any(key =>
            !string.IsNullOrWhiteSpace(key) &&
            (key.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
             key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
    }

    private static string ResolveProviderNativeBrowserProjectedRelativePath(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        string normalizedOutputPath)
    {
        if (IsManagedBrowserArtifactPath(normalizedOutputPath))
        {
            return ResolveScopedManagedRelativePath(workspaceScope, normalizedOutputPath);
        }

        var fileName = Path.GetFileName(normalizedOutputPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "browser-proof";
        }

        return ResolveScopedManagedRelativePath(
            workspaceScope,
            WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(
                BuildCurrentRunManagedArtifactRoot(candidate),
                "browser",
                fileName)));
    }

    private static bool IsManagedBrowserArtifactPath(string relativePath)
    {
        var comparablePath = NormalizeManagedRelativePathForComparison(
            WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath));
        return comparablePath.StartsWith("artifacts/process-runs/", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildProviderNativeBrowserArtifactTitle(ProcessAutomationExecutionArtifact artifact)
    {
        var normalizedToolName = NormalizeToolToken(artifact.ProducedBy);
        return normalizedToolName switch
        {
            "browser_take_screenshot" => "Browser screenshot",
            "browser_snapshot" => "Browser snapshot",
            "browser_console_messages" => "Browser console log",
            "browser_evaluate" => "Browser DOM or state proof",
            _ => BuildArtifactTitle(artifact)
        };
    }

    private static bool IsProviderNativeBrowserArtifactPath(string relativePath)
        => ProcessArtifactProviderNativeVisualValidationRules.IsProviderNativeBrowserArtifactPath(relativePath);

    private void EnsureProviderNativeBrowserOutputDirectories(DispatchCandidate candidate)
    {
        if (!RequiresConcreteBrowserProof(candidate))
        {
            return;
        }

        var workspaceRoot = workspacePathResolver.ResolveWorkspaceRoot();
        foreach (var relativeDirectory in ResolveProviderNativeBrowserOutputDirectoryPaths(
                     BuildCurrentRunManagedArtifactRoot(candidate),
                     candidate.ExpectedArtifacts))
        {
            var fullPath = Path.GetFullPath(Path.Combine(
                workspaceRoot,
                relativeDirectory.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, fullPath))
            {
                logger.LogWarning(
                    "Skipping provider-native browser output directory preflight for process run {RunId}; path '{RelativeDirectory}' resolves outside the workspace root.",
                    candidate.Run.Id,
                    relativeDirectory);
                continue;
            }

            Directory.CreateDirectory(fullPath);
        }
    }

    internal static IReadOnlyList<string> ResolveProviderNativeBrowserOutputDirectoryPaths(
        string currentRunManagedArtifactRoot,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        var directories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddProviderNativeBrowserDirectory(directories, currentRunManagedArtifactRoot);

        foreach (var expectedArtifact in expectedArtifacts)
        {
            if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath) ||
                string.IsNullOrWhiteSpace(ResolveProviderNativeBrowserToolName(expectedRelativePath)))
            {
                continue;
            }

            AddProviderNativeBrowserDirectory(
                directories,
                Path.GetDirectoryName(expectedRelativePath.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty);
        }

        return directories.ToList();
    }

    private static void AddProviderNativeBrowserDirectory(
        ISet<string> directories,
        string relativeDirectory)
    {
        var normalizedDirectory = WorkspaceScopeDescriptor.NormalizeRelativePath(relativeDirectory);
        if (string.IsNullOrWhiteSpace(normalizedDirectory) ||
            Path.IsPathRooted(normalizedDirectory) ||
            IsExternalTargetAliasPath(normalizedDirectory))
        {
            return;
        }

        var segments = normalizedDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || !IsManagedRootSegment(segments[0]))
        {
            return;
        }

        directories.Add(normalizedDirectory);
    }

    private async Task<Result> TransitionStepAsync(
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.TransitionStepAsync(request, cancellationToken);
    }

    private async Task<Result> TransitionStepWithClaimAsync(
        ProcessStepTransitionRequest request,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        return await TransitionStepAsync(request, cancellationToken);
    }

    private async Task<StepRunTransitionSnapshot?> LoadStepRunTransitionSnapshotAsync(
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.Id == stepRunId)
            .Select(item => new StepRunTransitionSnapshot(item.Id, item.Status, item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Result<Guid>> RecordArtifactAsync(
        ProcessArtifactRecordRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.RecordArtifactAsync(request, cancellationToken);
    }

    private static string ApplyArtifactProjectionLineage(
        string externalReferenceKey,
        Guid executionRunId,
        ArtifactProjectionLineage? lineage)
        => ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
            externalReferenceKey,
            executionRunId,
            CreateArtifactRecoveryProjectionContext(lineage));

    private static ProcessArtifactProjectionLineage BuildArtifactProjectionLineage(
        ProcessArtifactProjectionSourceKind sourceKind,
        Guid? sourceExecutionRunId = null,
        ArtifactProjectionLineage? lineage = null,
        Guid? sourceArtifactId = null,
        string sourceExternalReferenceKey = "")
        => ProcessArtifactProjectionLineageBuilder.BuildLineage(
            sourceKind,
            sourceExecutionRunId,
            CreateArtifactRecoveryProjectionContext(lineage),
            sourceArtifactId,
            sourceExternalReferenceKey);

    private static string BuildArtifactProjectionProvenance(
        string baseProvenance,
        Guid executionRunId,
        ArtifactProjectionLineage? lineage)
        => ProcessArtifactProjectionLineageBuilder.BuildProvenance(
            baseProvenance,
            executionRunId,
            CreateArtifactRecoveryProjectionContext(lineage));

    private static ProcessArtifactRecoveryProjectionContext CreateArtifactRecoveryProjectionContext(ArtifactProjectionLineage? lineage)
    {
        return lineage is null
            ? ProcessArtifactRecoveryProjectionContext.None
            : new ProcessArtifactRecoveryProjectionContext(
                lineage.RecoveryExecutionRunId,
                lineage.RecoveredForExecutionRunId,
                lineage.ReworkPacketId);
    }

}
