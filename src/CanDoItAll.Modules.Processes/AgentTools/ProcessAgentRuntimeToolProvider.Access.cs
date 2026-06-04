using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessAgentRuntimeToolProvider
{
    private async Task<ProcessRunListItem> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await processesService.GetRunAsync(runId, cancellationToken);
        if (run is not null)
        {
            return run;
        }

        throw new ProcessToolException(
            "ProcessRunNotFound",
            $"Process run '{runId:D}' was not found.");
    }

    private async Task<IReadOnlyDictionary<Guid, ProcessDefinitionListItem>> GetAllowedDefinitionsByIdAsync(
        ProcessAccessState accessState,
        CancellationToken cancellationToken)
    {
        if (accessState.AllowedDefinitionsByIdTask is not null)
        {
            return await accessState.AllowedDefinitionsByIdTask;
        }

        accessState.AllowedDefinitionsByIdTask = LoadAllowedDefinitionsByIdAsync(accessState, cancellationToken);
        return await accessState.AllowedDefinitionsByIdTask;
    }

    private async Task<IReadOnlyDictionary<Guid, ProcessDefinitionListItem>> LoadAllowedDefinitionsByIdAsync(
        ProcessAccessState accessState,
        CancellationToken cancellationToken)
    {
        var definitions = await processesService.ListDefinitionsAsync(cancellationToken: cancellationToken);
        return accessState.AllowAllDefinitions
            ? definitions.ToDictionary(item => item.Id)
            : definitions
                .Where(item => accessState.AllowedDefinitionIds.Contains(item.Id))
                .ToDictionary(item => item.Id);
    }

    private async Task EnsureDefinitionExistsAsync(
        ProcessAccessState accessState,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var allowedDefinitions = await GetAllowedDefinitionsByIdAsync(accessState, cancellationToken);
        if (allowedDefinitions.ContainsKey(definitionId))
        {
            return;
        }

        throw new ProcessToolException(
            "ProcessDefinitionNotFound",
            $"Process definition '{definitionId:D}' was not found.");
    }

    private async Task EnsureProjectReadAllowedAsync(
        ProcessAccessState accessState,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        var allowedDefinitions = await GetAllowedDefinitionsByIdAsync(accessState, cancellationToken);
        if (allowedDefinitions.Values.Any(item => item.ProjectId == projectId))
        {
            return;
        }

        throw new ProcessToolException(
            "ProcessProjectDenied",
            $"Project '{projectId:D}' is outside the agent's allowed process scope.");
    }

    private static Guid EnsureSuccess(Result<Guid> result)
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }

        throw CreateProcessToolException(result.Errors);
    }

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw CreateProcessToolException(result.Errors);
        }
    }

    private static ProcessToolException CreateProcessToolException(IReadOnlyList<Error> errors)
    {
        var firstError = errors.FirstOrDefault() ?? Error.Failure("The process operation failed.", "processes.failure");
        return new ProcessToolException(firstError.Code, firstError.Message);
    }

    private static string ResolveRequiredRoleName(ProcessDefinitionRoleAddRequest request)
    {
        var roleName = request.RoleName.Trim();
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            return roleName;
        }

        throw new ProcessToolException(
            "ProcessRoleNameRequired",
            "A non-empty roleName is required to add a process role.");
    }

    private static string ResolveRolePurpose(ProcessDefinitionRoleAddRequest request, string roleName)
    {
        var value = ResolveFirstNonBlank(request.Purpose, request.Responsibilities);
        return string.IsNullOrWhiteSpace(value)
            ? $"Owns {roleName} responsibilities for the process."
            : value;
    }

    private static string ResolveRoleStaffingIntent(ProcessDefinitionRoleAddRequest request, string roleName)
    {
        var value = ResolveFirstNonBlank(request.StaffingIntent, request.Responsibilities);
        return string.IsNullOrWhiteSpace(value)
            ? $"Staff a contributor accountable for {roleName}."
            : value;
    }

    private static string ResolveFirstNonBlank(params string?[] values)
    {
        return values
            .Select(value => value?.Trim() ?? string.Empty)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static double ResolveNextRoleCanvasX(ProcessDefinitionEditorModel editor)
    {
        return editor.Roles.Count == 0
            ? 80
            : editor.Roles.Max(role => role.CanvasX) + 220;
    }

    private static double ResolveNextRoleCanvasY(ProcessDefinitionEditorModel editor)
    {
        return editor.Roles.Count == 0
            ? 80
            : editor.Roles
                .OrderBy(role => role.CanvasX)
                .Last()
                .CanvasY;
    }

    private static void EnsureReadAllowed(ProcessAccessState accessState)
    {
        if (accessState.CanRead)
        {
            return;
        }

        throw new ProcessToolException(
            "ProcessReadDenied",
            "This agent is not allowed to read process modules. Enable read access in the agent settings.");
    }

    private static void EnsureWriteAllowed(ProcessAccessState accessState)
    {
        if (accessState.CanWrite)
        {
            return;
        }

        throw new ProcessToolException(
            "ProcessWriteDenied",
            "This agent is not allowed to write process modules. Enable write access in the agent settings.");
    }

    private static void EnsureDefinitionReadAllowed(ProcessAccessState accessState, Guid definitionId)
    {
        EnsureReadAllowed(accessState);
        EnsureDefinitionAllowed(accessState, definitionId);
    }

    private static void EnsureDefinitionWriteAllowed(ProcessAccessState accessState, Guid definitionId)
    {
        EnsureWriteAllowed(accessState);
        EnsureDefinitionAllowed(accessState, definitionId);
    }

    private static void EnsureDefinitionAllowed(ProcessAccessState accessState, Guid definitionId)
    {
        if (accessState.AllowAllDefinitions ||
            accessState.AllowedDefinitionIds.Contains(definitionId))
        {
            return;
        }

        throw new ProcessToolException(
            "ProcessDefinitionDenied",
            $"Process definition '{definitionId:D}' is outside the agent's allowed process scope.");
    }

    private static void GrantDefinitionAccess(ProcessAccessState accessState, Guid definitionId)
    {
        if (definitionId == Guid.Empty)
        {
            return;
        }

        accessState.AllowedDefinitionIds.Add(definitionId);
        accessState.AllowedDefinitionsByIdTask = null;
    }

    private static void RevokeDefinitionAccess(ProcessAccessState accessState, Guid definitionId)
    {
        accessState.AllowedDefinitionIds.Remove(definitionId);
        accessState.AllowedDefinitionsByIdTask = null;
    }

    private sealed class ProcessAccessState
    {
        public ProcessAccessState(AgentProcessAccessSettings settings)
        {
            var normalized = AgentProcessAccessMetadata.Normalize(settings);
            CanRead = normalized.CanRead;
            CanWrite = normalized.CanWrite;
            AllowAllDefinitions = normalized.AllowAllDefinitions;
            AllowedDefinitionIds = normalized.AllowedDefinitionIds.ToHashSet();
        }

        public bool CanRead { get; }

        public bool CanWrite { get; }

        public bool AllowAllDefinitions { get; }

        public HashSet<Guid> AllowedDefinitionIds { get; }

        public Task<IReadOnlyDictionary<Guid, ProcessDefinitionListItem>>? AllowedDefinitionsByIdTask { get; set; }
    }

    private sealed class ProcessToolException(
        string errorCode,
        string message) : InvalidOperationException(message)
    {
        public string ErrorCode { get; } = errorCode;
    }
}
