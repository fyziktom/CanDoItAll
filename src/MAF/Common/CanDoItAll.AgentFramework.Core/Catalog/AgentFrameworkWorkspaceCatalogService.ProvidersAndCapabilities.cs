using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceCatalogService
{
    public async Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
    {
        return await providerRegistry.ListProvidersAsync(cancellationToken);
    }

    public async Task<ProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!providerId.HasValue)
        {
            return providerProfileService.CreateEditor();
        }

        return await providerRegistry.GetProviderEditorAsync(providerId, cancellationToken);
    }

    public async Task<Guid> SaveProviderAsync(
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default)
    {
        return await providerRegistry.SaveProviderAsync(model, cancellationToken);
    }

    public async Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        await providerRegistry.DeleteProviderAsync(providerId, cancellationToken);
        await UpdateCatalogAsync(catalog => catalog with
        {
            Agents = catalog.Agents
                .Select(item => item.ProviderProfileId == providerId
                    ? item with { ProviderProfileId = null }
                    : item)
                .ToList()
        }, cancellationToken);
    }

    public async Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var provider = await providerSource.GetProviderAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");

        var result = await providerDiagnosticsService.TestProviderAsync(provider, cancellationToken);
        var checkedAtUtc = DateTimeOffset.UtcNow;

        await providerRegistry.UpdateProviderAsync(
            providerId,
            currentProvider => providerProfileService.ApplyHealthResult(currentProvider, result, checkedAtUtc),
            cancellationToken);

        return result;
    }

    public async Task<ProviderTestChatResult> RunProviderTestChatAsync(
        Guid providerId,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await providerSource.GetProviderAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");

        return await providerDiagnosticsService.RunProviderTestChatAsync(provider, request, cancellationToken);
    }

    public async Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        Guid providerId,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await providerSource.GetProviderAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");

        var result = await providerDiagnosticsService.CreateOrUpdateProviderModelAsync(provider, request, cancellationToken);
        var checkedAtUtc = DateTimeOffset.UtcNow;

        await providerRegistry.UpdateProviderAsync(
            providerId,
            currentProvider => providerProfileService.ApplyProviderModelMaintenanceResult(currentProvider, result, checkedAtUtc),
            cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Capabilities
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CapabilityEditorModel> GetCapabilityEditorAsync(
        Guid? capabilityId = null,
        CancellationToken cancellationToken = default)
    {
        if (!capabilityId.HasValue)
        {
            return new CapabilityEditorModel();
        }

        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var capability = catalog.Capabilities.FirstOrDefault(item => item.Id == capabilityId.Value)
            ?? throw new InvalidOperationException("Capability was not found.");

        var editor = CapabilityEditorModel.FromDefinition(capability);
        editor.ExpectedFingerprint = CapabilityEditorConcurrency.ComputeFingerprint(editor);
        return editor;
    }

    public async Task<Guid> SaveCapabilityAsync(
        CapabilityEditorModel model,
        CancellationToken cancellationToken = default)
    {
        LegacyMemoryCapabilityPolicy.EnsureNotRetired(model.Kind, model.Name);
        Guid capabilityId = Guid.Empty;
        await UpdateCatalogAsync(catalog =>
        {
            var current = model.Id.HasValue
                ? catalog.Capabilities.FirstOrDefault(item => item.Id == model.Id.Value)
                : null;

            if (model.Id.HasValue && current is null)
            {
                throw new InvalidOperationException($"Capability '{model.Id.Value:D}' was not found.");
            }

            if (!model.Id.HasValue && !string.IsNullOrWhiteSpace(model.ExpectedFingerprint))
            {
                throw new InvalidOperationException("A capability create cannot specify an expected fingerprint.");
            }

            if (current is not null && !string.IsNullOrWhiteSpace(model.ExpectedFingerprint))
            {
                var actualFingerprint = CapabilityEditorConcurrency.ComputeFingerprint(
                    CapabilityEditorModel.FromDefinition(current));
                if (!string.Equals(actualFingerprint, model.ExpectedFingerprint.Trim(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Capability '{current.Id:D}' changed after it was read. Reload it before saving.");
                }
            }

            var capability = new CapabilityCatalogItem(
                Id: model.Id ?? Guid.NewGuid(),
                Kind: model.Kind,
                Key: WorkspaceCatalogIdentityNormalizer.NormalizeCapabilityKey(model.Key),
                Name: model.Name.Trim(),
                Description: model.Description.Trim(),
                EndpointOrPath: model.EndpointOrPath.Trim(),
                ConfigurationJson: model.ConfigurationJson.Trim(),
                ProofStatus: current?.ProofStatus ?? CapabilityProofStatus.NotRun,
                ProofNotes: current?.ProofNotes ?? string.Empty,
                LastVerifiedAtUtc: current?.LastVerifiedAtUtc,
                IsBuiltIn: model.IsBuiltIn)
            {
                Tags = NormalizeTags(model.Tags)
            };
            capabilityId = capability.Id;
            EnsureUniqueCapabilityIdentity(catalog.Capabilities, capability);

            return catalog with
            {
                Capabilities = catalog.Capabilities
                    .Where(item => item.Id != capability.Id)
                    .Append(capability)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, cancellationToken);

        return capabilityId;
    }

    public async Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default)
    {
        await UpdateCatalogAsync(catalog => catalog with
        {
            Capabilities = catalog.Capabilities.Where(item => item.Id != capabilityId).ToList(),
            Agents = catalog.Agents
                .Select(agent => agent with
                {
                    Capabilities = agent.Capabilities
                        .Where(item => item.CapabilityId != capabilityId)
                        .ToList()
                })
                .ToList()
        }, cancellationToken);
    }

    public async Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var agent = catalog.Agents.FirstOrDefault(item => item.Id == agentId)
            ?? throw new InvalidOperationException("Agent was not found.");
        var capability = catalog.Capabilities.FirstOrDefault(item => item.Id == capabilityId)
            ?? throw new InvalidOperationException("Capability was not found.");
        var provider = agent.ProviderProfileId.HasValue
            ? await providerSource.GetProviderAsync(agent.ProviderProfileId.Value, cancellationToken)
            : null;

        var verification = await capabilityProofService.VerifyAsync(agent, provider, capability, cancellationToken);

        await UpdateCatalogAsync(currentCatalog => currentCatalog with
        {
            Agents = currentCatalog.Agents.Select(currentAgent =>
            {
                if (currentAgent.Id != agentId)
                {
                    return currentAgent;
                }

                var updatedCapabilities = currentAgent.Capabilities
                    .Select(item => item.CapabilityId == capabilityId
                        ? item with
                        {
                            ProofStatus = verification.Status,
                            LastVerifiedAtUtc = verification.CheckedAtUtc,
                            ProofNotes = verification.Notes
                        }
                        : item)
                    .ToList();

                return currentAgent with
                {
                    Capabilities = updatedCapabilities,
                    UpdatedAtUtc = verification.CheckedAtUtc
                };
            }).ToList(),
            Capabilities = currentCatalog.Capabilities
                .Select(item => item.Id == capabilityId
                    ? item with
                    {
                        ProofStatus = verification.Status,
                        LastVerifiedAtUtc = verification.CheckedAtUtc,
                        ProofNotes = verification.Notes
                    }
                    : item)
                .ToList()
        }, cancellationToken);
    }

    private static void EnsureUniqueCapabilityIdentity(IEnumerable<CapabilityCatalogItem> existingCapabilities, CapabilityCatalogItem capability)
    {
        var identityKey = WorkspaceCatalogIdentityNormalizer.GetCapabilityIdentityKey(capability);
        var collisions = existingCapabilities
            .Where(item => item.Id != capability.Id)
            .Where(item => string.Equals(WorkspaceCatalogIdentityNormalizer.GetCapabilityIdentityKey(item), identityKey, StringComparison.Ordinal))
            .Select(item => item.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (collisions.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Capability save would reuse canonical capability identity '{identityKey}', which already belongs to: {string.Join(", ", collisions)}.");
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        return tags?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().TrimStart('#').ToLowerInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }
}
