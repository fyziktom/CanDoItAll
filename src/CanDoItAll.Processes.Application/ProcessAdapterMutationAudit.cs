using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Git;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessAdapterMutationAuditService
{
    public async Task<ProcessAdapterMutationAuditReport> AuditAsync(
        GitRepositoryClient git,
        ProcessAdapterMutationAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.PreExecutionStatusShort))
        {
            return new ProcessAdapterMutationAuditReport(
                ProcessAdapterMutationAuditOutcome.DirtyBaselinePreventedAudit,
                [],
                string.Empty,
                "Mutation audit cannot run against a dirty baseline.");
        }

        var status = await git.StatusAsync(cancellationToken);
        if (!status.Succeeded)
        {
            return new ProcessAdapterMutationAuditReport(
                ProcessAdapterMutationAuditOutcome.GitUnavailable,
                [
                    new ProcessAdapterMutationAuditFinding(
                        string.Empty,
                        ProcessAdapterMutationAuditFindingKind.GitCommandFailed,
                        status.SanitizedCommand)
                ],
                string.Empty,
                "Mutation audit could not read repository status.");
        }

        var entries = ParseStatusEntries(status.StandardOutput);
        if (entries.Count == 0)
        {
            return new ProcessAdapterMutationAuditReport(
                ProcessAdapterMutationAuditOutcome.NoChanges,
                [],
                string.Empty,
                "No file mutations were detected.");
        }

        var findings = new List<ProcessAdapterMutationAuditFinding>();
        foreach (var entry in entries)
        {
            if (IsInScope(entry.RepositoryRelativePath, request.ForbiddenScopes) && entry.IsDeletion)
            {
                findings.Add(new ProcessAdapterMutationAuditFinding(
                    entry.RepositoryRelativePath,
                    ProcessAdapterMutationAuditFindingKind.ForbiddenDeletion,
                    "Forbidden path was deleted."));
                continue;
            }

            if (!IsInScope(entry.RepositoryRelativePath, request.AllowedScopes))
            {
                findings.Add(new ProcessAdapterMutationAuditFinding(
                    entry.RepositoryRelativePath,
                    ProcessAdapterMutationAuditFindingKind.UnauthorizedPathMutation,
                    "Changed path is outside the adapter mutation scope."));
            }
        }

        var diff = await git.DiffAsync(cancellationToken: cancellationToken);
        var restrictedDiffReference = diff.Succeeded
            ? CreateRestrictedDiffReference(diff)
            : string.Empty;

        if (findings.Count > 0)
        {
            var outcome = findings.Any(finding => finding.Kind == ProcessAdapterMutationAuditFindingKind.ForbiddenDeletion)
                ? ProcessAdapterMutationAuditOutcome.ForbiddenDeletion
                : ProcessAdapterMutationAuditOutcome.UnauthorizedPathMutation;

            return new ProcessAdapterMutationAuditReport(
                outcome,
                findings,
                restrictedDiffReference,
                "Adapter file mutations require manager review.");
        }

        return new ProcessAdapterMutationAuditReport(
            ProcessAdapterMutationAuditOutcome.AllowedChangesOnly,
            [],
            restrictedDiffReference,
            "Only allowed adapter file mutations were detected.");
    }

    private static IReadOnlyList<GitStatusEntry> ParseStatusEntries(string statusOutput)
    {
        var entries = new List<GitStatusEntry>();
        foreach (var rawLine in statusOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawLine.Length < 4)
            {
                continue;
            }

            var code = rawLine[..2];
            var path = rawLine[3..].Trim();
            var renameSeparatorIndex = path.LastIndexOf(" -> ", StringComparison.Ordinal);
            if (renameSeparatorIndex >= 0)
            {
                path = path[(renameSeparatorIndex + 4)..].Trim();
            }

            entries.Add(new GitStatusEntry(
                NormalizePath(path),
                code.Contains('D', StringComparison.Ordinal)));
        }

        return entries;
    }

    private static bool IsInScope(
        string repositoryRelativePath,
        IReadOnlyList<ProcessAdapterMutationScope> scopes)
    {
        foreach (var scope in scopes)
        {
            if (scope.RepositoryRelativePathPrefix == "." ||
                string.Equals(repositoryRelativePath, scope.RepositoryRelativePathPrefix, StringComparison.OrdinalIgnoreCase) ||
                repositoryRelativePath.StartsWith(scope.RepositoryRelativePathPrefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    private static string CreateRestrictedDiffReference(GitCommandResult diff)
    {
        var bytes = Encoding.UTF8.GetBytes(diff.StandardOutput + "\n" + diff.StandardError);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed record GitStatusEntry(
        string RepositoryRelativePath,
        bool IsDeletion);
}

public sealed record ProcessAdapterMutationAuditRequest(
    ProcessRunId RunId,
    ProcessStepInstanceId? StepId,
    IReadOnlyList<ProcessAdapterMutationScope> AllowedScopes,
    IReadOnlyList<ProcessAdapterMutationScope> ForbiddenScopes,
    string PreExecutionStatusShort);

public sealed record ProcessAdapterMutationScope
{
    public ProcessAdapterMutationScope(string repositoryRelativePathPrefix)
    {
        if (string.IsNullOrWhiteSpace(repositoryRelativePathPrefix))
        {
            throw new ArgumentException("Mutation scope cannot be empty.", nameof(repositoryRelativePathPrefix));
        }

        RepositoryRelativePathPrefix = repositoryRelativePathPrefix.Replace('\\', '/').Trim('/');
        if (RepositoryRelativePathPrefix.Length == 0)
        {
            RepositoryRelativePathPrefix = ".";
        }
    }

    public string RepositoryRelativePathPrefix { get; }
}

public sealed record ProcessAdapterMutationAuditReport(
    ProcessAdapterMutationAuditOutcome Outcome,
    IReadOnlyList<ProcessAdapterMutationAuditFinding> Findings,
    string RestrictedDiffReference,
    string SafeSummary);

public sealed record ProcessAdapterMutationAuditFinding(
    string RepositoryRelativePath,
    ProcessAdapterMutationAuditFindingKind Kind,
    string SafeSummary);

public enum ProcessAdapterMutationAuditOutcome
{
    NoChanges,
    AllowedChangesOnly,
    UnauthorizedPathMutation,
    ForbiddenDeletion,
    DirtyBaselinePreventedAudit,
    GitUnavailable
}

public enum ProcessAdapterMutationAuditFindingKind
{
    UnauthorizedPathMutation,
    ForbiddenDeletion,
    GitCommandFailed
}
