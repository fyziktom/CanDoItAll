namespace CanDoItAll.Mcp.SshOps.Configuration;

public sealed class TargetCatalog(RuntimeConfiguration configuration)
{
    private readonly Dictionary<string, ResolvedTargetConfiguration> _targets = configuration.Options.Targets
        .Select(target => ResolveTarget(configuration.Options.Defaults, target))
        .ToDictionary(static target => target.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ResolvedTargetConfiguration> GetAll()
    {
        return _targets.Values.OrderBy(static target => target.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public ResolvedTargetConfiguration GetRequired(string? targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            throw new ToolInvocationException("TargetNotConfigured", "A target name is required.");
        }

        if (_targets.TryGetValue(targetName.Trim(), out var target))
        {
            return target;
        }

        throw new ToolInvocationException("TargetNotFound", $"Target '{targetName}' is not configured.", new { target = targetName });
    }

    private static ResolvedTargetConfiguration ResolveTarget(DefaultsOptions defaults, TargetOptions target)
    {
        return new ResolvedTargetConfiguration(
            target.Name.Trim(),
            target.Host.Trim(),
            target.Port,
            target.User.Trim(),
            target.Sudo,
            target.Auth,
            target.HostKeyVerification,
            NormalizePath(target.Paths.RemoteStateRoot ?? defaults.RemoteStateRoot),
            NormalizePath(target.Paths.StacksRoot ?? defaults.StacksRoot),
            NormalizePath(target.Paths.SecretsRoot ?? defaults.SecretsRoot),
            (target.Paths.AllowedRoots ?? defaults.AllowedRoots).Select(NormalizePath).ToArray(),
            target.Docker,
            target.Traefik,
            target.Validation,
            target.Guards);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }
}
