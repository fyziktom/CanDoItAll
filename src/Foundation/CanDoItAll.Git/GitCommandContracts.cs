namespace CanDoItAll.Git;

public sealed record GitCommandArgument(string Value, bool IsSensitive = false);

public sealed record GitCommandSpec(
    GitRepositoryPath RepositoryPath,
    IReadOnlyList<GitCommandArgument> Arguments)
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(2);

    public string Executable => "git";

    public string SanitizedCommand => GitCommandLogSanitizer.Sanitize(this);
}

public sealed record GitCommandResult(
    bool Succeeded,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string SanitizedCommand);

public interface IGitCommandExecutor
{
    Task<GitCommandResult> ExecuteAsync(GitCommandSpec spec, CancellationToken cancellationToken = default);
}

public static class GitCommandLogSanitizer
{
    public static string Sanitize(GitCommandSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var parts = new List<string> { spec.Executable };
        foreach (var argument in spec.Arguments)
        {
            parts.Add(argument.IsSensitive ? "***" : Quote(argument.Value));
        }

        return string.Join(" ", parts);
    }

    private static string Quote(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}
