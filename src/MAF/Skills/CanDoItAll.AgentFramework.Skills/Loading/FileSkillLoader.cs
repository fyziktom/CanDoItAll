using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Skills.Abstractions;

namespace CanDoItAll.AgentFramework.Skills;

public sealed class FileSkillLoader(string workspaceRoot, int maxSkillFileBytes = 128 * 1024) : IFileSkillLoader
{
    private const int MaxDescriptionLength = 512;

    private readonly string workspaceRoot = Path.GetFullPath(ExpandPortablePath(workspaceRoot));
    private readonly int maxSkillFileBytes = Math.Max(1024, maxSkillFileBytes);

    public async Task<SkillLoadResult> LoadAsync(
        FileSkillDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (descriptor.AvailabilityState != CapabilityAvailabilityState.Available)
        {
            return Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.CapabilityUnavailable,
                "$.availabilityState",
                $"File skill '{descriptor.Identity.Key}' is {descriptor.AvailabilityState}.",
                "Enable or replace the file skill before loading it.");
        }

        var policyResult = ResolveSkillFile(descriptor, correlationId);
        if (!policyResult.IsSuccess)
        {
            return policyResult.Failure ?? throw new InvalidOperationException("Skill file policy failure did not include a diagnostic.");
        }

        var skillFilePath = policyResult.SkillFilePath!;
        var fileInfo = new FileInfo(skillFilePath);
        if (fileInfo.Length > maxSkillFileBytes)
        {
            return ValidationFailure(
                descriptor,
                correlationId,
                "$.skillRoot",
                $"Skill file '{skillFilePath}' is {fileInfo.Length} bytes, which exceeds the {maxSkillFileBytes} byte limit.",
                "Shorten the skill activation document or move bulk context into resources.");
        }

        string content;
        try
        {
            content = await File.ReadAllTextAsync(skillFilePath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValidationFailure(
                descriptor,
                correlationId,
                "$.skillRoot",
                $"Skill file '{skillFilePath}' could not be read. {exception.GetType().Name}: {exception.Message}",
                "Check file permissions and retry.");
        }

        var metadata = SkillMarkdownParser.Parse(content);
        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            return ValidationFailure(
                descriptor,
                correlationId,
                "$.skill.name",
                $"Skill file '{skillFilePath}' is missing required name metadata.",
                "Add a non-empty 'name:' entry to SKILL.md front matter.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Description) || metadata.Description.Length > MaxDescriptionLength)
        {
            return ValidationFailure(
                descriptor,
                correlationId,
                "$.skill.description",
                $"Skill file '{skillFilePath}' is missing required description metadata or the description is too long.",
                $"Add a description between 1 and {MaxDescriptionLength} characters to SKILL.md front matter.");
        }

        return SkillLoadResult.Success(new LoadedSkill(
            descriptor.Identity,
            SkillDescriptorKind.File,
            metadata.Name,
            metadata.Description,
            metadata.Instructions,
            [],
            skillFilePath,
            null,
            descriptor.ScriptExecutionPolicy), correlationId);
    }

    private SkillFilePolicyResult ResolveSkillFile(FileSkillDescriptor descriptor, string correlationId)
    {
        var expandedSkillRoot = ExpandPortablePath(descriptor.SkillRoot);
        var configuredPath = Path.GetFullPath(Path.IsPathRooted(expandedSkillRoot)
            ? expandedSkillRoot
            : Path.Combine(workspaceRoot, expandedSkillRoot));
        var skillRootPath = Path.GetFileName(configuredPath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(configuredPath) ?? configuredPath
            : configuredPath;

        if (!IsPathWithinRoot(skillRootPath, workspaceRoot))
        {
            var allowedRoots = descriptor.AllowedExternalRoots
                .Select(ExpandPortablePath)
                .Select(root => Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(workspaceRoot, root)))
                .ToArray();
            if (allowedRoots.Length == 0 || !allowedRoots.Any(allowedRoot => IsPathWithinRoot(skillRootPath, allowedRoot)))
            {
                return SkillFilePolicyResult.FailureResult(Failure(
                    descriptor,
                    correlationId,
                    CapabilityDiagnosticCategory.CommandPolicy,
                    "$.allowedExternalRoots",
                    $"Skill root '{SkillDiagnostics.Bound(descriptor.SkillRoot, 160)}' resolves outside the workspace root.",
                    "Add the external skill root to allowedExternalRoots or move the skill under the workspace."));
            }
        }

        var skillFilePath = Path.GetFileName(configuredPath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
            ? configuredPath
            : Path.Combine(configuredPath, "SKILL.md");

        if (!File.Exists(skillFilePath))
        {
            return SkillFilePolicyResult.FailureResult(ValidationFailure(
                descriptor,
                correlationId,
                "$.skillRoot",
                $"Skill file '{skillFilePath}' was not found.",
                "Create SKILL.md in the configured skill root or update the skillRoot path."));
        }

        return SkillFilePolicyResult.SuccessResult(skillFilePath);
    }

    private static bool IsPathWithinRoot(string candidatePath, string rootPath)
    {
        var normalizedCandidate = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpandPortablePath(string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    private static SkillLoadResult ValidationFailure(
        FileSkillDescriptor descriptor,
        string correlationId,
        string fieldPath,
        string detail,
        string repairHint)
    {
        return Failure(
            descriptor,
            correlationId,
            CapabilityDiagnosticCategory.TemplateValidation,
            fieldPath,
            detail,
            repairHint);
    }

    private static SkillLoadResult Failure(
        FileSkillDescriptor descriptor,
        string correlationId,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string detail,
        string repairHint)
    {
        return SkillLoadResult.Failure(correlationId,
        [
            SkillDiagnostics.Create(
                category,
                descriptor,
                fieldPath,
                detail,
                repairHint,
                correlationId,
                CapabilityTransportKind.FileSkill)
        ]);
    }

    private sealed record SkillFilePolicyResult(
        bool IsSuccess,
        string? SkillFilePath,
        SkillLoadResult? Failure)
    {
        public static SkillFilePolicyResult SuccessResult(string skillFilePath)
            => new(true, skillFilePath, null);

        public static SkillFilePolicyResult FailureResult(SkillLoadResult result)
            => new(false, null, result);
    }
}
