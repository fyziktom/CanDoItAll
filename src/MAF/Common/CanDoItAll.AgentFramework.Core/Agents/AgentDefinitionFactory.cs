using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentDefinitionFactory
{
    public static AgentDefinition Create(
        SandboxWorkspaceCatalog catalog,
        AgentEditorModel model,
        Guid id,
        AgentDefinition? existingAgent,
        DateTimeOffset now,
        IProviderProfileService providerProfileService,
        string operationLabel)
    {
        var normalizedTemplateKey = WorkspaceCatalogIdentityNormalizer.NormalizeTemplateKey(
            model.TemplateKey,
            model.Name);
        EnsureUniqueTemplateKey(catalog.Agents, id, normalizedTemplateKey, operationLabel);

        var configurationJson = AgentProjectStructureAccessMetadata.Write(
            model.ConfigurationJson,
            model.ProjectStructureAccess);
        configurationJson = AgentProcessAccessMetadata.Write(configurationJson, model.ProcessAccess);
        configurationJson = AgentWorkspaceToolAccessMetadata.Write(configurationJson, model.WorkspaceToolAccess);
        configurationJson = AgentImageGenerationAccessMetadata.Write(configurationJson, model.ImageGenerationAccess);
        configurationJson = AgentVoiceAccessMetadata.Write(configurationJson, model.VoiceAccess);
        configurationJson = AgentMemoryAccessMetadata.Write(configurationJson, model.MemoryAccess);
        configurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(configurationJson);

        var capabilities = catalog.Capabilities
            .Where(item => model.SelectedCapabilityIds.Contains(item.Id))
            .Select(item =>
            {
                var existingCapability = existingAgent?.Capabilities
                    .FirstOrDefault(capability => capability.CapabilityId == item.Id);

                return new AgentCapabilityAssignment(
                    item.Id,
                    item.Key,
                    item.Kind,
                    existingCapability?.ProofStatus ?? item.ProofStatus,
                    existingCapability?.LastVerifiedAtUtc ?? item.LastVerifiedAtUtc,
                    existingCapability?.ProofNotes ?? item.ProofNotes);
            })
            .ToList();

        var normalizedModel = NormalizeAgentModelForSave(model.Model);
        EnsureManualModelOverrideHasPricing(
            model.ProviderProfileId,
            normalizedModel,
            catalog.Providers.Select(providerProfileService.NormalizeImportedProfile).ToList());

        return new AgentDefinition(
            Id: id,
            Name: model.Name.Trim(),
            RoleTitle: model.RoleTitle.Trim(),
            Summary: model.Summary.Trim(),
            Instructions: model.Instructions.Trim(),
            Status: model.Status,
            ProviderProfileId: model.ProviderProfileId,
            Model: normalizedModel,
            Workload: model.Workload,
            ChatHistoryMode: model.ChatHistoryMode,
            Temperature: model.Temperature,
            RequirePerServiceCallChatHistoryPersistence: model.RequirePerServiceCallChatHistoryPersistence,
            EnableBackgroundResponses: model.EnableBackgroundResponses,
            ConfigurationJson: configurationJson.Trim(),
            IsTemplate: model.IsTemplate,
            TemplateKey: normalizedTemplateKey,
            Permissions: model.Permissions with
            {
                AllowedSecrets = NormalizeAllowedSecretReferences(model.AllowedSecretReferences)
            },
            Capabilities: capabilities,
            Tags: model.Tags
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CreatedAtUtc: existingAgent?.CreatedAtUtc ?? now,
            UpdatedAtUtc: now)
        {
            AvatarImageUrl = string.IsNullOrWhiteSpace(model.AvatarImageUrl)
                ? null
                : model.AvatarImageUrl.Trim()
        };
    }

    public static void EnsureUniqueTemplateKey(
        IEnumerable<AgentDefinition> agents,
        Guid currentAgentId,
        string templateKey,
        string operationLabel)
    {
        var collisions = agents
            .Where(item => item.Id != currentAgentId)
            .Where(item => string.Equals(
                WorkspaceCatalogIdentityNormalizer.GetAgentTemplateIdentity(item),
                templateKey,
                StringComparison.Ordinal))
            .Select(item => item.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (collisions.Count > 0)
        {
            throw new InvalidOperationException(
                $"{operationLabel} would reuse canonical template key '{templateKey}', which already belongs to: {string.Join(", ", collisions)}.");
        }
    }

    private static string NormalizeAgentModelForSave(string? model)
        => string.IsNullOrWhiteSpace(model) ? string.Empty : model.Trim();

    private static void EnsureManualModelOverrideHasPricing(
        Guid? providerProfileId,
        string normalizedModel,
        IReadOnlyList<ProviderProfile> providers)
    {
        if (!providerProfileId.HasValue || string.IsNullOrWhiteSpace(normalizedModel))
        {
            return;
        }

        var provider = providers.FirstOrDefault(item => item.Id == providerProfileId.Value);
        if (provider is null || ProviderPricingDefaults.TryFindPrice(provider.ModelPrices, normalizedModel, out _))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Agent model override '{normalizedModel}' for provider '{provider.Name}' requires a model price row on the provider profile.");
    }

    private static IReadOnlyList<AgentAllowedSecretReference> NormalizeAllowedSecretReferences(
        IEnumerable<AgentAllowedSecretReference> references)
    {
        return references
            .Where(item => item.SecretId != Guid.Empty)
            .GroupBy(item => item.SecretId)
            .Select(group =>
            {
                var item = group.Last();
                return new AgentAllowedSecretReference(
                    item.SecretId,
                    item.NameSnapshot.Trim(),
                    string.IsNullOrWhiteSpace(item.Purpose)
                        ? AgentSecretPurposes.GeneralAgentRequest
                        : item.Purpose.Trim());
            })
            .OrderBy(item => item.NameSnapshot, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SecretId)
            .ToList();
    }
}
