namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureExternalLinkClassifier
{
    public static string DescribeGitHost(string? url)
    {
        var host = ResolveHost(url);
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("github", StringComparison.OrdinalIgnoreCase))
        {
            return "GitHub";
        }

        if (host.Equals("gitlab.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".gitlab.com", StringComparison.OrdinalIgnoreCase) ||
            host.Contains("gitlab", StringComparison.OrdinalIgnoreCase))
        {
            return "GitLab";
        }

        return string.Empty;
    }

    private static string ResolveHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var candidate = url.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        var sshSeparator = candidate.IndexOf(':', StringComparison.Ordinal);
        if (candidate.Contains('@', StringComparison.Ordinal) && sshSeparator > 0)
        {
            var userHost = candidate[..sshSeparator];
            var atIndex = userHost.LastIndexOf('@');
            return atIndex >= 0 && atIndex < userHost.Length - 1
                ? userHost[(atIndex + 1)..]
                : string.Empty;
        }

        return string.Empty;
    }
}
