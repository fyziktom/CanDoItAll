using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class SkillScriptExecutionBoundary
{
    public static Task<WorkspaceCommandExecutionResult> ExecuteAsync(
        AgentFileSkill skill,
        AgentFileSkillScript script,
        IReadOnlyList<string> scriptArguments,
        FileSkillExecutionPolicy policy,
        IWorkspaceCommandExecutionService commandExecutionService)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(scriptArguments);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(commandExecutionService);

        if (!File.Exists(script.FullPath))
        {
            throw AgentToolInputValidationException.Create(
                "The selected skill script is no longer available. Reload the skill resources and choose an available script before retrying.");
        }

        return commandExecutionService.RunSkillScript(
            Path.GetFileName(skill.Path),
            script.FullPath,
            scriptArguments.ToArray(),
            Path.GetDirectoryName(script.FullPath),
            policy.ApprovalRequired,
            policy.TrustLevel,
            [policy.RootPath]);
    }
}
