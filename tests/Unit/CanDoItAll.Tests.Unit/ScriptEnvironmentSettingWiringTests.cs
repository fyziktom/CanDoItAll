using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ScriptEnvironmentSettingWiringTests
{
    [Fact]
    public void Seed_drift_detection_notices_a_changed_script_environment_permission()
    {
        var method = typeof(SandboxWorkspaceSeedNormalizer).GetMethod(
            "WorkspaceToolAccessEquals",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var withEnvironment = AgentWorkspaceToolAccessMetadata.Write(
            "{}",
            new AgentWorkspaceToolAccessSettings { CanRunLocalScripts = true, CanScriptsReadEnvironment = true });
        var withoutEnvironment = AgentWorkspaceToolAccessMetadata.Write(
            "{}",
            new AgentWorkspaceToolAccessSettings { CanRunLocalScripts = true });

        Assert.False((bool)method.Invoke(null, [withEnvironment, withoutEnvironment])!);
        Assert.True((bool)method.Invoke(null, [withEnvironment, withEnvironment])!);
    }
}
