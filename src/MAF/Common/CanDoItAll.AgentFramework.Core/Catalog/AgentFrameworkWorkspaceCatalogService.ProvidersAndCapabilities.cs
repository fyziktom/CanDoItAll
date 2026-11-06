using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceCatalogService {
    public async Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default) {
        return await providerRegistry.ListProvidersAsync(cancellationToken);
    }

    public async Task<ProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default) {
        if (!providerId.HasValue) {
            return providerProfileService.CreateEditor();
        }

        return await providerRegistry.GetProviderEditorAsync(providerId, cancellationToken);
    }

    public async Task<Guid> SaveProviderAsync(
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default) {
        return await providerRegistry.SaveProviderAsync(model, cancellationToken);
    }

    public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        => DeleteProviderCoreAsync(providerId, null, cancellationToken);

    public Task DeleteProviderAsync(Guid providerId, Guid expectedConcurrencyToken, CancellationToken cancellationToken = default)
        => DeleteProviderCoreAsync(providerId, expectedConcurrencyToken, cancellationToken);

    private async Task DeleteProviderCoreAsync(Guid providerId, Guid? expectedConcurrencyToken, CancellationToken cancellationToken) {
        if (expectedConcurrencyToken is { } expected) {
            await providerRegistry.DeleteProviderAsync(providerId, expected, cancellationToken);
        } else {
            await providerRegistry.DeleteProviderAsync(providerId, cancellationToken);
        }
        await UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents
                .Select(item => item.ProviderProfileId == providerId
                    ? item with { ProviderProfileId = null, UpdatedAtUtc = AgentConfigurationVersion.NextRevision(item.UpdatedAtUtc, DateTimeOffset.UtcNow) }
                    : item)
                .ToList()
        }, cancellationToken);
    }

    public async Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) {
        var observation = await providerRegistry.GetProviderObservationAsync(providerId, cancellationToken);
        var provider = observation.Profile;

        if (IsSourceManagedProvider(provider) && !provider.IsEnabled) {
            return new ProviderHealthResult(
                false,
                $"The source-managed provider is unavailable ({provider.HealthStatus}).",
                provider.SuggestedModels);
        }

        ProviderHealthResult result;
        if (!IsSourceManagedProvider(provider) && observation.ExpectedConcurrencyToken == Guid.Empty) {
            throw new ProviderProfileValidationException("Save this provider configuration before persisting a diagnostic.");
        }
        try {
            result = await providerDiagnosticsService.TestProviderAsync(
                provider,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException &&
                IsSourceManagedProvider(provider)) {
            result = new ProviderHealthResult(
                false,
                ProviderFailureDisclosurePolicy.SelectMessage(
                    provider,
                    ProviderFailureOperation.HealthCheck,
                    exception.Message),
                provider.SuggestedModels);
        }

        if (IsSourceManagedProvider(provider)) {
            return ProviderFailureDisclosurePolicy.SanitizeHealthResult(
                provider,
                result);
        }

        var checkedAtUtc = DateTimeOffset.UtcNow;

        await providerRegistry.RecordProviderHealthAsync(observation, result, checkedAtUtc, cancellationToken);

        return result;
    }

    public async Task<ProviderTestChatResult> RunProviderTestChatAsync(
        Guid providerId,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default) {
        var provider = await providerSource.GetProviderAsync(providerId, cancellationToken)
            ?? throw new InvalidOperationException("Provider profile was not found.");

        EnsureProviderAvailable(provider);

        try {
            return await providerDiagnosticsService.RunProviderTestChatAsync(
                provider,
                request,
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException &&
                IsSourceManagedProvider(provider)) {
            throw ProviderFailureDisclosurePolicy.CreateBoundaryException(
                provider,
                ProviderFailureOperation.RuntimeRequest,
                exception);
        }
    }

    public async Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
        Guid providerId,
        ProviderModelMaintenanceEditorRequest request,
        CancellationToken cancellationToken = default) {
        var observation = await providerRegistry.GetProviderObservationAsync(providerId, cancellationToken);
        var provider = observation.Profile;

        if (IsSourceManagedProvider(provider) || observation.ExpectedConcurrencyToken == Guid.Empty) {
            throw new ProviderProfileValidationException("Model maintenance requires a saved, locally managed provider configuration.");
        }

        EnsureProviderAvailable(provider);

        var result = await providerDiagnosticsService.CreateOrUpdateProviderModelAsync(provider, request, cancellationToken);
        var checkedAtUtc = DateTimeOffset.UtcNow;

        try {
            var editor = providerProfileService.CreateEditor(providerProfileService.ApplyProviderModelMaintenanceResult(provider, result, checkedAtUtc));
            editor.ExpectedConcurrencyToken = observation.ExpectedConcurrencyToken;
            await providerRegistry.SaveProviderEditorAsync(editor, cancellationToken);
        } catch (ProviderConfigurationConflictException exception) {
            throw new ProviderModelMaintenancePersistenceException(providerId, result, ProviderModelMaintenancePersistenceState.Conflict, exception);
        } catch (Exception exception) when (exception is ProviderProfileValidationException or UnauthorizedAccessException) {
            throw new ProviderModelMaintenancePersistenceException(providerId, result, ProviderModelMaintenancePersistenceState.Rejected, exception);
        } catch (Exception exception) {
            throw new ProviderModelMaintenancePersistenceException(providerId, result,
                exception is IProviderConfigurationFailure { CanonicalCommitSucceeded: true }
                    ? ProviderModelMaintenancePersistenceState.CommittedWithWarning : ProviderModelMaintenancePersistenceState.Unconfirmed, exception);
        }

        return result;
    }

    private static bool IsSourceManagedProvider(ProviderProfile provider)
        => ProviderFailureDisclosurePolicy.RequiresSanitization(provider);

    private static void EnsureProviderAvailable(ProviderProfile provider) {
        if (!provider.IsEnabled) {
            throw new ProviderRuntimeProfileUnavailableException(provider.Id);
        }
    }

    public async Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Capabilities
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<CapabilityEditorModel> GetCapabilityEditorAsync(
        Guid? capabilityId = null,
        CancellationToken cancellationToken = default) {
        if (!capabilityId.HasValue) {
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
        => (await SaveCapabilityEditorAsync(model, cancellationToken)).Id!.Value;

    public async Task<CapabilityEditorModel> SaveCapabilityEditorAsync(
        CapabilityEditorModel model,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);
        model = new() {
            Id = model.Id,
            ExpectedFingerprint = model.ExpectedFingerprint,
            Kind = model.Kind,
            Key = model.Key,
            Name = model.Name,
            Description = model.Description,
            EndpointOrPath = model.EndpointOrPath,
            ConfigurationJson = model.ConfigurationJson,
            IsBuiltIn = model.IsBuiltIn,
            Tags = model.Tags.ToList()
        };
        LegacyMemoryCapabilityPolicy.EnsureNotRetired(model.Kind, model.Name);
        Guid capabilityId = Guid.Empty;
        return await UpdateCatalogAsync(catalog => {
            var current = model.Id.HasValue
                ? catalog.Capabilities.FirstOrDefault(item => item.Id == model.Id.Value)
                : null;

            if (model.Id.HasValue && current is null) {
                throw new CapabilityCatalogRejectedException($"Capability '{model.Id.Value:D}' was not found. Its original can no longer be saved.",
                    isConcurrencyConflict: true);
            }

            if (!model.Id.HasValue && !string.IsNullOrWhiteSpace(model.ExpectedFingerprint)) {
                throw new CapabilityCatalogRejectedException("A capability create cannot specify an expected fingerprint.");
            }

            if (current is not null) {
                var actualFingerprint = CapabilityEditorConcurrency.ComputeFingerprint(
                    CapabilityEditorModel.FromDefinition(current));
                if (string.IsNullOrWhiteSpace(model.ExpectedFingerprint) ||
                    !string.Equals(actualFingerprint, model.ExpectedFingerprint.Trim(), StringComparison.Ordinal)) {
                    throw new CapabilityCatalogRejectedException(
                        $"Capability '{current.Id:D}' changed after it was read. Reload it before saving.",
                        isConcurrencyConflict: true);
                }
            }

            if (model.IsBuiltIn != (current?.IsBuiltIn ?? false)) {
                throw new CapabilityCatalogRejectedException("Built-in capability trust is owned by the installed catalog and cannot be changed by an editor command.");
            }

            var normalizedKey = WorkspaceCatalogIdentityNormalizer.NormalizeCapabilityKey(model.Key);
            var capability = new CapabilityCatalogItem(
                Id: model.Id ?? Guid.NewGuid(),
                Kind: model.Kind,
                Key: normalizedKey,
                Name: model.Name.Trim(),
                Description: model.Description.Trim(),
                EndpointOrPath: model.EndpointOrPath.Trim(),
                ConfigurationJson: InlineSkillConfigurationNormalizer.Normalize(
                    model.Kind,
                    normalizedKey,
                    model.ConfigurationJson),
                ProofStatus: current?.ProofStatus ?? CapabilityProofStatus.NotRun,
                ProofNotes: current?.ProofNotes ?? string.Empty,
                LastVerifiedAtUtc: current?.LastVerifiedAtUtc,
                IsBuiltIn: model.IsBuiltIn) {
                Tags = NormalizeTags(model.Tags)
            };
            capabilityId = capability.Id;
            EnsureUniqueCapabilityIdentity(catalog.Capabilities, capability);

            return catalog with {
                Capabilities = catalog.Capabilities
                    .Where(item => item.Id != capability.Id)
                    .Append(capability)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }, catalog => {
            var accepted = CapabilityEditorModel.FromDefinition(catalog.Capabilities.Single(item => item.Id == capabilityId));
            accepted.ExpectedFingerprint = CapabilityEditorConcurrency.ComputeFingerprint(accepted);
            return accepted;
        }, cancellationToken);
    }

    public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default)
        => DeleteCapabilityCoreAsync(capabilityId, null, cancellationToken);

    public Task DeleteCapabilityAsync(Guid capabilityId, string expectedFingerprint, CancellationToken cancellationToken = default)
        => DeleteCapabilityCoreAsync(capabilityId, expectedFingerprint, cancellationToken);

    private async Task DeleteCapabilityCoreAsync(Guid capabilityId, string? expectedFingerprint, CancellationToken cancellationToken) {
        var now = DateTimeOffset.UtcNow;
        await UpdateCatalogAsync(catalog => {
            var current = catalog.Capabilities.SingleOrDefault(item => item.Id == capabilityId);
            if (current is null) {
                return catalog;
            }
            if (string.IsNullOrWhiteSpace(expectedFingerprint) || !string.Equals(expectedFingerprint.Trim(),
                CapabilityEditorConcurrency.ComputeFingerprint(CapabilityEditorModel.FromDefinition(current)), StringComparison.Ordinal)) {
                throw new CapabilityCatalogRejectedException("The capability changed or its original fingerprint is missing. Review the saved definition before deleting it.",
                    isConcurrencyConflict: true);
            }
            if (current.IsBuiltIn) {
                throw new CapabilityCatalogRejectedException("A built-in capability cannot be deleted.");
            }
            return catalog with {
                Capabilities = catalog.Capabilities.Where(item => item.Id != capabilityId).ToList(),
                Agents = catalog.Agents.Select(agent => agent.Capabilities.All(item => item.CapabilityId != capabilityId)
                    ? agent : agent with {
                        Capabilities = agent.Capabilities.Where(item => item.CapabilityId != capabilityId).ToList(),
                        UpdatedAtUtc = AgentConfigurationVersion.NextRevision(agent.UpdatedAtUtc, now)
                    }).ToList()
            };
        }, _ => capabilityId, cancellationToken);
    }

    public async Task<CapabilityVerificationOutcome> VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) {
        if (providerSource is not IProviderRuntimeProfileSnapshotSource snapshots) {
            throw new CapabilityVerificationException(new(CapabilityVerificationDisposition.InfrastructureUnavailable));
        }
        var outcome = await new CapabilityVerificationPublication(store, capabilityProofService, snapshots)
            .ExecuteAsync(agentId, capabilityId, cancellationToken);
        if (outcome.Disposition != CapabilityVerificationDisposition.Committed) {
            throw new CapabilityVerificationException(outcome);
        }
        return outcome;
    }

    private static void EnsureUniqueCapabilityIdentity(IEnumerable<CapabilityCatalogItem> existingCapabilities, CapabilityCatalogItem capability) {
        var identityKey = WorkspaceCatalogIdentityNormalizer.GetCapabilityIdentityKey(capability);
        var collisions = existingCapabilities
            .Where(item => item.Id != capability.Id)
            .Where(item => string.Equals(WorkspaceCatalogIdentityNormalizer.GetCapabilityIdentityKey(item), identityKey, StringComparison.Ordinal))
            .Select(item => item.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (collisions.Count == 0) {
            return;
        }

        throw new CapabilityCatalogRejectedException(
            $"Capability save would reuse canonical capability identity '{identityKey}', which already belongs to: {string.Join(", ", collisions)}.");
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags) {
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
