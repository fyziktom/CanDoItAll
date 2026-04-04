using System.Text;
using CanDoItAll.Mcp.SshOps.Configuration;

namespace CanDoItAll.Mcp.SshOps.Security;

public sealed class SecretResolver
{
    public string ResolveRequired(string environmentVariableName)
    {
        var value = ResolveScopedEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ToolInvocationException("TargetNotConfigured", $"Environment variable '{environmentVariableName}' is required but not set.");
        }

        return value;
    }

    public string? ResolveOptional(string? environmentVariableName)
    {
        return string.IsNullOrWhiteSpace(environmentVariableName)
            ? null
            : ResolveScopedEnvironmentVariable(environmentVariableName);
    }

    public string? ResolvePrivateKey(AuthOptions auth)
    {
        var configuredValue = ResolveOptional(auth.PrivateKeyEnv);
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return null;
        }

        if (configuredValue.Contains("BEGIN", StringComparison.OrdinalIgnoreCase))
        {
            return configuredValue;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(configuredValue));
        }
        catch (FormatException)
        {
            return configuredValue;
        }
    }

    public string? ResolvePrivateKeyPassphrase(AuthOptions auth)
    {
        return ResolveOptional(auth.PrivateKeyPassphraseEnv);
    }

    public string? ResolvePassword(AuthOptions auth)
    {
        return ResolveOptional(auth.PasswordEnv);
    }

    private static string? ResolveScopedEnvironmentVariable(string environmentVariableName)
    {
        var processValue = Environment.GetEnvironmentVariable(environmentVariableName);
        if (!string.IsNullOrWhiteSpace(processValue))
        {
            return processValue;
        }

        var userValue = Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(userValue))
        {
            return userValue;
        }

        return Environment.GetEnvironmentVariable(environmentVariableName, EnvironmentVariableTarget.Machine);
    }
}
