using CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static SoftwareDeliveryProofPolicyRequest CreateSoftwareDeliveryProofPolicyRequest(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        CarriedImplementationProof carriedProof,
        DateTimeOffset requestedAtUtc,
        string projectStructureGroundingSummary = "")
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(detail);

        var toolReceipts = MapSoftwareDeliveryToolReceipts(detail.ToolReceipts);
        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        var runnableDotNetProjectPaths = ResolveRunnableDotNetHostProjectPaths(detail, successfulReceipts);
        return new SoftwareDeliveryProofPolicyRequest(
            CreateSoftwareDeliveryImplementationContractSnapshot(candidate),
            CreateSoftwareDeliveryPathFacts(
                candidate,
                toolReceipts),
            CreateSoftwareDeliveryExternalTargetSnapshot(candidate, detail.Run, projectStructureGroundingSummary),
            toolReceipts,
            candidate.ExpectedArtifacts
                .Select(CreateSoftwareDeliveryArtifactExpectationSnapshot)
                .ToList(),
            detail.Artifacts
                .Select(CreateSoftwareDeliveryArtifactRecordSnapshot)
                .ToList(),
            CreateSoftwareDeliveryBrowserEvidenceSnapshot(candidate),
            new SoftwareDeliveryRunnableHostSnapshot(
                runnableDotNetProjectPaths,
                ResolveInvalidRunnableDotNetHostSummary(runnableDotNetProjectPaths)),
            new SoftwareDeliveryCarriedProofSnapshot(
                carriedProof.HasConcreteImplementationProof,
                carriedProof.HasRunnableApplicationProof,
                carriedProof.HasConcreteProductMutation,
                detail.Run.Id.ToString("D"),
                carriedProof == CarriedImplementationProof.None
                    ? string.Empty
                    : "Concrete proof was carried from prior process execution facts."),
            ResolveRequiredToolNames(candidate)
                .Select(SoftwareDeliveryEvidencePolicy.NormalizeToolToken)
                .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
                .Any(projection => CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection)),
            requestedAtUtc);
    }

    private static SoftwareDeliveryImplementationContractSnapshot CreateSoftwareDeliveryImplementationContractSnapshot(
        DispatchCandidate candidate,
        string? additionalContext = null,
        bool? requiresConcreteBrowserProof = null)
    {
        var contract = CreateSoftwareDeliveryContractText(candidate, additionalContext);
        return new SoftwareDeliveryImplementationContractSnapshot(
            contract.ContractText,
            contract.TriggerText,
            contract.AdditionalGroundingText,
            RequiresConcreteImplementationProof(candidate),
            RequiresConcreteImplementationReview(candidate),
            requiresConcreteBrowserProof ?? RequiresConcreteBrowserProof(candidate),
            UsesScaffoldContractDrivenSetup(candidate),
            IsDotNetSolutionSetupScaffoldMutationStep(candidate));
    }

    private static bool LooksLikeExternalArtifactDestination(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary)
    {
        var context = string.Join(
            '\n',
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.StepDefinition.InputContractSummary,
            candidate.StepDefinition.OutputContractSummary,
            candidate.StepDefinition.EvidenceContractSummary,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            projectStructureGroundingSummary);

        return ContainsAnyArtifactDestinationSignal(
            context,
            [
                "artifact destination",
                "deliverable artifact",
                "document output",
                "report output",
                "plan output",
                "handoff folder",
                "business plan",
                "marketing plan",
                "financial model",
                "strategy brief",
                "research report",
                "analysis report",
                "decision package"
            ]) &&
            !ContainsAnyArtifactDestinationSignal(
                context,
                [
                    "product root",
                    "generated app source",
                    "app source belongs",
                    ".sln",
                    ".csproj",
                    "solution name",
                    "app project",
                    "test project",
                    "console app",
                    "blazor",
                    "razor",
                    "asp.net",
                    "javascript browser app",
                    "static javascript",
                    "index.html",
                    "app.js",
                    "package.json"
                ]);
    }

    private static bool UsesScaffoldContractDrivenSetup(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.ArtifactInputs.Any(ReferencesScaffoldContract))
        {
            return true;
        }

        var context = string.Join(
            '\n',
            candidate.Definition.Name,
            candidate.StepRun.Title,
            candidate.StepDefinition.InputContractSummary,
            candidate.StepDefinition.OutputContractSummary,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary);

        return context.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase) ||
               context.Contains(".NET solution setup subprocess", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReferencesScaffoldContract(DispatchArtifactInput artifactInput)
    {
        if (artifactInput.ExpectedArtifactTitle.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase) ||
            artifactInput.SourceStepTitle.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return artifactInput.Artifacts.Any(artifact =>
            artifact.Title.Contains("scaffold contract", StringComparison.OrdinalIgnoreCase) ||
            artifact.ManagedStoragePath.EndsWith("scaffold-contract.md", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAnyArtifactDestinationSignal(string text, IReadOnlyCollection<string> needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasScaffoldOverwriteConflict(
        ProcessAutomationExecutionRunDetail detail,
        string? responseText)
    {
        if (ContainsScaffoldOverwriteConflictSignal(responseText))
        {
            return true;
        }

        return detail.ToolReceipts.Any(receipt =>
            string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_new", StringComparison.Ordinal) &&
            (ContainsScaffoldOverwriteConflictSignal(receipt.ExitSummary) ||
             ContainsScaffoldOverwriteConflictSignal(receipt.RequestSummary)));
    }

    private static bool ContainsScaffoldOverwriteConflictSignal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("overwrite conflict", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("files already exist", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("files already existed", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("would overwrite", StringComparison.OrdinalIgnoreCase);
    }

    private static SoftwareDeliveryPathFacts CreateSoftwareDeliveryPathFacts(
        DispatchCandidate candidate,
        IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> toolReceipts)
    {
        var workspacePaths = toolReceipts
            .SelectMany(receipt => receipt.WorkspacePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var outputFiles = toolReceipts
            .SelectMany(receipt => receipt.OutputFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SoftwareDeliveryPathFacts(
            workspacePaths,
            outputFiles,
            [],
            [BuildCurrentRunManagedArtifactRoot(candidate)],
            [BuildCurrentRunManagedOutputRoot(candidate)]);
    }

    private static SoftwareDeliveryExternalTargetSnapshot CreateSoftwareDeliveryExternalTargetSnapshot(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunRecord run,
        string projectStructureGroundingSummary)
    {
        var groundedTarget = ProcessExternalTargetGroundingService.ResolveProjectStructureGroundingTarget(
            projectStructureGroundingSummary);

        return new SoftwareDeliveryExternalTargetSnapshot(
            ResolveAllowedExternalTargetAliases(run),
            groundedTarget.MappedAlias,
            groundedTarget.AbsolutePath,
            groundedTarget.HasTarget,
            groundedTarget.ScaffoldTarget is not null,
            BuildCurrentRunManagedArtifactRoot(candidate),
            BuildCurrentRunManagedOutputRoot(candidate));
    }

    private static SoftwareDeliveryBrowserEvidenceSnapshot CreateSoftwareDeliveryBrowserEvidenceSnapshot(
        DispatchCandidate candidate)
    {
        return new SoftwareDeliveryBrowserEvidenceSnapshot(
            RequiresConcreteBrowserProof(candidate),
            HasCurrentRunBrowserEvidence: false,
            HasConsoleErrorEvidence: false,
            Routes: [],
            ArtifactPaths: [],
            Summary: string.Empty);
    }

    private static IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> MapSoftwareDeliveryToolReceipts(
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> receipts)
    {
        return receipts
            .Select(CreateSoftwareDeliveryToolReceiptSnapshot)
            .ToList();
    }

    private static SoftwareDeliveryToolReceiptSnapshot CreateSoftwareDeliveryToolReceiptSnapshot(
        ProcessAutomationToolExecutionReceipt receipt)
    {
        return new SoftwareDeliveryToolReceiptSnapshot(
            receipt.ToolName,
            receipt.StartedAtUtc,
            receipt.CompletedAtUtc,
            !IsFailedToolReceipt(receipt),
            receipt.RequestSummary,
            receipt.WorkingDirectory,
            receipt.ExitSummary,
            ResolveSoftwareDeliveryWorkspacePathsFromReceipt(receipt),
            []);
    }

    private static SoftwareDeliveryContractText CreateSoftwareDeliveryContractText(
        DispatchCandidate candidate,
        string? additionalContext = null)
    {
        var additionalGroundingText = string.Join(' ', candidate.Definition.Summary, candidate.StepDefinition.Notes).Trim();
        var contractTextParts = new[]
            {
                candidate.StepRun.Title,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.EvidenceExpectationSummary,
                additionalGroundingText,
                additionalContext
            }
            .Concat(candidate.ExpectedArtifacts.Select(item => item.Title))
            .Concat(candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary));
        var triggerText = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var triggerTextParts = new[]
        {
            triggerText,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary
        };

        return new SoftwareDeliveryContractText(
            CollapseSoftwareDeliveryPromptWhitespace(string.Join(' ', contractTextParts)),
            CollapseSoftwareDeliveryPromptWhitespace(string.Join(' ', triggerTextParts)),
            additionalGroundingText);
    }

    private static string CollapseSoftwareDeliveryPromptWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static IReadOnlyList<string> ResolveSoftwareDeliveryWorkspacePathsFromReceipt(
        ProcessAutomationToolExecutionReceipt receipt)
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in SoftwareDeliveryPathRules.ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary))
        {
            paths.Add(path);
        }

        if (SoftwareDeliveryPathRules.TryMapWorkspacePathForPrompt(receipt.WorkingDirectory, out var workingDirectory))
        {
            paths.Add(workingDirectory);
        }

        return paths.ToList();
    }

    private static SoftwareDeliveryArtifactExpectationSnapshot CreateSoftwareDeliveryArtifactExpectationSnapshot(
        DispatchArtifactExpectation expectation)
    {
        return new SoftwareDeliveryArtifactExpectationSnapshot(
            expectation.Id.ToString("D"),
            expectation.Title,
            expectation.IsRequired,
            expectation.ValidationRequirementSummary,
            ExpectedPath: string.Empty,
            expectation.ArtifactKind.ToString());
    }

    private static SoftwareDeliveryArtifactRecordSnapshot CreateSoftwareDeliveryArtifactRecordSnapshot(
        ProcessAutomationExecutionArtifact artifact)
    {
        return new SoftwareDeliveryArtifactRecordSnapshot(
            artifact.Id.ToString("D"),
            artifact.DisplayName,
            artifact.RelativePath,
            artifact.ContentType,
            artifact.ProducedBy,
            artifact.Summary,
            artifact.CreatedAtUtc);
    }

    private sealed record SoftwareDeliveryContractText(
        string ContractText,
        string TriggerText,
        string AdditionalGroundingText);
}
