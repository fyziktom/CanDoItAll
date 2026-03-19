using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagerHostEnvironmentTests
{
    [Fact]
    public void ResolveEnvironmentName_defaults_to_development_when_no_environment_is_set()
    {
        var originalAspNetCore = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var originalDotNet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);

            var environmentName = ManagerHostEnvironment.ResolveEnvironmentName();

            Assert.Equal("Development", environmentName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalAspNetCore);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalDotNet);
        }
    }

    [Fact]
    public void ResolveEnvironmentName_prefers_explicit_aspnetcore_environment()
    {
        var originalAspNetCore = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var originalDotNet = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

            var environmentName = ManagerHostEnvironment.ResolveEnvironmentName();

            Assert.Equal("Production", environmentName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalAspNetCore);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", originalDotNet);
        }
    }
}
