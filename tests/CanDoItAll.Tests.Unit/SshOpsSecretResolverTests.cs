using CanDoItAll.Mcp.SshOps.Configuration;
using CanDoItAll.Mcp.SshOps.Security;

namespace CanDoItAll.Tests.Unit;

public sealed class SshOpsSecretResolverTests
{
    [Fact]
    public void ResolvePassword_PrefersProcessScopedValue()
    {
        var variableName = $"CANDOITALL_TEST_SECRET_{Guid.NewGuid():N}";
        var originalProcessValue = Environment.GetEnvironmentVariable(variableName);
        var originalUserValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);

        try
        {
            Environment.SetEnvironmentVariable(variableName, "process-secret");
            Environment.SetEnvironmentVariable(variableName, "user-secret", EnvironmentVariableTarget.User);

            var resolver = new SecretResolver();

            var resolved = resolver.ResolvePassword(new AuthOptions { PasswordEnv = variableName });

            Assert.Equal("process-secret", resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalProcessValue);
            Environment.SetEnvironmentVariable(variableName, originalUserValue, EnvironmentVariableTarget.User);
        }
    }

    [Fact]
    public void ResolvePassword_FallsBackToUserScopedValue_WhenProcessScopedValueIsMissing()
    {
        var variableName = $"CANDOITALL_TEST_SECRET_{Guid.NewGuid():N}";
        var originalProcessValue = Environment.GetEnvironmentVariable(variableName);
        var originalUserValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);

        try
        {
            Environment.SetEnvironmentVariable(variableName, null);
            Environment.SetEnvironmentVariable(variableName, "user-secret", EnvironmentVariableTarget.User);

            var resolver = new SecretResolver();

            var resolved = resolver.ResolvePassword(new AuthOptions { PasswordEnv = variableName });

            Assert.Equal("user-secret", resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalProcessValue);
            Environment.SetEnvironmentVariable(variableName, originalUserValue, EnvironmentVariableTarget.User);
        }
    }
}
