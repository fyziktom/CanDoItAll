using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceProcessEnvironmentCompositionTests
{
    private static Dictionary<string, string?> LinuxHost() => new(StringComparer.Ordinal)
    {
        ["PATH"] = "/usr/bin",
        ["HOME"] = "/home/operator",
        ["TEMP"] = "/tmp",
        ["NUGET_PACKAGES"] = "/home/operator/.nuget/packages",
        ["PYTHONUTF8"] = "1",
        ["POWERSHELL_UPDATECHECK"] = "Off",
        ["npm_config_cache"] = "/home/operator/.npm",
        ["DOCKER_HOST"] = "unix:///run/docker.sock",
        ["HTTPS_PROXY"] = "http://proxy.example:3128",
        ["https_proxy"] = "http://proxy.example:3128",
        ["SSL_CERT_FILE"] = "/etc/ssl/corp.pem",
        ["CORP_BUILD_CHANNEL"] = "stable",
        ["OPENAI_API_KEY"] = "ambient-secret"
    };

    [Theory]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe", "workspace_static_serve", "NUGET_PACKAGES")]
    [InlineData("/usr/bin/dotnet", null, "NUGET_PACKAGES")]
    [InlineData("pwsh", "skill_script_run", "POWERSHELL_UPDATECHECK")]
    [InlineData("powershell.exe", null, "POWERSHELL_UPDATECHECK")]
    [InlineData("python3", "workspace_inspect_spreadsheet", "PYTHONUTF8")]
    [InlineData("py.exe", null, "PYTHONUTF8")]
    [InlineData("/opt/venv/bin/python3.12", null, "PYTHONUTF8")]
    [InlineData("npx.cmd", null, "npm_config_cache")]
    [InlineData("/usr/bin/docker", null, "DOCKER_HOST")]
    [InlineData("custom-tool", "workspace_dotnet_build", "NUGET_PACKAGES")]
    [InlineData("", "workspace_python_run_file", "PYTHONUTF8")]
    public void The_toolchain_follows_the_program_that_runs_and_falls_back_to_the_tool_name(
        string executable,
        string? toolName,
        string expectedName)
    {
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux, LinuxHost(), WorkspaceProcessEnvironmentSettings.Default);

        var environment = policy.MergeEnvironmentVariables(null, toolName, executable);

        Assert.Contains(expectedName, environment.Keys);
        Assert.DoesNotContain("OPENAI_API_KEY", environment.Keys);
    }

    [Fact]
    public void Git_gets_only_the_common_allowlist()
    {
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux, LinuxHost(), WorkspaceProcessEnvironmentSettings.Default);

        var environment = policy.MergeEnvironmentVariables(null, "workspace_git", "git");

        Assert.Contains("PATH", environment.Keys);
        Assert.DoesNotContain("NUGET_PACKAGES", environment.Keys);
        Assert.DoesNotContain("PYTHONUTF8", environment.Keys);
    }

    [Fact]
    public void Temp_and_home_values_are_never_emptied_and_null_unsets_a_variable()
    {
        var host = LinuxHost();
        host["TMP"] = string.Empty;
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux, host, WorkspaceProcessEnvironmentSettings.Default);

        var environment = policy.MergeEnvironmentVariables(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["TEMP"] = "",
            ["HOME"] = "   ",
            ["CUSTOM_SETTING"] = null,
            ["PATH"] = null,
            ["RECIPE_FLAG"] = ""
        });

        Assert.Equal("/tmp", environment["TEMP"]);
        Assert.Equal("/home/operator", environment["HOME"]);
        Assert.DoesNotContain("TMP", environment.Keys);
        Assert.DoesNotContain("CUSTOM_SETTING", environment.Keys);
        Assert.DoesNotContain("PATH", environment.Keys);
        Assert.Equal(string.Empty, environment["RECIPE_FLAG"]);
    }

    [Fact]
    public void A_unix_host_without_home_gets_the_private_toolchain_home()
    {
        var toolchainHome = Path.Combine(Path.GetTempPath(), $"cdia-toolchain-home-{Guid.NewGuid():N}");
        var host = LinuxHost();
        host.Remove("HOME");
        var policy = new WorkspaceCommandEnvironmentPolicy(
            LocalHostPlatform.Linux,
            host,
            WorkspaceProcessEnvironmentSettings.Default with { ToolchainHomePath = toolchainHome });

        try
        {
            var environment = policy.MergeEnvironmentVariables(null, "workspace_dotnet_build", "dotnet");

            Assert.Equal(toolchainHome, environment["HOME"]);
            Assert.Equal(toolchainHome, environment["DOTNET_CLI_HOME"]);
            Assert.True(Directory.Exists(toolchainHome));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(toolchainHome) & (UnixFileMode)0b111_111_111);
            }
        }
        finally
        {
            if (Directory.Exists(toolchainHome))
            {
                Directory.Delete(toolchainHome);
            }
        }
    }

    [Fact]
    public void An_existing_home_and_windows_hosts_are_left_alone()
    {
        var settings = WorkspaceProcessEnvironmentSettings.Default with { ToolchainHomePath = "/var/lib/candoitall/toolchain-home" };
        var linux = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux, LinuxHost(), settings);
        var windowsHost = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = @"C:\Windows",
            ["USERPROFILE"] = @"C:\Users\operator"
        };
        var windows = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Windows, windowsHost, settings);

        Assert.Equal("/home/operator", linux.MergeEnvironmentVariables(null)["HOME"]);
        Assert.DoesNotContain("HOME", windows.MergeEnvironmentVariables(null).Keys);
    }

    [Fact]
    public void Network_trust_is_off_by_default_and_passes_proxy_and_certificate_names_when_enabled()
    {
        var off = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux, LinuxHost(), WorkspaceProcessEnvironmentSettings.Default);
        var on = new WorkspaceCommandEnvironmentPolicy(
            LocalHostPlatform.Linux,
            LinuxHost(),
            WorkspaceProcessEnvironmentSettings.Default with { NetworkTrust = true });

        var withoutTrust = off.MergeEnvironmentVariables(null, "workspace_dotnet_restore", "dotnet");
        var withTrust = on.MergeEnvironmentVariables(null, "workspace_dotnet_restore", "dotnet");
        var scriptWithoutPermission = on.MergeEnvironmentVariables(null, "workspace_pwsh_run_script", "pwsh", extendedEnvironmentAllowed: false);

        Assert.DoesNotContain("HTTPS_PROXY", withoutTrust.Keys);
        Assert.Equal("http://proxy.example:3128", withTrust["HTTPS_PROXY"]);
        Assert.Equal("http://proxy.example:3128", withTrust["https_proxy"]);
        Assert.Equal("/etc/ssl/corp.pem", withTrust["SSL_CERT_FILE"]);
        Assert.DoesNotContain("HTTPS_PROXY", scriptWithoutPermission.Keys);
        Assert.Equal(["HTTPS_PROXY", "https_proxy", "SSL_CERT_FILE"], off.DescribeWithheldNetworkTrustNames().ToArray());
        Assert.Empty(on.DescribeWithheldNetworkTrustNames());
    }

    [Fact]
    public void Operator_extension_names_are_validated_and_reach_processes_only_as_extended_environment()
    {
        var settings = WorkspaceProcessEnvironmentSettings.FromOptions(
            new ProcessEnvironmentOptions
            {
                AdditionalInheritedNames =
                [
                    "CORP_BUILD_CHANNEL",
                    "OPENAI_API_KEY",
                    "DOTNET_STARTUP_HOOKS",
                    "MY_TOKEN",
                    "LD_PRELOAD",
                    "NODE_OPTIONS",
                    "1BAD",
                    "BAD NAME",
                    " "
                ]
            },
            defaultToolchainHomePath: "/var/lib/candoitall/toolchain-home",
            out var rejected);
        var policy = new WorkspaceCommandEnvironmentPolicy(LocalHostPlatform.Linux, LinuxHost(), settings);

        Assert.Equal(["CORP_BUILD_CHANNEL"], settings.AdditionalInheritedNames.ToArray());
        Assert.Equal(
            ["OPENAI_API_KEY", "DOTNET_STARTUP_HOOKS", "MY_TOKEN", "LD_PRELOAD", "NODE_OPTIONS", "1BAD", "BAD NAME"],
            rejected.ToArray());
        Assert.Equal("/var/lib/candoitall/toolchain-home", settings.ToolchainHomePath);
        Assert.Equal("stable", policy.MergeEnvironmentVariables(null, "workspace_dotnet_build", "dotnet")["CORP_BUILD_CHANNEL"]);
        Assert.DoesNotContain(
            "CORP_BUILD_CHANNEL",
            policy.MergeEnvironmentVariables(null, "workspace_pwsh_run_script", "pwsh", extendedEnvironmentAllowed: false).Keys);
        Assert.DoesNotContain("OPENAI_API_KEY", policy.MergeEnvironmentVariables(null, "workspace_dotnet_build", "dotnet").Keys);
    }

    [Fact]
    public void Options_default_to_everything_off()
    {
        var settings = WorkspaceProcessEnvironmentSettings.FromOptions(null, null, out var rejected);

        Assert.False(settings.NetworkTrust);
        Assert.Empty(settings.AdditionalInheritedNames);
        Assert.Null(settings.ToolchainHomePath);
        Assert.Empty(rejected);
    }

    [Theory]
    [InlineData("pwsh", "workspace_pwsh_run_script", "TEMP", true)]
    [InlineData("pwsh", "workspace_pwsh_run_script", "POWERSHELL_UPDATECHECK", true)]
    [InlineData("pwsh", "workspace_pwsh_run_script", "NUGET_PACKAGES", false)]
    [InlineData("python", "workspace_python_run_file", "PYTHONUTF8", true)]
    [InlineData("python", "workspace_python_run_file", "HTTPS_PROXY", false)]
    [InlineData("python", "workspace_python_run_file", "OPENAI_API_KEY", false)]
    public void Base_inherited_names_are_the_allowlist_and_toolchain_settings_only(
        string executable,
        string toolName,
        string name,
        bool expected)
    {
        var policy = new WorkspaceCommandEnvironmentPolicy(
            LocalHostPlatform.Linux,
            LinuxHost(),
            WorkspaceProcessEnvironmentSettings.Default with { NetworkTrust = true });

        Assert.Equal(expected, policy.IsBaseInheritedName(name, toolName, executable));
    }
}
