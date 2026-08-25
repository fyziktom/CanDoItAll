using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.CrmHr;

public enum AiResourceBindingStatus
{
    Unbound,
    PendingBackfill,
    Bound,
    Error
}

public sealed class AiResourceBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PartyId { get; set; }

    public Guid? TechnicalAgentId { get; set; }

    public AiResourceBindingStatus BindingStatus { get; set; } = AiResourceBindingStatus.Unbound;

    public string BindingReason { get; set; } = string.Empty;

    public string LastError { get; set; } = string.Empty;

    public AiExecutionMode? ProjectedExecutionMode { get; set; }

    public string ProjectedProviderName { get; set; } = string.Empty;

    public string ProjectedDefaultModel { get; set; } = string.Empty;

    public int ProjectedCapabilityCount { get; set; }

    public string ProjectedRoleTitle { get; set; } = string.Empty;

    public string ProjectedInstructions { get; set; } = string.Empty;

    public string ProjectedTemplateKey { get; set; } = string.Empty;

    public string ProjectedTagsJson { get; set; } = "[]";

    public string ProjectedCapabilitiesJson { get; set; } = "[]";

    public DateTimeOffset? ProjectionUpdatedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class AiResourceBindingConfiguration : IEntityTypeConfiguration<AiResourceBinding>
{
    public void Configure(EntityTypeBuilder<AiResourceBinding> builder)
    {
        builder.ToTable("CrmHr_AiResourceBindings");
        builder.HasKey(binding => binding.Id);
        builder.Property(binding => binding.BindingStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(binding => binding.BindingReason).HasColumnType("TEXT");
        builder.Property(binding => binding.LastError).HasColumnType("TEXT");
        builder.Property(binding => binding.ProjectedExecutionMode)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(binding => binding.ProjectedProviderName).HasMaxLength(200);
        builder.Property(binding => binding.ProjectedDefaultModel).HasMaxLength(200);
        builder.Property(binding => binding.ProjectedRoleTitle).HasMaxLength(200);
        builder.Property(binding => binding.ProjectedInstructions).HasColumnType("TEXT");
        builder.Property(binding => binding.ProjectedTemplateKey).HasMaxLength(200);
        builder.Property(binding => binding.ProjectedTagsJson).HasColumnType("TEXT");
        builder.Property(binding => binding.ProjectedCapabilitiesJson).HasColumnType("TEXT");
        builder.HasIndex(binding => binding.PartyId).IsUnique();
        builder.HasIndex(binding => binding.TechnicalAgentId);
    }
}

public sealed record AiTechnicalAgentDirectorySummary(
    Guid? TechnicalAgentId,
    AiResourceBindingStatus BindingStatus,
    string BindingSummary,
    AiExecutionMode? ExecutionMode,
    string ProviderName,
    string DefaultModel,
    int CapabilityCount,
    bool HasTechnicalProfile,
    string AgentsRoute);

public sealed record AiTechnicalAgentWorkspaceModel(
    Guid? TechnicalAgentId,
    AiResourceBindingStatus BindingStatus,
    string BindingSummary,
    string AgentsRoute,
    Guid? ProviderProfileId,
    string ProviderName,
    AiExecutionMode ExecutionMode,
    string DefaultModel,
    IReadOnlyList<AiCapabilityEditorModel> Capabilities,
    IReadOnlyList<AiProviderOptionModel> ProviderOptions);

public sealed record AiTechnicalAgentSaveResult(
    Guid? TechnicalAgentId,
    AiResourceBindingStatus BindingStatus,
    string BindingSummary,
    string AgentsRoute);

public sealed record AiAgentStaffingFactModel(
    Guid PartyId,
    Guid? TechnicalAgentId,
    string DisplayName,
    string RoleTitle,
    string Summary,
    string Instructions,
    AiResourceBindingStatus BindingStatus,
    string BindingSummary,
    AiExecutionMode? ExecutionMode,
    string ProviderName,
    string DefaultModel,
    string TemplateKey,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AiCapabilityEditorModel> Capabilities,
    string AgentsRoute);

public interface IAiTechnicalAgentBridge
{
    Task SynchronizeDirectoryProjectionAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken = default);

    Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken = default);

    Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(
        AiAgentProfileEditorModel model,
        CancellationToken cancellationToken = default);
}

internal sealed class LegacyAiTechnicalAgentBridge(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : IAiTechnicalAgentBridge
{
    public Task SynchronizeDirectoryProjectionAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken = default)
    {
        var requestedPartyIds = partyIds
            .Where(partyId => partyId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (requestedPartyIds.Length == 0)
        {
            return new Dictionary<Guid, AiTechnicalAgentDirectorySummary>();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profiles = await dbContext.Set<AiAgentProfile>()
            .Where(item => requestedPartyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        var bindings = await dbContext.Set<AiResourceBinding>()
            .Where(item => requestedPartyIds.Contains(item.PartyId))
            .ToListAsync(cancellationToken);
        var providerIds = profiles
            .Where(item => item.ProviderProfileId.HasValue)
            .Select(item => item.ProviderProfileId!.Value)
            .Distinct()
            .ToList();
        var providerNames = providerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<ProviderProfile>()
                .Where(item => providerIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var profileByPartyId = profiles.ToDictionary(item => item.PartyId);
        var bindingByPartyId = bindings.ToDictionary(item => item.PartyId);
        var result = new Dictionary<Guid, AiTechnicalAgentDirectorySummary>();

        foreach (var partyId in requestedPartyIds)
        {
            profileByPartyId.TryGetValue(partyId, out var profile);
            bindingByPartyId.TryGetValue(partyId, out var binding);
            var resolvedBinding = ResolveBinding(binding, profile);
            result[partyId] = new AiTechnicalAgentDirectorySummary(
                resolvedBinding.TechnicalAgentId,
                resolvedBinding.BindingStatus,
                resolvedBinding.BindingSummary,
                profile?.ExecutionMode,
                profile?.ProviderProfileId is Guid providerProfileId ? providerNames.GetValueOrDefault(providerProfileId) ?? string.Empty : string.Empty,
                profile?.DefaultModel ?? string.Empty,
                profile is null ? 0 : DeserializeCapabilities(profile.CapabilityJson, profile.Id).Count,
                profile is not null,
                resolvedBinding.AgentsRoute);
        }

        return result;
    }

    public async Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var profile = await dbContext.Set<AiAgentProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var binding = await dbContext.Set<AiResourceBinding>()
            .SingleOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);
        var providerOptions = (await dbContext.Set<ProviderProfile>()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken))
            .Select(item => new AiProviderOptionModel(
                item.Id,
                item.Name,
                item.ProviderKind?.ToString() ?? item.ConnectorPluginKey,
                item.DefaultModel,
                item.IsEnabled))
            .ToList();
        var resolvedBinding = ResolveBinding(binding, profile);
        var resolvedDefaultModel = profile is null
            ? string.Empty
            : ResolveDefaultModel(profile.DefaultModel, providerOptions.FirstOrDefault(item => item.Id == profile.ProviderProfileId)?.DefaultModel);

        return new AiTechnicalAgentWorkspaceModel(
            resolvedBinding.TechnicalAgentId,
            resolvedBinding.BindingStatus,
            resolvedBinding.BindingSummary,
            resolvedBinding.AgentsRoute,
            profile?.ProviderProfileId,
            profile?.ProviderProfileId is Guid providerProfileId ? providerOptions.FirstOrDefault(item => item.Id == providerProfileId)?.Name ?? string.Empty : string.Empty,
            profile?.ExecutionMode ?? AiExecutionMode.Remote,
            resolvedDefaultModel,
            profile is null ? [] : DeserializeCapabilities(profile.CapabilityJson, profile.Id),
            providerOptions);
    }

    public async Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(
        IReadOnlyList<Guid> partyIds,
        CancellationToken cancellationToken = default)
    {
        var summaries = await GetDirectorySummariesAsync(partyIds, cancellationToken);
        if (summaries.Count == 0)
        {
            return new Dictionary<Guid, AiAgentStaffingFactModel>();
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyIdsToLoad = summaries.Keys.ToList();
        var parties = await dbContext.Set<Party>()
            .Where(item => partyIdsToLoad.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var profiles = await dbContext.Set<AiAgentProfile>()
            .Where(item => partyIdsToLoad.Contains(item.PartyId))
            .ToDictionaryAsync(item => item.PartyId, cancellationToken);

        return partyIdsToLoad.ToDictionary(
            partyId => partyId,
            partyId =>
            {
                summaries.TryGetValue(partyId, out var summary);
                parties.TryGetValue(partyId, out var party);
                profiles.TryGetValue(partyId, out var profile);

                return new AiAgentStaffingFactModel(
                    partyId,
                    summary?.TechnicalAgentId,
                    party?.DisplayName ?? string.Empty,
                    string.Empty,
                    party?.Summary ?? string.Empty,
                    profile?.Notes ?? string.Empty,
                    summary?.BindingStatus ?? AiResourceBindingStatus.Unbound,
                    summary?.BindingSummary ?? "No technical binding.",
                    summary?.ExecutionMode,
                    summary?.ProviderName ?? string.Empty,
                    summary?.DefaultModel ?? string.Empty,
                    string.Empty,
                    [],
                    profile is null ? [] : DeserializeCapabilities(profile.CapabilityJson, profile.Id),
                    summary?.AgentsRoute ?? "/agents?tab=agents");
            });
    }

    public async Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(
        AiAgentProfileEditorModel model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ProviderProfile? provider = null;
        if (model.ProviderProfileId is Guid providerProfileId)
        {
            provider = await dbContext.Set<ProviderProfile>()
                .SingleOrDefaultAsync(item => item.Id == providerProfileId, cancellationToken);
            if (provider is null)
            {
                return Result<AiTechnicalAgentSaveResult>.Failure(
                    Error.Validation("Provider profile must reference an existing workspace provider.", "crmhr.ai-agent.provider-invalid"));
            }
        }

        var profile = await dbContext.Set<AiAgentProfile>()
            .SingleOrDefaultAsync(item => item.PartyId == model.PartyId, cancellationToken);
        if (profile is null)
        {
            profile = new AiAgentProfile
            {
                PartyId = model.PartyId
            };
            dbContext.Set<AiAgentProfile>().Add(profile);
        }

        var normalizedExtendedData = NormalizeJson(model.ExtendedDataJson, "{}");
        if (normalizedExtendedData is null)
        {
            return Result<AiTechnicalAgentSaveResult>.Failure(
                Error.Validation("Extended data must be valid JSON.", "crmhr.ai-agent.extended-data-invalid"));
        }

        profile.ProviderProfileId = model.ProviderProfileId;
        profile.DefaultModel = ResolveDefaultModel(model.DefaultModel, provider?.DefaultModel);
        profile.ExecutionMode = model.ExecutionMode;
        profile.CapabilityJson = SerializeCapabilities(model.Capabilities);
        profile.ExtendedDataJson = normalizedExtendedData;

        var binding = await dbContext.Set<AiResourceBinding>()
            .SingleOrDefaultAsync(item => item.PartyId == model.PartyId, cancellationToken);
        if (binding is null)
        {
            binding = new AiResourceBinding
            {
                PartyId = model.PartyId,
                CreatedAtUtc = clock.GetUtcNow()
            };
            dbContext.Set<AiResourceBinding>().Add(binding);
        }

        binding.TechnicalAgentId ??= profile.Id;
        binding.BindingStatus = AiResourceBindingStatus.PendingBackfill;
        binding.BindingReason = "Legacy CRM-HR technical profile pending AgentFramework backfill.";
        binding.LastError = string.Empty;
        binding.UpdatedAtUtc = clock.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AiTechnicalAgentSaveResult>.Success(new AiTechnicalAgentSaveResult(
            binding.TechnicalAgentId,
            binding.BindingStatus,
            binding.BindingReason,
            binding.TechnicalAgentId.HasValue ? $"/agents?tab=agents&agentId={binding.TechnicalAgentId.Value:D}" : "/agents?tab=agents"));
    }

    private static (Guid? TechnicalAgentId, AiResourceBindingStatus BindingStatus, string BindingSummary, string AgentsRoute) ResolveBinding(
        AiResourceBinding? binding,
        AiAgentProfile? profile)
    {
        if (binding is not null)
        {
            var route = binding.TechnicalAgentId.HasValue
                ? $"/agents?tab=agents&agentId={binding.TechnicalAgentId.Value:D}"
                : "/agents?tab=agents";
            var summary = string.IsNullOrWhiteSpace(binding.BindingReason)
                ? binding.BindingStatus.ToString()
                : binding.BindingReason;
            return (binding.TechnicalAgentId, binding.BindingStatus, summary, route);
        }

        if (profile is not null)
        {
            return (
                profile.Id,
                AiResourceBindingStatus.PendingBackfill,
                "Legacy CRM-HR technical profile pending AgentFramework backfill.",
                $"/agents?tab=agents&agentId={profile.Id:D}");
        }

        return (null, AiResourceBindingStatus.Unbound, "No technical binding.", "/agents?tab=agents");
    }

    private static List<AiCapabilityEditorModel> DeserializeCapabilities(string json, Guid profileId)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AiCapabilityEditorModel>>(json)?
                .Where(HasCapabilityContent)
                .Select(CloneCapability)
                .ToList()
                ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"AI agent profile '{profileId}' contains invalid capability JSON.");
        }
    }

    private static string SerializeCapabilities(IEnumerable<AiCapabilityEditorModel> capabilities)
    {
        return JsonSerializer.Serialize(capabilities
            .Where(HasCapabilityContent)
            .Select(CloneCapability)
            .ToList());
    }

    private static AiCapabilityEditorModel CloneCapability(AiCapabilityEditorModel capability)
    {
        return new AiCapabilityEditorModel
        {
            Name = capability.Name.Trim(),
            Scope = capability.Scope.Trim(),
            ToolAccess = capability.ToolAccess.Trim(),
            Limitations = capability.Limitations.Trim(),
            Notes = capability.Notes.Trim()
        };
    }

    private static bool HasCapabilityContent(AiCapabilityEditorModel capability)
    {
        return !string.IsNullOrWhiteSpace(capability.Name)
            || !string.IsNullOrWhiteSpace(capability.Scope)
            || !string.IsNullOrWhiteSpace(capability.ToolAccess)
            || !string.IsNullOrWhiteSpace(capability.Limitations)
            || !string.IsNullOrWhiteSpace(capability.Notes);
    }

    private static string ResolveDefaultModel(string preferredModel, string? providerDefaultModel)
    {
        return string.IsNullOrWhiteSpace(preferredModel)
            ? providerDefaultModel?.Trim() ?? string.Empty
            : preferredModel.Trim();
    }

    private static string? NormalizeJson(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(input);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
