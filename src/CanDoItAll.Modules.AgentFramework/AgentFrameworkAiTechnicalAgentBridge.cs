using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkAiTechnicalAgentBridge(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICanDoItAllAgentWorkspaceFactory workspaceFactory,
    IClock clock) : IAiTechnicalAgentBridge
{
    public async Task SynchronizeDirectoryProjectionAsync(
        CancellationToken cancellationToken = default)
    {
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        if (agents.Count == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidatePartyIds = agents
            .SelectMany(agent =>
            {
                var metadata = AgentFrameworkCrmHrMetadata.Read(agent.ConfigurationJson);
                var taggedPartyId = ResolveTaggedPartyId(agent.Tags);
                return new Guid?[]
                {
                    metadata is null ? null : metadata.PartyId,
                    taggedPartyId
                };
            })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToHashSet();
        var bindings = await dbContext.Set<AiResourceBinding>()
            .ToListAsync(cancellationToken);
        var parties = await dbContext.Set<Party>()
            .Where(item => item.PartyType == PartyType.AiAgent || candidatePartyIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var partiesById = parties.ToDictionary(item => item.Id);
        var bindingsByTechnicalAgentId = bindings
            .Where(item => item.TechnicalAgentId.HasValue)
            .GroupBy(item => item.TechnicalAgentId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.UpdatedAtUtc)
                    .ThenByDescending(item => item.CreatedAtUtc)
                    .ToList());
        var bindingByTechnicalAgentId = bindingsByTechnicalAgentId
            .ToDictionary(group => group.Key, group => group.Value[0]);
        var bindingByPartyId = bindings.ToDictionary(item => item.PartyId);
        var timestamp = clock.GetUtcNow();
        var changed = false;

        foreach (var agent in agents)
        {
            var metadata = AgentFrameworkCrmHrMetadata.Read(agent.ConfigurationJson);
            var taggedPartyId = ResolveTaggedPartyId(agent.Tags);
            var preferredPartyId = metadata is null
                ? taggedPartyId
                : metadata.PartyId;

            var binding = preferredPartyId.HasValue
                ? bindingByPartyId.GetValueOrDefault(preferredPartyId.Value)
                : null;
            binding ??= bindingByTechnicalAgentId.GetValueOrDefault(agent.Id);
            var partyId = binding?.PartyId ?? preferredPartyId;
            if (preferredPartyId.HasValue)
            {
                partyId = preferredPartyId.Value;
            }

            Party? party = null;
            if (partyId.HasValue)
            {
                partiesById.TryGetValue(partyId.Value, out party);
            }

            if (party is not null && party.PartyType != PartyType.AiAgent)
            {
                party = null;
                partyId = null;
            }

            if (party is null)
            {
                party = new Party
                {
                    Id = partyId ?? Guid.NewGuid(),
                    PartyType = PartyType.AiAgent,
                    LifecycleStatus = MapLifecycleStatus(agent.Status),
                    DisplayName = agent.Name.Trim(),
                    Summary = BuildPartySummary(agent),
                    ExtendedDataJson = "{}",
                    TagsJson = "[]",
                    LastChangedBy = "agent-framework-sync",
                    CreatedAtUtc = timestamp,
                    UpdatedAtUtc = timestamp
                };
                dbContext.Set<Party>().Add(party);
                partiesById[party.Id] = party;
                changed = true;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(party.DisplayName))
                {
                    party.DisplayName = agent.Name.Trim();
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(party.Summary))
                {
                    party.Summary = BuildPartySummary(agent);
                    changed = true;
                }
            }

            if (binding is null)
            {
                binding = bindingByPartyId.GetValueOrDefault(party.Id);
            }

            if (binding is null)
            {
                binding = new AiResourceBinding
                {
                    PartyId = party.Id,
                    TechnicalAgentId = agent.Id,
                    BindingStatus = AiResourceBindingStatus.Bound,
                    BindingReason = "Projected from AgentFramework organization catalog.",
                    LastError = string.Empty,
                    CreatedAtUtc = timestamp,
                    UpdatedAtUtc = timestamp
                };
                dbContext.Set<AiResourceBinding>().Add(binding);
                bindingByTechnicalAgentId[agent.Id] = binding;
                bindingByPartyId[party.Id] = binding;
                changed = true;
            }
            else
            {
                var bindingChanged = false;
                if (binding.PartyId != party.Id)
                {
                    bindingByPartyId.Remove(binding.PartyId);
                    binding.PartyId = party.Id;
                    bindingByPartyId[party.Id] = binding;
                    bindingChanged = true;
                }

                if (binding.TechnicalAgentId != agent.Id)
                {
                    binding.TechnicalAgentId = agent.Id;
                    bindingChanged = true;
                }

                if (binding.BindingStatus != AiResourceBindingStatus.Bound)
                {
                    binding.BindingStatus = AiResourceBindingStatus.Bound;
                    bindingChanged = true;
                }

                if (!string.Equals(binding.BindingReason, "Projected from AgentFramework organization catalog.", StringComparison.Ordinal))
                {
                    binding.BindingReason = "Projected from AgentFramework organization catalog.";
                    bindingChanged = true;
                }

                if (!string.IsNullOrWhiteSpace(binding.LastError))
                {
                    binding.LastError = string.Empty;
                    bindingChanged = true;
                }

                if (bindingChanged)
                {
                    binding.UpdatedAtUtc = timestamp;
                    changed = true;
                }
            }

            if (bindingsByTechnicalAgentId.TryGetValue(agent.Id, out var duplicateBindings))
            {
                foreach (var duplicateBinding in duplicateBindings.Where(item => item.Id != binding.Id))
                {
                    var duplicateChanged = false;
                    if (duplicateBinding.TechnicalAgentId.HasValue)
                    {
                        duplicateBinding.TechnicalAgentId = null;
                        duplicateChanged = true;
                    }

                    if (duplicateBinding.BindingStatus != AiResourceBindingStatus.Error)
                    {
                        duplicateBinding.BindingStatus = AiResourceBindingStatus.Error;
                        duplicateChanged = true;
                    }

                    var duplicateReason = $"Superseded by AgentFramework party projection for '{agent.Name}'.";
                    if (!string.Equals(duplicateBinding.BindingReason, duplicateReason, StringComparison.Ordinal))
                    {
                        duplicateBinding.BindingReason = duplicateReason;
                        duplicateChanged = true;
                    }

                    var duplicateError = $"Technical agent '{agent.Name}' is already bound to CRM party '{party.Id:D}'.";
                    if (!string.Equals(duplicateBinding.LastError, duplicateError, StringComparison.Ordinal))
                    {
                        duplicateBinding.LastError = duplicateError;
                        duplicateChanged = true;
                    }

                    if (duplicateChanged)
                    {
                        duplicateBinding.UpdatedAtUtc = timestamp;
                        changed = true;
                    }
                }
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken = default)
    {
        if (partyIds.Count == 0)
        {
            return new Dictionary<Guid, AiTechnicalAgentDirectorySummary>();
        }

        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var providerNames = providers.ToDictionary(item => item.Id, item => item.Name);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var bindings = await dbContext.Set<AiResourceBinding>()
            .Where(item => partyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        var bindingByPartyId = bindings.ToDictionary(item => item.PartyId);

        var result = new Dictionary<Guid, AiTechnicalAgentDirectorySummary>();
        foreach (var partyId in partyIds)
        {
            var resolved = ResolveBoundAgent(bindingByPartyId.GetValueOrDefault(partyId), agents, partyId);
            if (resolved.Agent is null)
            {
                result[partyId] = new AiTechnicalAgentDirectorySummary(
                    resolved.Binding?.TechnicalAgentId,
                    resolved.Binding?.BindingStatus ?? AiResourceBindingStatus.Unbound,
                    ResolveBindingSummary(resolved.Binding, missingAgent: resolved.Binding?.TechnicalAgentId is not null),
                    null,
                    string.Empty,
                    string.Empty,
                    0,
                    false,
                    BuildAgentsRoute(resolved.Binding?.TechnicalAgentId));
                continue;
            }

            var metadata = AgentFrameworkCrmHrMetadata.Read(resolved.Agent.ConfigurationJson);
            var resolvedModel = string.IsNullOrWhiteSpace(resolved.Agent.Model)
                ? resolved.Agent.ProviderProfileId is Guid providerId
                    ? providers.FirstOrDefault(item => item.Id == providerId)?.DefaultModel ?? string.Empty
                    : string.Empty
                : resolved.Agent.Model;
            var capabilityCount = metadata?.Capabilities.Count ?? resolved.Agent.Capabilities.Count;
            result[partyId] = new AiTechnicalAgentDirectorySummary(
                resolved.Agent.Id,
                resolved.Binding?.BindingStatus ?? AiResourceBindingStatus.Bound,
                ResolveBindingSummary(resolved.Binding, missingAgent: false),
                metadata?.ExecutionMode,
                resolved.Agent.ProviderProfileId is Guid resolvedProviderId ? providerNames.GetValueOrDefault(resolvedProviderId) ?? string.Empty : string.Empty,
                resolvedModel,
                capabilityCount,
                true,
                BuildAgentsRoute(resolved.Agent.Id));
        }

        return result;
    }

    public async Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var providerOptions = providers
            .Select(item => new AiProviderOptionModel(
                item.Id,
                item.Name,
                item.Kind.ToString(),
                item.DefaultModel,
                item.IsEnabled))
            .ToList();

        var binding = await LoadBindingAsync(partyId, cancellationToken);
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var resolved = ResolveBoundAgent(binding, agents, partyId);
        var metadata = resolved.Agent is null
            ? null
            : AgentFrameworkCrmHrMetadata.Read(resolved.Agent.ConfigurationJson);
        var providerName = resolved.Agent?.ProviderProfileId is Guid providerId
            ? providerOptions.FirstOrDefault(item => item.Id == providerId)?.Name ?? string.Empty
            : string.Empty;
        var defaultModel = string.IsNullOrWhiteSpace(resolved.Agent?.Model)
            ? resolved.Agent?.ProviderProfileId is Guid resolvedProviderId
                ? providerOptions.FirstOrDefault(item => item.Id == resolvedProviderId)?.DefaultModel ?? string.Empty
                : string.Empty
            : resolved.Agent.Model;

        return new AiTechnicalAgentWorkspaceModel(
            resolved.Agent?.Id ?? binding?.TechnicalAgentId,
            binding?.BindingStatus ?? (resolved.Agent is null ? AiResourceBindingStatus.Unbound : AiResourceBindingStatus.Bound),
            ResolveBindingSummary(binding, missingAgent: binding?.TechnicalAgentId is not null && resolved.Agent is null),
            BuildAgentsRoute(resolved.Agent?.Id ?? binding?.TechnicalAgentId),
            resolved.Agent?.ProviderProfileId,
            providerName,
            metadata?.ExecutionMode ?? AiExecutionMode.Remote,
            defaultModel,
            metadata?.Capabilities ?? [],
            providerOptions);
    }

    public async Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(
        AiAgentProfileEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var party = await dbContext.Set<Party>()
                .SingleOrDefaultAsync(item => item.Id == model.PartyId, cancellationToken);
            if (party is null)
            {
                return Result<AiTechnicalAgentSaveResult>.Failure(
                    Error.Validation("AI agent party was not found.", "crmhr.ai-agent.party-not-found"));
            }

            var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
            var providers = await workspaceService.ListProvidersAsync(cancellationToken);
            var selectedProvider = model.ProviderProfileId.HasValue
                ? providers.FirstOrDefault(item => item.Id == model.ProviderProfileId.Value)
                : null;
            if (model.ProviderProfileId.HasValue && selectedProvider is null)
            {
                return Result<AiTechnicalAgentSaveResult>.Failure(
                    Error.Validation("Provider profile must reference an existing workspace provider.", "crmhr.ai-agent.provider-invalid"));
            }

            var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
            var binding = await dbContext.Set<AiResourceBinding>()
                .SingleOrDefaultAsync(item => item.PartyId == model.PartyId, cancellationToken);
            var resolved = ResolveBoundAgent(binding, agents, model.PartyId);
            var editor = resolved.Agent is null
                ? await workspaceService.GetAgentEditorAsync(null, cancellationToken)
                : await workspaceService.GetAgentEditorAsync(resolved.Agent.Id, cancellationToken);

            var normalizedCapabilities = AgentFrameworkCrmHrMetadata.NormalizeCapabilities(model.Capabilities);
            editor.Name = party.DisplayName;
            editor.RoleTitle = string.IsNullOrWhiteSpace(editor.RoleTitle)
                ? "AI resource"
                : editor.RoleTitle;
            editor.Summary = string.IsNullOrWhiteSpace(party.Summary)
                ? $"{party.DisplayName} technical runtime profile."
                : party.Summary.Trim();
            editor.Instructions = AgentFrameworkCrmHrMetadata.BuildInstructions(
                editor.Instructions,
                party.DisplayName,
                model.Notes,
                normalizedCapabilities);
            editor.ProviderProfileId = model.ProviderProfileId;
            editor.Model = string.IsNullOrWhiteSpace(model.DefaultModel)
                ? selectedProvider?.DefaultModel ?? string.Empty
                : model.DefaultModel.Trim();
            editor.Status = AgentLifecycleStatus.Active;
            editor.ConfigurationJson = AgentFrameworkCrmHrMetadata.Write(
                editor.ConfigurationJson,
                model.PartyId,
                model.ExecutionMode,
                normalizedCapabilities);
            editor.Tags = AgentFrameworkCrmHrMetadata.EnsurePartyTag(editor.Tags, model.PartyId)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var technicalAgentId = await workspaceService.SaveAgentAsync(editor, cancellationToken);
            var timestamp = clock.GetUtcNow();
            if (binding is null)
            {
                binding = new AiResourceBinding
                {
                    PartyId = model.PartyId,
                    CreatedAtUtc = timestamp
                };
                dbContext.Set<AiResourceBinding>().Add(binding);
            }

            binding.TechnicalAgentId = technicalAgentId;
            binding.BindingStatus = AiResourceBindingStatus.Bound;
            binding.BindingReason = "Bound to AgentFramework organization catalog.";
            binding.LastError = string.Empty;
            binding.UpdatedAtUtc = timestamp;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<AiTechnicalAgentSaveResult>.Success(
                new AiTechnicalAgentSaveResult(
                    technicalAgentId,
                    binding.BindingStatus,
                    binding.BindingReason,
                    BuildAgentsRoute(technicalAgentId)));
        }
        catch (Exception exception)
        {
            await MarkBindingErrorAsync(model.PartyId, exception.Message, cancellationToken);
            return Result<AiTechnicalAgentSaveResult>.Failure(
                Error.Failure(exception.Message, "crmhr.ai-agent.agentframework-save-failed"));
        }
    }

    private async Task<AiResourceBinding?> LoadBindingAsync(
        Guid partyId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<AiResourceBinding>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
    }

    private async Task MarkBindingErrorAsync(
        Guid partyId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var binding = await dbContext.Set<AiResourceBinding>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        if (binding is null)
        {
            binding = new AiResourceBinding
            {
                PartyId = partyId,
                CreatedAtUtc = clock.GetUtcNow()
            };
            dbContext.Set<AiResourceBinding>().Add(binding);
        }

        binding.BindingStatus = AiResourceBindingStatus.Error;
        binding.BindingReason = "AgentFramework binding failed.";
        binding.LastError = errorMessage;
        binding.UpdatedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static (AiResourceBinding? Binding, AgentDefinition? Agent) ResolveBoundAgent(
        AiResourceBinding? binding,
        IReadOnlyList<AgentDefinition> agents,
        Guid partyId)
    {
        AgentDefinition? agent = null;
        if (binding?.TechnicalAgentId is Guid technicalAgentId)
        {
            agent = agents.FirstOrDefault(item => item.Id == technicalAgentId);
        }

        if (agent is null)
        {
            agent = agents.FirstOrDefault(item =>
            {
                var metadata = AgentFrameworkCrmHrMetadata.Read(item.ConfigurationJson);
                return metadata?.PartyId == partyId ||
                       item.Tags.Contains(AgentFrameworkCrmHrMetadata.BuildPartyTag(partyId), StringComparer.OrdinalIgnoreCase);
            });
        }

        return (binding, agent);
    }

    private static string ResolveBindingSummary(
        AiResourceBinding? binding,
        bool missingAgent)
    {
        if (binding is null)
        {
            return "No technical binding.";
        }

        if (missingAgent)
        {
            return string.IsNullOrWhiteSpace(binding.LastError)
                ? "Binding points to a missing technical agent."
                : binding.LastError;
        }

        return string.IsNullOrWhiteSpace(binding.BindingReason)
            ? binding.BindingStatus.ToString()
            : binding.BindingReason;
    }

    private static string BuildAgentsRoute(
        Guid? agentId)
    {
        return agentId.HasValue
            ? $"/agents?tab=agents&agentId={agentId.Value:D}"
            : "/agents?tab=agents";
    }

    private static PartyLifecycleStatus MapLifecycleStatus(
        AgentLifecycleStatus status)
    {
        return status switch
        {
            AgentLifecycleStatus.Active => PartyLifecycleStatus.Active,
            AgentLifecycleStatus.Suspended => PartyLifecycleStatus.Inactive,
            AgentLifecycleStatus.Archived => PartyLifecycleStatus.Archived,
            _ => PartyLifecycleStatus.Draft
        };
    }

    private static string BuildPartySummary(
        AgentDefinition agent)
    {
        return string.IsNullOrWhiteSpace(agent.Summary)
            ? $"{agent.RoleTitle} technical runtime profile.".Trim()
            : agent.Summary.Trim();
    }

    private static Guid? ResolveTaggedPartyId(
        IReadOnlyList<string> tags)
    {
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            const string prefix = "party-";
            if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tag[prefix.Length..];
            if (Guid.TryParseExact(value, "N", out var partyId))
            {
                return partyId;
            }

            if (Guid.TryParse(value, out partyId))
            {
                return partyId;
            }
        }

        return null;
    }
}
