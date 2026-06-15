using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessAgentRuntimeToolProvider
{
    private async Task<IReadOnlyList<ProcessDefinitionListItem>> ProcessesDefinitionsListAsync(
        ProcessAccessState accessState,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        var definitions = await processesService.ListDefinitionsAsync(projectId, cancellationToken);
        return accessState.AllowAllDefinitions
            ? definitions
            : definitions
                .Where(item => accessState.AllowedDefinitionIds.Contains(item.Id))
                .ToList();
    }

    private async Task<ProcessDefinitionEditorModel> ProcessesDefinitionEditorGetAsync(
        ProcessAccessState accessState,
        Guid? definitionId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        if (!definitionId.HasValue)
        {
            return await processesService.GetEditorAsync(null, projectId, cancellationToken);
        }

        EnsureDefinitionReadAllowed(accessState, definitionId.Value);
        await EnsureDefinitionExistsAsync(accessState, definitionId.Value, cancellationToken);

        var editor = await processesService.GetEditorAsync(definitionId, projectId, cancellationToken);
        if (!editor.Id.HasValue)
        {
            throw new ProcessToolException(
                "ProcessDefinitionNotFound",
                $"Process definition '{definitionId.Value:D}' was not found.");
        }

        return editor;
    }

    private async Task<Guid> ProcessesDefinitionSaveAsync(
        ProcessAccessState accessState,
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken)
    {
        if (model.Id.HasValue)
        {
            EnsureDefinitionWriteAllowed(accessState, model.Id.Value);
            await EnsureDefinitionExistsAsync(accessState, model.Id.Value, cancellationToken);
        }
        else
        {
            EnsureWriteAllowed(accessState);
        }

        var definitionId = EnsureSuccess(await processesService.SaveAsync(model, cancellationToken));
        GrantDefinitionAccess(accessState, definitionId);
        return definitionId;
    }

    private async Task<ProcessDefinitionRoleAddResult> ProcessesDefinitionRoleAddAsync(
        ProcessAccessState accessState,
        ProcessDefinitionRoleAddRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DefinitionId == Guid.Empty)
        {
            throw new ProcessToolException(
                "ProcessDefinitionIdRequired",
                "A process definition id is required to add a role.");
        }

        EnsureDefinitionWriteAllowed(accessState, request.DefinitionId);
        await EnsureDefinitionExistsAsync(accessState, request.DefinitionId, cancellationToken);

        var roleName = ResolveRequiredRoleName(request);
        var editor = await processesService.GetEditorAsync(request.DefinitionId, projectId: null, cancellationToken);
        if (!editor.Id.HasValue)
        {
            throw new ProcessToolException(
                "ProcessDefinitionNotFound",
                $"Process definition '{request.DefinitionId:D}' was not found.");
        }

        if (editor.Roles.Any(role => string.Equals(role.DisplayName.Trim(), roleName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ProcessToolException(
                "ProcessRoleDuplicate",
                $"Process definition '{request.DefinitionId:D}' already contains a role named '{roleName}'.");
        }

        var roleId = Guid.NewGuid();
        editor.Roles.Add(new ProcessRoleEditorModel
        {
            Id = roleId,
            DisplayName = roleName,
            Purpose = ResolveRolePurpose(request, roleName),
            StaffingIntent = ResolveRoleStaffingIntent(request, roleName),
            PreferredExecutorKind = request.PreferredExecutorKind.Trim(),
            PreferredProjectAssignmentRole = request.PreferredProjectAssignmentRole,
            IsRequired = request.IsRequired,
            AllowsFallback = request.AllowsFallback,
            RequiresExplicitApproval = request.RequiresExplicitApproval,
            DefaultAllocationPercent = request.DefaultAllocationPercent,
            SnapshotSummary = ResolveFirstNonBlank(request.SnapshotSummary, request.Responsibilities),
            CanvasX = request.CanvasX ?? ResolveNextRoleCanvasX(editor),
            CanvasY = request.CanvasY ?? ResolveNextRoleCanvasY(editor)
        });

        var definitionId = EnsureSuccess(await processesService.SaveAsync(editor, cancellationToken));
        var publishAttempted = request.PublishIfValid;
        var published = false;
        var publishErrorCode = string.Empty;
        var publishErrorMessage = string.Empty;

        if (request.PublishIfValid)
        {
            var publishResult = await processesService.PublishAsync(definitionId, cancellationToken);
            if (publishResult.IsSuccess)
            {
                published = true;
            }
            else
            {
                var publishError = publishResult.Errors.FirstOrDefault();
                publishErrorCode = publishError?.Code ?? "processes.publish-failed";
                publishErrorMessage = publishError?.Message ?? "The process definition could not be published.";
            }
        }

        return new ProcessDefinitionRoleAddResult(
            definitionId,
            roleId,
            roleName,
            publishAttempted,
            published,
            publishErrorCode,
            publishErrorMessage);
    }

    private async Task<Guid> ProcessesDefinitionPublishAsync(
        ProcessAccessState accessState,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        EnsureDefinitionWriteAllowed(accessState, definitionId);
        EnsureSuccess(await processesService.PublishAsync(definitionId, cancellationToken));
        return definitionId;
    }

    private async Task<Guid> ProcessesDefinitionDeleteAsync(
        ProcessAccessState accessState,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        EnsureDefinitionWriteAllowed(accessState, definitionId);
        await EnsureDefinitionExistsAsync(accessState, definitionId, cancellationToken);
        await processesService.DeleteAsync(definitionId, cancellationToken);
        RevokeDefinitionAccess(accessState, definitionId);
        return definitionId;
    }

    private async Task<ProcessImportExportEnvelope> ProcessesDefinitionExportAsync(
        ProcessAccessState accessState,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        EnsureDefinitionReadAllowed(accessState, definitionId);
        await EnsureDefinitionExistsAsync(accessState, definitionId, cancellationToken);
        return await processesService.ExportAsync(definitionId, cancellationToken);
    }

    private async Task<Guid> ProcessesDefinitionImportAsync(
        ProcessAccessState accessState,
        ProcessImportExportEnvelope envelope,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(accessState);
        var definitionId = EnsureSuccess(await processesService.ImportAsync(envelope, cancellationToken));
        GrantDefinitionAccess(accessState, definitionId);
        return definitionId;
    }
}
