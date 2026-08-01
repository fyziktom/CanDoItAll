using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceCommandEnvironmentPolicyTests
{
    [Theory]
    [InlineData("DOTNET_CLI_USE_MSBUILD_SERVER")]
    [InlineData("DOTNET_ENVIRONMENT")]
    [InlineData("DOTNET_LAUNCH_PROFILE")]
    [InlineData("DOTNET_MODIFIABLE_ASSEMBLIES")]
    [InlineData("DOTNET_STARTUP_HOOKS")]
    [InlineData("DOTNET_WATCH")]
    [InlineData("DOTNET_WATCH_HOTRELOAD_NAMEDPIPE_NAME")]
    [InlineData("DOTNET_WATCH_ITERATION")]
    [InlineData("DOTNET_WATCH_RESTART_ON_RUDE_EDIT")]
    [InlineData("DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH")]
    [InlineData("DOTNET_WATCH_SUPPRESS_EMOJIS")]
    [InlineData("DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER")]
    [InlineData("MSBuildExtensionsPath")]
    [InlineData("MSBUILDFAILONDRIVEENUMERATINGWILDCARD")]
    [InlineData("MSBuildSDKsPath")]
    [InlineData("MSBUILD_EXE_PATH")]
    public void Inherited_environment_excludes_observed_host_owned_variable(string variableName)
    {
        var policy = new WorkspaceCommandEnvironmentPolicy();
        var environment = policy.BuildEnvironmentVariables(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [variableName] = "host-owned-value"
            });

        Assert.DoesNotContain(variableName, environment.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inherited_environment_excludes_host_instrumentation_and_ambient_credentials()
    {
        var policy = new WorkspaceCommandEnvironmentPolicy();
        var environment = policy.BuildEnvironmentVariables(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = @"C:\Program Files\dotnet",
                ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
                ["NUGET_PACKAGES"] = @"C:\packages",
                ["PYTHONUTF8"] = "1",
                ["MSBUILD_EXE_PATH"] = @"C:\host\MSBuild.dll",
                ["MSBuildSDKsPath"] = @"C:\host\Sdks",
                ["DOTNET_STARTUP_HOOKS"] = @"C:\host\watch-hook.dll",
                ["DOTNET_WATCH"] = "1",
                ["DOTNET_WATCH_ITERATION"] = "4",
                ["DOTNET_WATCH_HOTRELOAD_NAMEDPIPE_NAME"] = "watch-pipe",
                ["OPENAI_API_KEY"] = "ambient-secret",
                ["PIP_INDEX_URL"] = "https://credential@example.invalid/simple"
            });

        Assert.Equal(@"C:\Program Files\dotnet", environment["PATH"]);
        Assert.Equal("en-US", environment["DOTNET_CLI_UI_LANGUAGE"]);
        Assert.Equal(@"C:\packages", environment["NUGET_PACKAGES"]);
        Assert.Equal("1", environment["PYTHONUTF8"]);
        Assert.DoesNotContain("MSBUILD_EXE_PATH", environment.Keys);
        Assert.DoesNotContain("MSBuildSDKsPath", environment.Keys);
        Assert.DoesNotContain("DOTNET_STARTUP_HOOKS", environment.Keys);
        Assert.DoesNotContain("DOTNET_WATCH", environment.Keys);
        Assert.DoesNotContain("DOTNET_WATCH_ITERATION", environment.Keys);
        Assert.DoesNotContain("DOTNET_WATCH_HOTRELOAD_NAMEDPIPE_NAME", environment.Keys);
        Assert.DoesNotContain("OPENAI_API_KEY", environment.Keys);
        Assert.DoesNotContain("PIP_INDEX_URL", environment.Keys);
    }

    [Fact]
    public void Explicit_environment_overlay_can_supply_recipe_owned_values()
    {
        var policy = new WorkspaceCommandEnvironmentPolicy();
        var merged = policy.MergeEnvironmentVariables(
            new Dictionary<string, string?>
            {
                ["RECIPE_TOKEN"] = "explicit-value",
                ["MSBUILD_EXE_PATH"] = @"C:\explicit\MSBuild.dll"
            });

        Assert.Equal("explicit-value", merged["RECIPE_TOKEN"]);
        Assert.Equal(@"C:\explicit\MSBuild.dll", merged["MSBUILD_EXE_PATH"]);
    }
}
