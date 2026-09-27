using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class SkillScriptExecutionBoundaryTests
{
    [Fact]
    public async Task ExecuteAsync_missing_script_returns_typed_safe_failure_without_absolute_path()
    {
        var skillRoot = Path.Combine(
            Path.GetTempPath(),
            $"sample-skill-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(skillRoot, "scripts", "missing-private-script.ps1");
        try {
            var (skill, script) = await CreateSkillAsync(skillRoot, scriptPath);
            File.Delete(scriptPath);
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
        } finally {
            Directory.Delete(skillRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_unexpected_command_failure_preserves_exception_and_is_not_model_mappable()
    {
        var skillRoot = Path.Combine(
            Path.GetTempPath(),
            $"sample-skill-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(skillRoot, "scripts", "run.ps1");
        var sentinel = new IOException(
            @"Skill provider failed while reading C:\private\skill-provider-secret.txt");
        var service = DispatchProxy.Create<IWorkspaceCommandExecutionService, ThrowingCommandExecutionProxy>();
        ((ThrowingCommandExecutionProxy)(object)service).Failure = sentinel;

        try {
            var (skill, script) = await CreateSkillAsync(skillRoot, scriptPath);
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

    private static async Task<(AgentFileSkill Skill, AgentFileSkillScript Script)> CreateSkillAsync(
        string skillRoot,
        string scriptPath) {
        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, "Write-Output 'ok'");
        await File.WriteAllTextAsync(Path.Combine(skillRoot, "SKILL.md"), $$"""
            ---
            name: {{Path.GetFileName(skillRoot)}}
            description: Sample skill
            ---
            # Sample skill
            """);
        using var client = new UnusedChatClient();
        var agent = new ChatClientAgent(client);
        var source = new AgentFileSkillsSource(skillRoot);
        var skill = Assert.IsType<AgentFileSkill>(Assert.Single(
            await source.GetSkillsAsync(new AgentSkillsSourceContext(agent, null))));
        var relativePath = Path.GetRelativePath(skillRoot, scriptPath).Replace(Path.DirectorySeparatorChar, '/');
        var script = Assert.IsType<AgentFileSkillScript>(await skill.GetScriptAsync(relativePath));
        return (skill, script);
    }

    private sealed class UnusedChatClient : IChatClient {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("File-skill discovery must not call a model.");
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("File-skill discovery must not call a model.");
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
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
