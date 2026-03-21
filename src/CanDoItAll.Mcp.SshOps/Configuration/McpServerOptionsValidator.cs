using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.SshOps.Configuration;

public sealed class McpServerOptionsValidator : IValidateOptions<McpServerOptions>
{
    public ValidateOptionsResult Validate(string? name, McpServerOptions options)
    {
        List<string> failures = [];

        if (options.Targets.Length == 0)
        {
            failures.Add("At least one SSH target must be configured.");
        }

        var duplicateTargets = options.Targets
            .GroupBy(static target => target.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateTargets.Length > 0)
        {
            failures.Add($"Target names must be unique. Duplicates: {string.Join(", ", duplicateTargets)}.");
        }

        foreach (var target in options.Targets)
        {
            if (options.Security.DenyPasswordAuthentication && !string.IsNullOrWhiteSpace(target.Auth.PasswordEnv))
            {
                failures.Add($"Target '{target.Name}' cannot configure password authentication when Security.DenyPasswordAuthentication is true.");
            }

            if (string.IsNullOrWhiteSpace(target.Auth.PrivateKeyEnv) &&
                string.IsNullOrWhiteSpace(target.Auth.PasswordEnv))
            {
                failures.Add($"Target '{target.Name}' must configure either Auth.PrivateKeyEnv or Auth.PasswordEnv.");
            }

            if (string.IsNullOrWhiteSpace(target.HostKeyVerification.Mode))
            {
                failures.Add($"Target '{target.Name}' must configure HostKeyVerification.Mode.");
            }

            if (options.Security.RequireHostKeyPinningInProduction &&
                string.Equals(target.HostKeyVerification.Mode, "none", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"Target '{target.Name}' cannot disable host key verification while Security.RequireHostKeyPinningInProduction is true.");
            }

            var hasFingerprintValues = !string.IsNullOrWhiteSpace(target.HostKeyVerification.Value) ||
                                       target.HostKeyVerification.Values.Length > 0;
            if (!string.Equals(target.HostKeyVerification.Mode, "none", StringComparison.OrdinalIgnoreCase) &&
                !hasFingerprintValues)
            {
                failures.Add($"Target '{target.Name}' must configure a host key verification value.");
            }

            var allowedRoots = target.Paths.AllowedRoots ?? options.Defaults.AllowedRoots;
            if (allowedRoots.Length == 0)
            {
                failures.Add($"Target '{target.Name}' must declare at least one allowed root.");
            }

            if (target.Guards.AllowRawExec && !options.Server.AllowDangerousRawExec)
            {
                failures.Add($"Target '{target.Name}' cannot allow raw exec when Server.AllowDangerousRawExec is false.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
