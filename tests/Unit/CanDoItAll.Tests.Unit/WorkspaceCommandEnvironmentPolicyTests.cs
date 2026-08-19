using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit.AgentFramework;

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
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Windows);
        var environment = policy.BuildEnvironmentVariables(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                [variableName] = "host-owned-value"
            },
            "workspace_dotnet_build");

        Assert.DoesNotContain(variableName, environment.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inherited_environment_excludes_host_instrumentation_and_ambient_credentials()
    {
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Windows);
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
            },
            "workspace_dotnet_build");

        Assert.Equal(@"C:\Program Files\dotnet", environment["PATH"]);
        Assert.Equal("en-US", environment["DOTNET_CLI_UI_LANGUAGE"]);
        Assert.Equal(@"C:\packages", environment["NUGET_PACKAGES"]);
        Assert.DoesNotContain("PYTHONUTF8", environment.Keys);
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

    [Fact]
    public void Unix_environment_preserves_case_distinct_explicit_names()
    {
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux);

        var merged = policy.MergeEnvironmentVariables(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["TOKEN"] = "upper",
                ["token"] = "lower"
            },
            "workspace_python_run_file");

        Assert.Equal("upper", merged["TOKEN"]);
        Assert.Equal("lower", merged["token"]);
        Assert.Equal(2, merged.Keys.Count(name => name is "TOKEN" or "token"));
    }

    [Fact]
    public void Windows_environment_collapses_case_distinct_explicit_names()
    {
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Windows);

        var merged = policy.MergeEnvironmentVariables(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["TOKEN"] = "upper",
                ["token"] = "lower"
            },
            "workspace_pwsh_run_script");

        Assert.Single(merged.Keys, name => name.Equals("TOKEN", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("lower", merged["TOKEN"]);
    }

    [Fact]
    public void Tool_specific_environment_is_not_inherited_by_unrelated_tools()
    {
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux);
        var source = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["NUGET_PACKAGES"] = "/tmp/nuget",
            ["PYTHONUTF8"] = "1",
            ["OPENAI_API_KEY"] = "ambient-secret"
        };

        var dotnet = policy.BuildEnvironmentVariables(source, "workspace_dotnet_test");
        var python = policy.BuildEnvironmentVariables(source, "workspace_python_run_file");

        Assert.Contains("NUGET_PACKAGES", dotnet.Keys);
        Assert.DoesNotContain("PYTHONUTF8", dotnet.Keys);
        Assert.Contains("PYTHONUTF8", python.Keys);
        Assert.DoesNotContain("NUGET_PACKAGES", python.Keys);
        Assert.DoesNotContain("OPENAI_API_KEY", dotnet.Keys);
        Assert.DoesNotContain("OPENAI_API_KEY", python.Keys);
    }

    [Fact]
    public void Docker_environment_inherits_only_named_docker_configuration_with_host_case_semantics()
    {
        var source = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/bin",
            ["DOCKER_HOST"] = "unix:///run/docker.sock",
            ["DOCKER_CONTEXT"] = "rootless",
            ["DOCKER_CONFIG"] = "/home/operator/.docker",
            ["DOCKER_CERT_PATH"] = "/secret/certificates",
            ["DOCKER_TLS_VERIFY"] = "1",
            ["DOCKER_UNSUPPORTED_SETTING"] = "must-not-flow",
            ["OPENAI_API_KEY"] = "ambient-secret"
        };
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux, source);

        IReadOnlyDictionary<string, string?> environment = policy.MergeEnvironmentVariables(
            environmentVariables: null,
            toolName: "docker");

        Assert.Equal("unix:///run/docker.sock", environment["DOCKER_HOST"]);
        Assert.Equal("rootless", environment["DOCKER_CONTEXT"]);
        Assert.Equal("/home/operator/.docker", environment["DOCKER_CONFIG"]);
        Assert.Equal("/secret/certificates", environment["DOCKER_CERT_PATH"]);
        Assert.Equal("1", environment["DOCKER_TLS_VERIFY"]);
        Assert.DoesNotContain("DOCKER_UNSUPPORTED_SETTING", environment.Keys);
        Assert.DoesNotContain("OPENAI_API_KEY", environment.Keys);
        Assert.Equal(StringComparer.Ordinal, policy.EnvironmentNameComparer);
    }
}
