using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class AgentCapabilitySetupFlowService
{
    private async Task<IReadOnlyList<CapabilityEditorModel>> ResolvePreviewCapabilitiesAsync(
        CapabilityAccessPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var saved = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        var selectedIds = request.CapabilityIds.ToHashSet();
        var editors = saved
            .Where(capability => selectedIds.Count == 0 || selectedIds.Contains(capability.Id))
            .Select(CapabilityEditorModel.FromDefinition)
            .ToList();

        foreach (var draft in request.DraftCapabilities)
        {
            var existingIndex = draft.Id.HasValue
                ? editors.FindIndex(item => item.Id == draft.Id)
                : editors.FindIndex(item => string.Equals(item.Key, draft.Key, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                editors[existingIndex] = draft;
            }
            else
            {
                editors.Add(draft);
            }
        }

        return editors;
    }

    private static IReadOnlyList<CapabilityIdentity> ReadRequiredCapabilities(
        IEnumerable<CapabilityIdentityEditorModel> requiredCapabilities,
        List<CapabilityValidationIssue> validationIssues)
    {
        var required = new List<CapabilityIdentity>();
        var index = 0;
        foreach (var requiredCapability in requiredCapabilities)
        {
            if (CapabilityKey.TryCreate(requiredCapability.Key, out var key))
            {
                required.Add(new CapabilityIdentity(requiredCapability.Kind, key));
            }
            else
            {
                validationIssues.Add(ValidationIssue(
                    $"$.requiredCapabilities[{index}].key",
                    "Required capability key is invalid.",
                    "Use lower kebab-case capability keys.",
                    requiredCapability.Kind));
            }

            index++;
        }

        return required;
    }

    private static IReadOnlyList<CapabilityExposureDescriptor> BuildExposureDescriptors(
        CapabilityEditorModel capability,
        List<CapabilityValidationIssue> validationIssues)
    {
        return capability.Kind switch
        {
            ModelCapabilityKind.Tool => [BuildToolExposureDescriptor(capability, validationIssues)],
            ModelCapabilityKind.McpServer => BuildMcpExposureDescriptors(capability, validationIssues),
            ModelCapabilityKind.Skill => [BuildCompatibilityExposureDescriptor(capability, AccessCapabilityKind.Skill, validationIssues)],
            _ => [BuildCompatibilityExposureDescriptor(capability, MapKind(capability.Kind), validationIssues)]
        };
    }

    private static CapabilityExposureDescriptor BuildToolExposureDescriptor(
        CapabilityEditorModel capability,
        List<CapabilityValidationIssue> validationIssues)
    {
        var configuration = ReadConfiguration<CapabilityToolConfigurationModel>(
            capability.ConfigurationJson,
            "$.configurationJson",
            MapKind(capability.Kind),
            validationIssues) ?? new CapabilityToolConfigurationModel();
        var identity = ReadIdentity(capability, AccessCapabilityKind.Tool, validationIssues);
        var runtimeToolName = ReadRuntimeToolName(
            configuration.RuntimeToolName,
            ToSnake(identity.Key.Value),
            "$.runtimeToolName",
            validationIssues);
        var implementationKey = ReadImplementationKey(
            configuration.ImplementationKey,
            $"tool.{identity.Key.Value}",
            "$.implementationKey",
            validationIssues);
        var classifications = ReadClassifications(
            configuration.OperationClassifications,
            [CapabilityOperationClassification.ExternalAction],
            validationIssues);
        var sideEffects = ReadSideEffects(configuration.SideEffects, new CapabilitySideEffectProfile(
            CapabilitySideEffectKind.ExternalAction,
            RequiresApprovalByDefault: true,
            IsStateChanging: true));

        return new CapabilityExposureDescriptor(
            identity,
            ResolveDisplayName(capability),
            ResolveDescription(capability),
            implementationKey,
            runtimeToolName,
            null,
            null,
            ReadTags(capability.Tags, validationIssues),
            classifications,
            sideEffects,
            validationIssues.Count == 0 ? CapabilityAvailabilityState.Available : CapabilityAvailabilityState.FailedSetup,
            null);
    }

    private static IReadOnlyList<CapabilityExposureDescriptor> BuildMcpExposureDescriptors(
        CapabilityEditorModel capability,
        List<CapabilityValidationIssue> validationIssues)
    {
        var descriptor = BuildMcpDescriptor(capability, "access-preview", out var diagnostics);
        validationIssues.AddRange(diagnostics.Select(ToValidationIssue));

        var server = new CapabilityExposureDescriptor(
            descriptor.Identity,
            descriptor.DisplayName,
            descriptor.Description,
            descriptor is InternalHostedMcpServerDescriptor hosted ? hosted.ImplementationKey : null,
            null,
            descriptor.ServerKey,
            null,
            descriptor.Tags,
            descriptor.OperationClassifications,
            descriptor.SideEffectProfile,
            diagnostics.Count == 0 ? descriptor.AvailabilityState : CapabilityAvailabilityState.FailedSetup,
            null);

        var tools = descriptor.AllowedTools
            .Select(tool => new CapabilityExposureDescriptor(
                new CapabilityIdentity(AccessCapabilityKind.McpTool, CapabilityKey.Create($"{descriptor.ServerKey.Value}-{ToKebab(tool.Value)}")),
                tool.Value,
                $"MCP tool exposed by {descriptor.DisplayName}.",
                null,
                null,
                descriptor.ServerKey,
                tool,
                descriptor.Tags.Concat([CapabilityTag.Create("mcp-tool")]).ToHashSet(),
                descriptor.OperationClassifications,
                descriptor.SideEffectProfile,
                descriptor.AvailabilityState,
                null))
            .ToList();

        return [server, ..tools];
    }

    private static CapabilityExposureDescriptor BuildCompatibilityExposureDescriptor(
        CapabilityEditorModel capability,
        AccessCapabilityKind kind,
        List<CapabilityValidationIssue> validationIssues)
    {
        var identity = ReadIdentity(capability, kind, validationIssues);
        return new CapabilityExposureDescriptor(
            identity,
            ResolveDisplayName(capability),
            ResolveDescription(capability),
            null,
            null,
            null,
            null,
            ReadTags(capability.Tags, validationIssues),
            new HashSet<CapabilityOperationClassification>(),
            new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false),
            CapabilityAvailabilityState.Available,
            null);
    }
}
