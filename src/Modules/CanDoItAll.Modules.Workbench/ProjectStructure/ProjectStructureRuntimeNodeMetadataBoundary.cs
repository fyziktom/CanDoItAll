using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureRuntimeNodeMetadataBoundary(
    IProjectStructureRuntimeLauncher runtimeLauncher,
    IWorkspacePathAccessGuard workspacePathAccessGuard,
    IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory)
{
    public string ValidateAndCanonicalize(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        string? metadataJson)
        => ValidateAndCanonicalizeCore(
            objectType,
            objectSubtype,
            notes,
            metadataJson,
            ProjectStructureRuntimePathAuthorityMode.OperatorSelected);

    public string ValidateAndCanonicalizeForAgent(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        string? metadataJson)
        => ValidateAndCanonicalizeCore(
            objectType,
            objectSubtype,
            notes,
            metadataJson,
            ProjectStructureRuntimePathAuthorityMode.AgentExecution);

    private string ValidateAndCanonicalizeCore(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        string? metadataJson,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        var effectiveMetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        if (objectType is not (ProjectObjectType.Script or ProjectObjectType.Environment or ProjectObjectType.Infrastructure))
        {
            return effectiveMetadataJson;
        }

        try
        {
            var metadata = ProjectObjectMetadataSerializer.Parse(effectiveMetadataJson);
            if (!ProjectStructureRuntimeNodeKindPolicy.TryValidateAndApply(
                    objectType,
                    objectSubtype,
                    effectiveMetadataJson,
                    metadata,
                    out var kindValidationMessage))
            {
                throw InvalidRuntimeMetadata(kindValidationMessage);
            }

            ProjectStructureDotNetRuntimeMetadataHydrator.Hydrate(
                objectType,
                objectSubtype,
                notes,
                metadata);
            ProjectObjectMetadataSerializer.Validate(
                objectType,
                objectSubtype?.Trim() ?? string.Empty,
                metadata);

            switch (objectType)
            {
                case ProjectObjectType.Script:
                    ValidateScript(objectSubtype, notes, metadata, pathAuthorityMode);
                    break;
                case ProjectObjectType.Environment:
                    ValidateEnvironment(objectSubtype, notes, metadata, pathAuthorityMode);
                    break;
                case ProjectObjectType.Infrastructure:
                    ValidateInfrastructure(objectSubtype, notes, metadata, pathAuthorityMode);
                    break;
            }

            return ProjectObjectMetadataSerializer.SerializePreservingUnknownProperties(
                effectiveMetadataJson,
                metadata);
        }
        catch (ProjectStructureAgentException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            throw InvalidRuntimeMetadata(
                $"Runnable node metadata is invalid JSON or does not match the project-structure metadata schema: {exception.Message}");
        }
    }

    private void ValidateScript(
        string? objectSubtype,
        string? notes,
        ProjectObjectMetadataEnvelope metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (metadata.Script is null)
        {
            throw InvalidRuntimeMetadata(
                "Script runtime nodes require metadata.script with command, arguments, and workingDirectory fields.");
        }

        if (ProjectStructureDirectDotNetCommandPolicy.TryClassify(
                metadata.Script.Command,
                metadata.Script.Arguments,
                out _))
        {
            throw InvalidRuntimeMetadata(ProjectStructureDirectDotNetCommandPolicy.TypedEnvironmentRequiredMessage);
        }

        EnsureLaunchReady(
            ProjectObjectType.Script,
            objectSubtype,
            notes,
            metadata,
            "The script runtime target",
            pathAuthorityMode);
    }

    private void ValidateEnvironment(
        string? objectSubtype,
        string? notes,
        ProjectObjectMetadataEnvelope metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        if (metadata.Environment is null)
        {
            throw InvalidRuntimeMetadata(
                "Environment runtime nodes require metadata.environment with environmentKind, projectPath, and workingDirectory fields.");
        }

        var resolution = EnsureLaunchReady(
            ProjectObjectType.Environment,
            objectSubtype,
            notes,
            metadata,
            "The environment runtime target",
            pathAuthorityMode);
        if (metadata.Environment.EnvironmentKind is not (
                ProjectEnvironmentKind.DotNetRuntime or
                ProjectEnvironmentKind.DotNetWatch or
                ProjectEnvironmentKind.DotNetRelease))
        {
            return;
        }

        if (resolution.Plan?.Target is not { IsDirectory: false } projectTarget ||
            string.IsNullOrWhiteSpace(projectTarget.Path))
        {
            throw InvalidRuntimeMetadata(
                "The .NET runtime target did not resolve to an exact application project file.");
        }

        metadata.Environment.ProjectPath = projectTarget.Path;
        metadata.Environment.WorkingDirectory = Path.GetDirectoryName(projectTarget.Path)
                                                ?? resolution.Plan.WorkingDirectory;
    }

    private void ValidateInfrastructure(
        string? objectSubtype,
        string? notes,
        ProjectObjectMetadataEnvelope metadata,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        var expectedKind = ProjectNodeKindRegistry.ResolveInfrastructureKind(objectSubtype);
        if (expectedKind != ProjectInfrastructureKind.DockerMode)
        {
            return;
        }

        if (metadata.Infrastructure is null)
        {
            throw InvalidRuntimeMetadata(
                "Docker runtime nodes require metadata.infrastructure with runtimeCommand and workingDirectory fields.");
        }

        EnsureLaunchReady(
            ProjectObjectType.Infrastructure,
            objectSubtype,
            notes,
            metadata,
            "The Docker runtime target",
            pathAuthorityMode);
    }

    private ProjectStructureRuntimeLaunchResolution EnsureLaunchReady(
        ProjectObjectType objectType,
        string? objectSubtype,
        string? notes,
        ProjectObjectMetadataEnvelope metadata,
        string targetDescription,
        ProjectStructureRuntimePathAuthorityMode pathAuthorityMode)
    {
        var resolution = runtimeLauncher.Resolve(
            objectType,
            objectSubtype,
            notes,
            ProjectObjectMetadataSerializer.Serialize(metadata),
            pathAuthorityMode);
        if (!resolution.IsSuccess)
        {
            throw InvalidRuntimeMetadata($"{targetDescription} is not launch-ready: {resolution.Message}");
        }

        if (pathAuthorityMode == ProjectStructureRuntimePathAuthorityMode.AgentExecution)
        {
            EnsureAgentPlanPathAuthority(resolution.Plan!);
        }

        return resolution;
    }

    private void EnsureAgentPlanPathAuthority(ProjectStructureRuntimeLaunchPlan plan)
    {
        EnsureAgentCanReadPath(plan.WorkingDirectory, "working directory");
        if (plan.Target is not null)
        {
            EnsureAgentCanReadPath(plan.Target.Path, plan.Target.Description);
        }
    }

    private void EnsureAgentCanReadPath(string path, string description)
    {
        if (workspacePathAccessGuard.ResolveWorkspacePath(path).IsSuccess)
        {
            return;
        }

        var auditScope = WorkspaceExecutionAuditContext.Current;
        var canRead = false;
        if (auditScope is not null)
        {
            var externalTargetRegistry = externalTargetPathRegistryFactory.Create(
                auditScope.ExternalTargetRootBindings);
            var pathAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
                path,
                externalTargetRegistry);
            canRead = !string.IsNullOrWhiteSpace(pathAlias) &&
                new EffectiveExternalTargetAccessScope(
                        auditScope.AllowedExternalTargetAliases,
                        auditScope.ReadOnlyExternalTargetAliases)
                    .CanRead(pathAlias);
        }

        if (canRead)
        {
            return;
        }

        throw ProjectStructureAgentException.CreateAgentVisible(
            403,
            "RuntimePathNotAuthorized",
            $"The runtime {description} is outside the managed workspace and is not included in this execution's audited external targets.",
            canRetryWithCorrectedInput: true);
    }

    private static ProjectStructureAgentException InvalidRuntimeMetadata(string message)
        => ProjectStructureAgentException.CreateAgentVisible(
            400,
            "InvalidRuntimeMetadata",
            message,
            canRetryWithCorrectedInput: true);
}
