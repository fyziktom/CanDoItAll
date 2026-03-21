using CanDoItAll.Mcp.SshOps.Configuration;

namespace CanDoItAll.Mcp.SshOps.Security;

public sealed class RemotePathGuard
{
    public string EnsureAllowedPath(ResolvedTargetConfiguration target, string path)
    {
        var normalizedPath = NormalizePosixPath(path);
        if (target.AllowedRoots.Any(allowedRoot => IsInside(normalizedPath, allowedRoot)))
        {
            return normalizedPath;
        }

        throw new ToolInvocationException(
            "PathNotAllowed",
            $"Path '{normalizedPath}' is not allowed for target '{target.Name}'.",
            new
            {
                path = normalizedPath,
                allowedRoots = target.AllowedRoots
            });
    }

    public string ResolveInsideStacksRoot(ResolvedTargetConfiguration target, string path)
    {
        var combined = path.StartsWith("/", StringComparison.Ordinal)
            ? path
            : $"{target.StacksRoot}/{path}";
        return EnsureAllowedPath(target, combined);
    }

    public string ResolveInsideStateRoot(ResolvedTargetConfiguration target, string path)
    {
        var combined = path.StartsWith("/", StringComparison.Ordinal)
            ? path
            : $"{target.RemoteStateRoot}/{path}";
        return EnsureAllowedPath(target, combined);
    }

    private static bool IsInside(string path, string root)
    {
        return string.Equals(path, root, StringComparison.Ordinal) ||
               path.StartsWith(root + "/", StringComparison.Ordinal);
    }

    private static string NormalizePosixPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ToolInvocationException("PathNotAllowed", "Remote path must not be empty.");
        }

        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        var stack = new Stack<string>();
        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (stack.Count == 0)
                {
                    throw new ToolInvocationException("PathNotAllowed", $"Path '{path}' resolves outside the allowed remote roots.");
                }

                stack.Pop();
                continue;
            }

            stack.Push(segment);
        }

        return "/" + string.Join("/", stack.Reverse());
    }
}
