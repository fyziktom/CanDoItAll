using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static bool RequiresCurrentAttemptProductMutation(DispatchCandidate candidate)
    {
        return SoftwareDeliveryContractRules.RequiresCurrentAttemptProductMutation(
            CreateSoftwareDeliveryContractSnapshot(candidate));
    }

    private static bool HasConcreteImplementationProofEvidence(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return EvaluateSoftwareDeliveryEvidence(candidate, detail).HasConcreteImplementationProofEvidence;
    }

    private static bool HasSuccessfulConcreteProductMutation(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail)
    {
        return EvaluateSoftwareDeliveryEvidence(candidate, detail).HasSuccessfulConcreteProductMutation;
    }

    private static ProcessAutomationToolExecutionReceipt? ResolveLatestImplementationProofReadReceipt(
        DispatchCandidate candidate,
        IEnumerable<ProcessAutomationToolExecutionReceipt> successfulReceipts)
    {
        var requiresSourceOrProjectImplementationProof = RequiresSourceOrProjectImplementationProof(candidate);
        return successfulReceipts
            .Where(receipt => string.Equals(
                SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName),
                "workspace_read_file",
                StringComparison.Ordinal))
            .Where(receipt => SoftwareDeliveryPathRules.HasConcreteProductPath(
                CreateSoftwareDeliveryToolReceiptSnapshot(receipt)))
            .Where(receipt => SoftwareDeliveryPathRules.HasConcreteProductImplementationPath(
                requiresSourceOrProjectImplementationProof,
                CreateSoftwareDeliveryToolReceiptSnapshot(receipt)))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static bool HasBuildValidationReceipt(IReadOnlyList<ProcessAutomationToolExecutionReceipt> successfulReceipts)
    {
        return SoftwareDeliveryReceiptTimeline.HasBuildValidationReceipt(
            successfulReceipts
                .Select(CreateSoftwareDeliveryToolReceiptSnapshot)
                .ToList());
    }

    private static bool IsBuildValidationToolName(string normalizedToolName)
    {
        return SoftwareDeliveryReceiptTimeline.IsBuildValidationToolName(normalizedToolName);
    }

    private static bool IsRunValidationToolName(string normalizedToolName)
    {
        return SoftwareDeliveryReceiptTimeline.IsRunValidationToolName(normalizedToolName);
    }

    private static bool ContainsRunnableApplicationContractSignal(DispatchCandidate candidate)
    {
        return SoftwareDeliveryContractRules.ContainsRunnableApplicationContractSignal(
            CreateSoftwareDeliveryContractSnapshot(candidate));
    }

    private static bool ImplementationContractMentionsTests(DispatchCandidate candidate)
    {
        return SoftwareDeliveryContractRules.ImplementationContractMentionsTests(
            CreateSoftwareDeliveryContractSnapshot(candidate));
    }

    private static bool ContainsExplicitImplementationTestRequest(string text)
    {
        return SoftwareDeliveryContractRules.ContainsExplicitImplementationTestRequest(text);
    }

    private static bool ImplementationContractMentionsDotNet(DispatchCandidate candidate, string? additionalContext = null)
    {
        return SoftwareDeliveryContractRules.ImplementationContractMentionsDotNet(
            CreateSoftwareDeliveryContractSnapshot(candidate, additionalContext));
    }

    private static bool ImplementationContractMentionsJavaScript(DispatchCandidate candidate, string? additionalContext = null)
    {
        return SoftwareDeliveryContractRules.ImplementationContractMentionsJavaScript(
            CreateSoftwareDeliveryContractSnapshot(candidate, additionalContext));
    }

    private static bool ImplementationContractNegatesDotNet(DispatchCandidate candidate, string? additionalContext = null)
    {
        return SoftwareDeliveryContractRules.ImplementationContractNegatesDotNet(
            CreateSoftwareDeliveryContractSnapshot(candidate, additionalContext));
    }

    private static string BuildImplementationContractText(DispatchCandidate candidate, string? additionalContext = null)
    {
        return CreateSoftwareDeliveryContractText(candidate, additionalContext).ContractText;
    }

    private static bool ContainsNegatedImplementationStackToken(string text, string token)
    {
        return SoftwareDeliveryContractRules.ContainsNegatedImplementationStackToken(text, token);
    }

    private static bool ContainsAffirmativeImplementationStackToken(string text, string token)
    {
        return SoftwareDeliveryContractRules.ContainsAffirmativeImplementationStackToken(text, token);
    }

    private static bool ContainsAffirmativeImplementationStackPattern(
        string text,
        string pattern,
        RegexOptions options)
    {
        return SoftwareDeliveryContractRules.ContainsAffirmativeImplementationStackPattern(text, pattern, options);
    }

    private static bool IsNegatedImplementationStackMention(string text, int matchIndex)
    {
        return SoftwareDeliveryContractRules.IsNegatedImplementationStackMention(text, matchIndex);
    }

    private static bool ContainsContractWord(string text, string word)
    {
        return SoftwareDeliveryContractRules.ContainsContractWord(text, word);
    }

    private static string ResolveInvalidRunnableDotNetHostSummary(IReadOnlyList<string> runnableDotNetProjectPaths)
    {
        foreach (var projectPath in runnableDotNetProjectPaths)
        {
            if (!TryResolvePromptPathToFullPath(projectPath, out var fullPath))
            {
                continue;
            }

            if (TryResolveInvalidWebHostShapeSummary(fullPath, out var summary))
            {
                return summary;
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveRunnableDotNetHostProjectPaths(
        ProcessAutomationExecutionRunDetail detail,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> successfulReceipts)
    {
        var candidatePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in ResolveAllowedExternalTargetAliases(detail.Run))
        {
            AddResolvedPromptPathCandidates(candidatePaths, alias);
        }

        foreach (var receipt in successfulReceipts)
        {
            foreach (var path in SoftwareDeliveryPathRules.ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary))
            {
                AddResolvedPromptPathCandidates(candidatePaths, path);
            }

            if (SoftwareDeliveryPathRules.TryMapWorkspacePathForPrompt(receipt.WorkingDirectory, out var mappedWorkingDirectory))
            {
                AddResolvedPromptPathCandidates(candidatePaths, mappedWorkingDirectory);
            }
        }

        return candidatePaths
            .SelectMany(EnumerateCandidateDotNetProjectFiles)
            .Where(IsRunnableDotNetHostProjectFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(TryMapAbsolutePathToExternalTargetAlias)
            .ToList();
    }

    private static void AddResolvedPromptPathCandidates(
        SortedSet<string> candidatePaths,
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (!SoftwareDeliveryPathRules.IsConcreteProductPath(normalized))
        {
            return;
        }

        candidatePaths.Add(normalized);
    }

    private static IEnumerable<string> EnumerateCandidateDotNetProjectFiles(string promptPath)
    {
        if (!TryResolvePromptPathToFullPath(promptPath, out var fullPath))
        {
            yield break;
        }

        if (File.Exists(fullPath))
        {
            if (string.Equals(Path.GetExtension(fullPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                yield return fullPath;
            }

            yield break;
        }

        var searchRoot = string.Equals(Path.GetExtension(fullPath), ".sln", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(Path.GetExtension(fullPath), ".slnx", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(fullPath)
            : fullPath;
        if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
        {
            yield break;
        }

        IEnumerable<string> projectFiles;
        try
        {
            projectFiles = Directory.EnumerateFiles(searchRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(path => !HasIgnoredProjectPathSegment(path))
                .Take(32)
                .ToList();
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var projectFile in projectFiles)
        {
            yield return projectFile;
        }
    }

    private static bool TryResolvePromptPathToFullPath(string promptPath, out string fullPath)
    {
        fullPath = string.Empty;
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (SoftwareDeliveryPathRules.IsExternalTargetAliasPath(normalized))
        {
            return TryResolveExternalTargetArtifactFullPath(normalized, out fullPath, out _);
        }

        if (Path.IsPathRooted(promptPath))
        {
            fullPath = Path.GetFullPath(promptPath);
            return true;
        }

        return false;
    }

    private static bool HasIgnoredProjectPathSegment(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, ".oldruns", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "oldruns", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "old-runs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "previous-runs", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "backup", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "backups", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "archive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "archives", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRunnableDotNetHostProjectFile(string fullPath)
    {
        try
        {
            var document = XDocument.Load(fullPath, LoadOptions.None);
            var sdk = document.Root?.Attribute("Sdk")?.Value ?? string.Empty;
            if (sdk.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
                sdk.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return document
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "OutputType", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Value.Trim())
                .Any(value =>
                    string.Equals(value, "Exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "WinExe", StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static string TryMapAbsolutePathToExternalTargetAlias(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath).Replace(Path.DirectorySeparatorChar, '/');
        if (normalized.Length < 3 || normalized[1] != ':' || normalized[2] != '/')
        {
            return fullPath;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var suffix = normalized[3..].Trim('/');
        return string.IsNullOrWhiteSpace(suffix)
            ? $"external-target/{driveLetter}"
            : $"external-target/{driveLetter}/{suffix}";
    }

    private static ProcessAutomationToolExecutionReceipt? ResolveLatestRequiredImplementationValidationReceipt(
        DispatchCandidate candidate,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt> successfulReceipts)
    {
        var requiredToolNames = ResolveRequiredToolNames(candidate)
            .Select(SoftwareDeliveryEvidencePolicy.NormalizeToolToken)
            .ToHashSet(StringComparer.Ordinal);
        return successfulReceipts
            .Where(receipt =>
            {
                var normalizedToolName = SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName);
                return requiredToolNames.Contains(normalizedToolName) &&
                       SoftwareDeliveryReceiptTimeline.IsImplementationValidationToolName(normalizedToolName);
            })
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static ProcessAutomationToolExecutionReceipt? ResolveLatestReceipt(
        IEnumerable<ProcessAutomationToolExecutionReceipt> receipts,
        string normalizedToolName,
        bool requireConcreteProductPath,
        bool requireConcreteDeliverableOrSourcePath)
    {
        return ResolveLatestReceipt(
            receipts,
            toolName => string.Equals(toolName, normalizedToolName, StringComparison.Ordinal),
            requireConcreteProductPath,
            requireConcreteDeliverableOrSourcePath);
    }

    private static ProcessAutomationToolExecutionReceipt? ResolveLatestReceipt(
        IEnumerable<ProcessAutomationToolExecutionReceipt> receipts,
        Func<string, bool> matchesToolName,
        bool requireConcreteProductPath,
        bool requireConcreteDeliverableOrSourcePath)
    {
        return receipts
            .Where(receipt => matchesToolName(SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName)))
            .Where(receipt => !requireConcreteProductPath || HasConcreteProductPath(receipt))
            .Where(receipt => !requireConcreteDeliverableOrSourcePath || HasConcreteProductDeliverableOrSourcePath(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsConcreteProductMutationReceipt(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        ProcessAutomationToolExecutionReceipt receipt)
    {
        return SoftwareDeliveryPathRules.IsConcreteProductMutationReceipt(
            RequiresCurrentAttemptProductMutation(candidate),
            RequiresSourceOrProjectImplementationProof(candidate),
            ResolveAllowedExternalTargetAliases(detail.Run),
            CreateSoftwareDeliveryToolReceiptSnapshot(receipt));
    }

    private static bool IsConcreteProductMutationReceipt(
        DispatchCandidate candidate,
        ProcessAutomationToolExecutionReceipt receipt)
    {
        return SoftwareDeliveryPathRules.IsConcreteProductMutationReceipt(
            RequiresCurrentAttemptProductMutation(candidate),
            RequiresSourceOrProjectImplementationProof(candidate),
            [],
            CreateSoftwareDeliveryToolReceiptSnapshot(receipt));
    }

    private static bool IsConcreteProductMutationToolName(string normalizedToolName)
    {
        return SoftwareDeliveryReceiptTimeline.IsConcreteProductMutationToolName(normalizedToolName);
    }

    private static bool IsImplementationBootstrapToolName(string normalizedToolName)
    {
        return SoftwareDeliveryReceiptTimeline.IsImplementationBootstrapToolName(normalizedToolName);
    }

    private static bool IsImplementationValidationToolName(string normalizedToolName)
    {
        return SoftwareDeliveryReceiptTimeline.IsImplementationValidationToolName(normalizedToolName);
    }

    private static bool HasConcreteProductPath(ProcessAutomationToolExecutionReceipt receipt)
    {
        return SoftwareDeliveryPathRules.HasConcreteProductPath(CreateSoftwareDeliveryToolReceiptSnapshot(receipt));
    }

    private static bool HasConcreteProductDeliverableOrSourcePath(ProcessAutomationToolExecutionReceipt receipt)
    {
        return SoftwareDeliveryPathRules.HasConcreteProductDeliverableOrSourcePath(
            CreateSoftwareDeliveryToolReceiptSnapshot(receipt));
    }

    private static bool HasConcreteProductImplementationPath(
        DispatchCandidate candidate,
        ProcessAutomationToolExecutionReceipt receipt)
    {
        return SoftwareDeliveryPathRules.HasConcreteProductImplementationPath(
            RequiresSourceOrProjectImplementationProof(candidate),
            CreateSoftwareDeliveryToolReceiptSnapshot(receipt));
    }

    private static bool HasConcreteProductSourceOrProjectPath(ProcessAutomationToolExecutionReceipt receipt)
    {
        return SoftwareDeliveryPathRules.HasConcreteProductSourceOrProjectPath(
            CreateSoftwareDeliveryToolReceiptSnapshot(receipt));
    }

    private static IReadOnlyList<string> ResolveWorkspacePathsFromReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        return ResolveSoftwareDeliveryWorkspacePathsFromReceipt(receipt);
    }

    private static IReadOnlyList<string> ResolveWorkspacePathsFromToolRequest(string requestSummary)
    {
        return SoftwareDeliveryPathRules.ResolveWorkspacePathsFromToolRequest(requestSummary);
    }

    private static bool TryMapWorkspacePathForPrompt(string path, out string promptPath)
    {
        return SoftwareDeliveryPathRules.TryMapWorkspacePathForPrompt(path, out promptPath);
    }

    private static bool IsConcreteProductDeliverableOrSourcePath(string promptPath)
    {
        return SoftwareDeliveryPathRules.IsConcreteProductDeliverableOrSourcePath(promptPath);
    }

    private static bool IsConcreteProductSourceOrProjectPath(string promptPath)
    {
        return SoftwareDeliveryPathRules.IsConcreteProductSourceOrProjectPath(promptPath);
    }

    private static bool IsImplementationDeliverableOrSourceExtension(string extension)
    {
        return SoftwareDeliveryPathRules.IsImplementationDeliverableOrSourceExtension(extension);
    }

    private static bool IsConcreteProductPath(string promptPath)
    {
        return SoftwareDeliveryPathRules.IsConcreteProductPath(promptPath);
    }

    private static bool IsManagedProcessRunProductOutputPath(string path)
    {
        return SoftwareDeliveryPathRules.IsManagedProcessRunProductOutputPath(path);
    }

    private static bool IsManagedProcessRunProductOutputPath(IReadOnlyList<string> segments)
    {
        return SoftwareDeliveryPathRules.IsManagedProcessRunProductOutputPath(segments);
    }

    private static bool IsManagedProcessRunNonProductPathSegment(string segment)
    {
        return SoftwareDeliveryPathRules.IsManagedProcessRunNonProductPathSegment(segment);
    }

    private static bool RequiresSourceOrProjectImplementationProof(DispatchCandidate candidate)
    {
        return SoftwareDeliveryContractRules.ResolveSignals(
                CreateSoftwareDeliveryContractSnapshot(candidate))
            .RequiresSourceOrProjectImplementationProof;
    }

    private static bool IsExternalTargetAliasWithinManagedWorkspace(IReadOnlyList<string> segments)
    {
        return SoftwareDeliveryPathRules.IsExternalTargetAliasWithinManagedWorkspace(segments);
    }

    private static bool IsExternalTargetNonProductPathSegment(string segment)
    {
        return SoftwareDeliveryPathRules.IsExternalTargetNonProductPathSegment(segment);
    }

    private static bool IsNonProductPathSegment(string segment)
    {
        return SoftwareDeliveryPathRules.IsNonProductPathSegment(segment);
    }

    private static bool IsReceiptAfter(ProcessAutomationToolExecutionReceipt candidate, ProcessAutomationToolExecutionReceipt baseline)
    {
        return candidate.CompletedAtUtc > baseline.CompletedAtUtc ||
               candidate.CompletedAtUtc == baseline.CompletedAtUtc &&
               candidate.StartedAtUtc > baseline.StartedAtUtc;
    }

    private static SoftwareDeliveryImplementationContractSnapshot CreateSoftwareDeliveryContractSnapshot(
        DispatchCandidate candidate,
        string? additionalContext = null)
    {
        var contract = CreateSoftwareDeliveryContractText(candidate, additionalContext);
        return new SoftwareDeliveryImplementationContractSnapshot(
            contract.ContractText,
            contract.TriggerText,
            contract.AdditionalGroundingText,
            RequiresConcreteImplementationProof(candidate),
            RequiresConcreteImplementationReview(candidate),
            RequiresConcreteBrowserProof: false,
            UsesScaffoldContractDrivenSetup(candidate),
            IsDotNetSolutionSetupScaffoldMutationStep(candidate));
    }
}
