using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class SkillScriptExecutionBoundaryTests
{
    [Fact]
    public async Task ExecuteAsync_missing_script_returns_typed_safe_failure_without_absolute_path()
    {
        var skillRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.SkillScriptExecutionBoundaryTests.{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(skillRoot, "scripts", "missing-private-script.ps1");
        var (skill, script) = CreateSkill(skillRoot, scriptPath);
        var service = DispatchProxy.Create<IWorkspaceCommandExecutionService, ThrowingCommandExecutionProxy>();

        var exception = await Assert.ThrowsAsync<AgentToolInputValidationException>(() =>
            SkillScriptExecutionBoundary.ExecuteAsync(
                skill,
                script,
                [],
                new FileSkillExecutionPolicy(skillRoot, ApprovalRequired: true, TrustLevel: "FileSkill"),
                service));

        Assert.DoesNotContain(scriptPath, exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reload the skill resources", exception.SafeMessage, StringComparison.Ordinal);
        Assert.True(MafAgentToolFailureMapper.TryMap(exception, out var failure));
        Assert.Equal(AgentToolInputValidationException.FailureCode, failure.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_unexpected_command_failure_preserves_exception_and_is_not_model_mappable()
    {
        var skillRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.SkillScriptExecutionBoundaryTests.{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(skillRoot, "scripts", "run.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, "Write-Output 'ok'");
        var (skill, script) = CreateSkill(skillRoot, scriptPath);
        var sentinel = new IOException(
            @"Skill provider failed while reading C:\private\skill-provider-secret.txt");
        var service = DispatchProxy.Create<IWorkspaceCommandExecutionService, ThrowingCommandExecutionProxy>();
        ((ThrowingCommandExecutionProxy)(object)service).Failure = sentinel;

        try
        {
            var exception = await Assert.ThrowsAsync<IOException>(() =>
                SkillScriptExecutionBoundary.ExecuteAsync(
                    skill,
                    script,
                    ["--validate"],
                    new FileSkillExecutionPolicy(skillRoot, ApprovalRequired: true, TrustLevel: "FileSkill"),
                    service));

            Assert.Same(sentinel, exception);
            Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
        }
        finally
        {
            Directory.Delete(skillRoot, recursive: true);
        }
    }

    private static (AgentFileSkill Skill, AgentFileSkillScript Script) CreateSkill(
        string skillRoot,
        string scriptPath)
    {
        var script = (AgentFileSkillScript)Activator.CreateInstance(
            typeof(AgentFileSkillScript),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: ["scripts/run.ps1", scriptPath, null],
            culture: null)!;
        var skill = (AgentFileSkill)Activator.CreateInstance(
            typeof(AgentFileSkill),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                new AgentSkillFrontmatter("sample-skill", "Sample skill", "MIT"),
                "# Sample skill",
                skillRoot,
                Array.Empty<AgentSkillResource>(),
                new AgentSkillScript[] { script }
            ],
            culture: null)!;
        return (skill, script);
    }

    private class ThrowingCommandExecutionProxy : DispatchProxy
    {
        public Exception Failure { get; set; } =
            new InvalidOperationException("The command service must not be invoked in this test.");

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IWorkspaceCommandExecutionService.RunSkillScript))
            {
                return Task.FromException<WorkspaceCommandExecutionResult>(Failure);
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
