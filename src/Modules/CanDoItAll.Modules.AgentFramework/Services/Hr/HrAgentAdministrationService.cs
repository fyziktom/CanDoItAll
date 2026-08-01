using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class HrAgentAdministrationService(
    IAgentFrameworkWorkspaceService workspaceService,
    ILogger<HrAgentAdministrationService> logger)
{
    private const int MaximumSearchTake = 100;
    private const int MaximumNameLength = 120;
    private const int MaximumRoleTitleLength = 160;
    private const int MaximumSummaryLength = 2_000;
    private const int MaximumInstructionsLength = 30_000;
    private const int MaximumTagCount = 20;
    private const int MaximumTagLength = 64;

    public async Task<HrAgentSearchResult> SearchAsync(
        HrAgentsSearchInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Take is < 1 or > MaximumSearchTake)
        {
            throw new ArgumentOutOfRangeException(nameof(input), $"Take must be between 1 and {MaximumSearchTake}.");
        }

        ValidateOptionalEnum(input.Status, nameof(input.Status));
        ValidateOptionalEnum(input.Workload, nameof(input.Workload));

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: true, cancellationToken);
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var providerNames = providers.ToDictionary(item => item.Id, item => item.Name);
        var query = input.Query?.Trim() ?? string.Empty;
        var matches = agents
            .Where(agent => !input.Status.HasValue || agent.Status == input.Status.Value)
            .Where(agent => !input.Workload.HasValue || agent.Workload == input.Workload.Value)
            .Where(agent => MatchesQuery(agent, query))
            .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(agent => agent.Id)
            .ToList();
        var items = matches
            .Take(input.Take)
            .Select(agent => MapSearchItem(agent, providerNames))
            .ToArray();

        return new HrAgentSearchResult(items, items.Length, matches.Count > items.Length);
    }

    public async Task<HrAgentSafeSettings> GetSettingsAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var agent = await GetAgentAsync(agentId, cancellationToken);
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var capabilities = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        return MapSettings(agent, providers, capabilities);
    }

    public async Task<HrAgentCreationOptionsResult> GetCreationOptionsAsync(
        CancellationToken cancellationToken)
    {
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var capabilities = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        var teams = await workspaceService.ListAgentTeamsAsync(cancellationToken);

        return new HrAgentCreationOptionsResult(
            providers
                .Where(provider => provider.IsEnabled && provider.Purpose == ProviderProfilePurpose.Chat)
                .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
                .Select(provider => new HrAgentProviderOption(
                    provider.Id,
                    provider.Name,
                    provider.Kind,
                    provider.Purpose,
                    provider.DefaultModel,
                    provider.SuggestedModels
                        .Concat(provider.ModelPrices.Select(price => price.Model))
                        .Where(model => !string.IsNullOrWhiteSpace(model))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    provider.IsEnabled))
                .ToArray(),
            capabilities
                .Where(capability => !ManagedAgentPrivilegedCapabilityKeys.All.Contains(capability.Key))
                .OrderBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
                .Select(MapCapability)
                .ToArray(),
            teams
                .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
                .Select(team => new HrAgentTeamOption(team.Id, team.Name, team.Description))
                .ToArray(),
            Enum.GetValues<AgentLifecycleStatus>(),
            Enum.GetValues<AgentWorkloadKind>(),
            Enum.GetValues<AgentChatHistoryMode>());
    }

    public async Task<HrAgentMutationResult> CreateAsync(
        Guid actorAgentId,
        HrAgentCreateInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        await EnsureAuthorizedActorAsync(
            actorAgentId,
            AgentToolInvocationPolicyMetadata.HrAgentCreate,
            cancellationToken);
        ValidateCreateInput(input);
        var projectStructureAccess = ApplyProjectStructureAccessPatch(
            new AgentProjectStructureAccessSettings(),
            input.ProjectStructureAccess);

        var id = Guid.NewGuid();
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var provider = ResolveChatProvider(providers, input.ProviderProfileId);
        var capabilities = await ValidateCapabilitySelectionAsync(input.CapabilityIds, cancellationToken);
        var teams = await workspaceService.ListAgentTeamsAsync(cancellationToken);
        var team = ResolveTeam(teams, input.TeamId);
        var permissions = input.Permissions ?? new HrAgentPermissionsInput();
        var editor = new AgentEditorModel
        {
            Id = id,
            Name = input.Name.Trim(),
            RoleTitle = input.RoleTitle.Trim(),
            Summary = input.Summary?.Trim() ?? string.Empty,
            Instructions = input.Instructions.Trim(),
            Status = AgentLifecycleStatus.Draft,
            ProviderProfileId = provider?.Id,
            Model = ResolveModel(provider, input.Model),
            Workload = input.Workload,
            ChatHistoryMode = input.ChatHistoryMode,
            Temperature = input.Temperature,
            TemplateKey = $"hr-created-{id:N}",
            Permissions = new AgentPermissionsPolicy(
                permissions.CanUseTools,
                permissions.CanAskOtherAgents,
                permissions.CanEscalateToHuman,
                permissions.CanObserveOtherAgents,
                permissions.CanScheduleWork,
                permissions.RequiresApprovalForExternalCalls,
                AutoApproveExternalCallsByDefault: false,
                AllowedSecrets: []),
            ProjectStructureAccess = projectStructureAccess,
            SelectedCapabilityIds = capabilities.Select(item => item.Id).ToList(),
            Tags = NormalizeTags(input.Tags).ToList()
        };

        var warnings = await SaveAndInspectProjectionOutcomeAsync(
            actorAgentId,
            id,
            "create",
            () => workspaceService.SaveAgentAsync(editor, cancellationToken));
        if (team is not null)
        {
            try
            {
                await workspaceService.UpdateAgentTeamMembersAsync(
                    team.Id,
                    team.AgentIds.Append(id).Distinct().ToArray(),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "HR agent {ActorAgentId} created target agent {TargetAgentId}, but team {TeamId} assignment failed.",
                    actorAgentId,
                    id,
                    team.Id);
                warnings.Add($"Agent was created, but assignment to team '{team.Name}' failed. Assign it manually after inspecting the team.");
            }
        }

        var created = await GetAgentAsync(id, cancellationToken);
        warnings.AddRange(BuildReadinessWarnings(created));
        logger.LogInformation(
            "HR agent {ActorAgentId} created target agent {TargetAgentId} with {CapabilityCount} capabilities.",
            actorAgentId,
            id,
            created.Capabilities.Count);
        return MapMutationResult(created, warnings);
    }

    public async Task<HrAgentMutationResult> UpdateAsync(
        Guid actorAgentId,
        HrAgentSettingsUpdateInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        await EnsureAuthorizedActorAsync(
            actorAgentId,
            AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate,
            cancellationToken);
        EnsureTargetCanBeManaged(actorAgentId, input.AgentId);
        if (input.ExpectedUpdatedAtUtc == default)
        {
            throw new InvalidOperationException("ExpectedUpdatedAtUtc is required for an agent settings update.");
        }

        if (input.ProviderProfileId.HasValue && input.ClearProviderProfile)
        {
            throw new InvalidOperationException("ProviderProfileId and ClearProviderProfile cannot be supplied together.");
        }

        ValidateOptionalText(input.Name, MaximumNameLength, nameof(input.Name), allowEmpty: false);
        ValidateOptionalText(input.RoleTitle, MaximumRoleTitleLength, nameof(input.RoleTitle), allowEmpty: false);
        ValidateOptionalText(input.Summary, MaximumSummaryLength, nameof(input.Summary), allowEmpty: true);
        ValidateOptionalText(input.Instructions, MaximumInstructionsLength, nameof(input.Instructions), allowEmpty: false);
        ValidateTemperature(input.Temperature);
        ValidateOptionalEnum(input.Status, nameof(input.Status));
        ValidateOptionalEnum(input.Workload, nameof(input.Workload));
        ValidateOptionalEnum(input.ChatHistoryMode, nameof(input.ChatHistoryMode));

        var current = await GetAgentAsync(input.AgentId, cancellationToken);
        var editor = await workspaceService.GetAgentEditorAsync(input.AgentId, cancellationToken);
        editor.ExpectedUpdatedAtUtc = input.ExpectedUpdatedAtUtc;
        editor.Name = input.Name is null ? editor.Name : input.Name.Trim();
        editor.RoleTitle = input.RoleTitle is null ? editor.RoleTitle : input.RoleTitle.Trim();
        editor.Summary = input.Summary is null ? editor.Summary : input.Summary.Trim();
        editor.Instructions = input.Instructions is null ? editor.Instructions : input.Instructions.Trim();
        editor.Status = input.Status ?? editor.Status;
        editor.Workload = input.Workload ?? editor.Workload;
        editor.ChatHistoryMode = input.ChatHistoryMode ?? editor.ChatHistoryMode;
        editor.Temperature = input.Temperature ?? editor.Temperature;

        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        if (input.ClearProviderProfile)
        {
            editor.ProviderProfileId = null;
            editor.Model = string.Empty;
        }
        else if (input.ProviderProfileId.HasValue)
        {
            var provider = ResolveChatProvider(providers, input.ProviderProfileId);
            editor.ProviderProfileId = provider!.Id;
            editor.Model = ResolveModel(provider, input.Model);
        }
        else if (input.Model is not null)
        {
            var provider = ResolveChatProvider(providers, editor.ProviderProfileId);
            editor.Model = ResolveModel(provider, input.Model);
        }

        if (input.CapabilityIds is not null)
        {
            var capabilities = await ValidateCapabilitySelectionAsync(input.CapabilityIds, cancellationToken);
            editor.SelectedCapabilityIds = capabilities.Select(item => item.Id).ToList();
        }

        if (input.Tags is not null)
        {
            editor.Tags = NormalizeTags(input.Tags).ToList();
        }

        if (input.Permissions is not null)
        {
            editor.Permissions = ApplyPermissionsPatch(editor.Permissions, input.Permissions);
        }

        if (input.ProjectStructureAccess is not null)
        {
            editor.ProjectStructureAccess = ApplyProjectStructureAccessPatch(
                editor.ProjectStructureAccess,
                input.ProjectStructureAccess);
        }

        var warnings = await SaveAndInspectProjectionOutcomeAsync(
            actorAgentId,
            input.AgentId,
            "update",
            () => workspaceService.SaveAgentAsync(editor, cancellationToken));
        var updated = await GetAgentAsync(input.AgentId, cancellationToken);
        warnings.AddRange(BuildReadinessWarnings(updated));
        logger.LogInformation(
            "HR agent {ActorAgentId} updated target agent {TargetAgentId} from timestamp {PreviousUpdatedAtUtc} to {UpdatedAtUtc}.",
            actorAgentId,
            updated.Id,
            current.UpdatedAtUtc,
            updated.UpdatedAtUtc);
        return MapMutationResult(updated, warnings);
    }

    private async Task<List<string>> SaveAndInspectProjectionOutcomeAsync(
        Guid actorAgentId,
        Guid targetAgentId,
        string operation,
        Func<Task<Guid>> save)
    {
        try
        {
            await save();
            return [];
        }
        catch (AgentDirectoryProjectionSynchronizationException exception)
        {
            logger.LogError(
                exception,
                "HR agent {ActorAgentId} catalog {Operation} for target agent {TargetAgentId} persisted, but downstream projection synchronization failed.",
                actorAgentId,
                operation,
                targetAgentId);
            return ["The technical-agent catalog change persisted, but CRM projection synchronization failed. Inspect the CRM AI-agent binding before relying on it."];
        }
    }

    private async Task<IReadOnlyList<CapabilityCatalogItem>> ValidateCapabilitySelectionAsync(
        IReadOnlyList<Guid>? requestedIds,
        CancellationToken cancellationToken)
    {
        var requested = requestedIds ?? [];
        if (requested.Any(id => id == Guid.Empty))
        {
            throw new InvalidOperationException("Capability IDs cannot contain an empty GUID.");
        }

        var ids = requested
            .Distinct()
            .ToArray();
        var catalog = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        var selected = catalog.Where(item => ids.Contains(item.Id)).ToArray();
        var missing = ids.Except(selected.Select(item => item.Id)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Unknown capability IDs: {string.Join(", ", missing.Select(id => id.ToString("D")))}.");
        }

        var privileged = selected.Where(item => ManagedAgentPrivilegedCapabilityKeys.All.Contains(item.Key)).ToArray();
        if (privileged.Length > 0)
        {
            throw new InvalidOperationException($"Privileged managed-agent capabilities cannot be granted by an HR runtime tool: {string.Join(", ", privileged.Select(item => item.Key))}.");
        }

        return selected;
    }

    private static AgentPermissionsPolicy ApplyPermissionsPatch(
        AgentPermissionsPolicy current,
        HrAgentPermissionsPatch patch)
    {
        return current with
        {
            CanUseTools = patch.CanUseTools ?? current.CanUseTools,
            CanAskOtherAgents = patch.CanAskOtherAgents ?? current.CanAskOtherAgents,
            CanEscalateToHuman = patch.CanEscalateToHuman ?? current.CanEscalateToHuman,
            CanObserveOtherAgents = patch.CanObserveOtherAgents ?? current.CanObserveOtherAgents,
            CanScheduleWork = patch.CanScheduleWork ?? current.CanScheduleWork,
            RequiresApprovalForExternalCalls = patch.RequiresApprovalForExternalCalls ?? current.RequiresApprovalForExternalCalls
        };
    }

    private static ProviderProfile? ResolveChatProvider(
        IReadOnlyList<ProviderProfile> providers,
        Guid? providerId)
    {
        if (!providerId.HasValue)
        {
            return null;
        }

        var provider = providers.FirstOrDefault(item => item.Id == providerId.Value)
            ?? throw new InvalidOperationException($"Provider '{providerId.Value:D}' was not found.");
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException($"Provider '{provider.Name}' is disabled.");
        }

        if (provider.Purpose != ProviderProfilePurpose.Chat)
        {
            throw new InvalidOperationException($"Provider '{provider.Name}' is not a chat provider.");
        }

        return provider;
    }

    private static string ResolveModel(ProviderProfile? provider, string? requestedModel)
    {
        if (provider is null)
        {
            if (!string.IsNullOrWhiteSpace(requestedModel))
            {
                throw new InvalidOperationException("A model cannot be selected without a chat provider.");
            }

            return string.Empty;
        }

        var model = string.IsNullOrWhiteSpace(requestedModel)
            ? provider.DefaultModel
            : requestedModel.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException($"Provider '{provider.Name}' does not define a default model; select one explicitly.");
        }

        return model;
    }

    private static AgentTeamDefinition? ResolveTeam(
        IReadOnlyList<AgentTeamDefinition> teams,
        Guid? teamId)
    {
        if (!teamId.HasValue)
        {
            return null;
        }

        return teams.FirstOrDefault(team => team.Id == teamId.Value)
            ?? throw new InvalidOperationException($"Agent team '{teamId.Value:D}' was not found.");
    }

    private async Task<AgentDefinition> GetAgentAsync(Guid agentId, CancellationToken cancellationToken)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("Agent id cannot be empty.", nameof(agentId));
        }

        return (await workspaceService.ListAgentsAsync(includeTemplates: true, cancellationToken))
            .FirstOrDefault(agent => agent.Id == agentId)
            ?? throw new InvalidOperationException($"Agent '{agentId:D}' was not found.");
    }

    private static HrAgentSafeSettings MapSettings(
        AgentDefinition agent,
        IReadOnlyList<ProviderProfile> providers,
        IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        var provider = agent.ProviderProfileId.HasValue
            ? providers.FirstOrDefault(item => item.Id == agent.ProviderProfileId.Value)
            : null;
        var capabilityLookup = capabilities.ToDictionary(item => item.Id);
        return new HrAgentSafeSettings(
            agent.Id,
            agent.Name,
            agent.RoleTitle,
            agent.Summary,
            agent.Instructions,
            MapAvatarMetadata(agent.AvatarImageUrl),
            agent.Status,
            agent.ProviderProfileId,
            provider?.Name ?? string.Empty,
            agent.Model,
            agent.Workload,
            agent.ChatHistoryMode,
            agent.Temperature,
            new HrAgentSafePermissions(
                agent.Permissions.CanUseTools,
                agent.Permissions.CanAskOtherAgents,
                agent.Permissions.CanEscalateToHuman,
                agent.Permissions.CanObserveOtherAgents,
                agent.Permissions.CanScheduleWork,
                agent.Permissions.RequiresApprovalForExternalCalls,
                agent.Permissions.AutoApproveExternalCallsByDefault),
            MapProjectStructureAccess(agent.ConfigurationJson),
            agent.Capabilities
                .Select(assignment => capabilityLookup.TryGetValue(assignment.CapabilityId, out var capability)
                    ? MapCapability(capability) with { ProofStatus = assignment.ProofStatus }
                    : new HrAgentCapabilityDescriptor(
                        assignment.CapabilityId,
                        assignment.CapabilityKey,
                        assignment.Kind,
                        assignment.CapabilityKey,
                        assignment.ProofStatus))
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            agent.Tags,
            agent.CreatedAtUtc,
            agent.UpdatedAtUtc);
    }

    private static HrAgentSafeProjectStructureAccess MapProjectStructureAccess(
        string? configurationJson)
    {
        var access = AgentProjectStructureAccessMetadata.Read(configurationJson);
        return new HrAgentSafeProjectStructureAccess(
            access.CanRead,
            access.CanWrite,
            access.CanWriteNonTaskStructure,
            access.CanWriteTasks,
            access.CanCreateProjects,
            access.CanCreateSubprojects,
            access.AllowAllProjects,
            access.AllowedProjectIds.ToArray());
    }

    private static HrAgentAvatarMetadata MapAvatarMetadata(string? avatarImageUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarImageUrl))
        {
            return new HrAgentAvatarMetadata(
                IsPresent: false,
                HrAgentAvatarKind.None,
                ContentType: string.Empty,
                ByteCount: null);
        }

        var value = avatarImageUrl.AsSpan().Trim();
        if (value.StartsWith("data:".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MapEmbeddedAvatarMetadata(value);
        }

        if (IsBundledAvatar(value))
        {
            return new HrAgentAvatarMetadata(
                IsPresent: true,
                HrAgentAvatarKind.BundledAsset,
                ContentType: "image/jpeg",
                ByteCount: null);
        }

        return new HrAgentAvatarMetadata(
            IsPresent: true,
            HrAgentAvatarKind.ExternalReference,
            ContentType: string.Empty,
            ByteCount: null);
    }

    private static HrAgentAvatarMetadata MapEmbeddedAvatarMetadata(ReadOnlySpan<char> dataUrl)
    {
        var separatorIndex = dataUrl.IndexOf(',');
        if (separatorIndex < 0)
        {
            return InvalidEmbeddedAvatarMetadata();
        }

        var header = dataUrl[5..separatorIndex];
        const string Base64Suffix = ";base64";
        if (!header.EndsWith(Base64Suffix.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return InvalidEmbeddedAvatarMetadata();
        }

        var suppliedContentType = header[..^Base64Suffix.Length];
        var normalizedContentType = AgentAvatarImagePolicy.TryNormalizeContentType(
            suppliedContentType,
            out var contentType)
            ? contentType
            : string.Empty;
        int? byteCount = TryGetBase64ByteCount(dataUrl[(separatorIndex + 1)..], out var count)
            ? count
            : null;
        return new HrAgentAvatarMetadata(
            IsPresent: true,
            HrAgentAvatarKind.EmbeddedData,
            normalizedContentType,
            byteCount);
    }

    private static bool IsBundledAvatar(ReadOnlySpan<char> value)
    {
        foreach (var bundledAvatarUrl in AgentAvatarImageCatalog.BundledAvatarUrls)
        {
            if (value.Equals(bundledAvatarUrl.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static HrAgentAvatarMetadata InvalidEmbeddedAvatarMetadata()
    {
        return new HrAgentAvatarMetadata(
            IsPresent: true,
            HrAgentAvatarKind.EmbeddedData,
            ContentType: string.Empty,
            ByteCount: null);
    }

    private static bool TryGetBase64ByteCount(ReadOnlySpan<char> encoded, out int byteCount)
    {
        byteCount = 0;
        if (encoded.IsEmpty || encoded.Length % 4 != 0)
        {
            return false;
        }

        var paddingCount = encoded[^1] == '=' ? 1 : 0;
        if (encoded.Length > 1 && encoded[^2] == '=')
        {
            paddingCount++;
        }

        var contentLength = encoded.Length - paddingCount;
        for (var index = 0; index < contentLength; index++)
        {
            var character = encoded[index];
            if (!IsBase64Character(character))
            {
                return false;
            }
        }

        for (var index = contentLength; index < encoded.Length; index++)
        {
            if (encoded[index] != '=')
            {
                return false;
            }
        }

        byteCount = checked((encoded.Length / 4 * 3) - paddingCount);
        return true;
    }

    private static bool IsBase64Character(char character)
    {
        return character is >= 'A' and <= 'Z' or
            >= 'a' and <= 'z' or
            >= '0' and <= '9' or
            '+' or '/';
    }

    private static HrAgentCapabilityDescriptor MapCapability(CapabilityCatalogItem capability)
    {
        return new HrAgentCapabilityDescriptor(
            capability.Id,
            capability.Key,
            capability.Kind,
            capability.Name,
            capability.ProofStatus);
    }

    private static HrAgentSearchItem MapSearchItem(
        AgentDefinition agent,
        IReadOnlyDictionary<Guid, string> providerNames)
    {
        var providerName = agent.ProviderProfileId.HasValue
            ? providerNames.GetValueOrDefault(agent.ProviderProfileId.Value) ?? string.Empty
            : string.Empty;
        return new HrAgentSearchItem(
            agent.Id,
            agent.Name,
            agent.RoleTitle,
            agent.Summary,
            agent.Status,
            agent.Workload,
            providerName,
            agent.Model,
            agent.Capabilities.Count,
            agent.Tags,
            agent.UpdatedAtUtc);
    }

    private static bool MatchesQuery(AgentDefinition agent, string query)
    {
        return string.IsNullOrWhiteSpace(query) ||
               agent.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               agent.RoleTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               agent.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               agent.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateCreateInput(HrAgentCreateInput input)
    {
        ValidateRequiredText(input.Name, MaximumNameLength, nameof(input.Name));
        ValidateRequiredText(input.RoleTitle, MaximumRoleTitleLength, nameof(input.RoleTitle));
        ValidateOptionalText(input.Summary, MaximumSummaryLength, nameof(input.Summary), allowEmpty: true);
        ValidateRequiredText(input.Instructions, MaximumInstructionsLength, nameof(input.Instructions));
        ValidateTemperature(input.Temperature);
        if (!Enum.IsDefined(input.Workload) || !Enum.IsDefined(input.ChatHistoryMode))
        {
            throw new InvalidOperationException("Workload and chat-history mode must be defined enum values.");
        }
    }

    private static AgentProjectStructureAccessSettings ApplyProjectStructureAccessPatch(
        AgentProjectStructureAccessSettings current,
        HrAgentProjectStructureAccessInput? patch)
    {
        ArgumentNullException.ThrowIfNull(current);
        var normalizedCurrent = AgentProjectStructureAccessMetadata.Normalize(current);
        if (patch is null)
        {
            return normalizedCurrent;
        }

        var projectIdsWereSupplied = patch.AllowedProjectIds is not null;
        var requestedProjectIds = patch.AllowedProjectIds ?? normalizedCurrent.AllowedProjectIds;
        if (projectIdsWereSupplied && requestedProjectIds.Any(projectId => projectId == Guid.Empty))
        {
            throw new InvalidOperationException("Project-structure access IDs cannot contain an empty GUID.");
        }

        if (patch.AllowAllProjects == true &&
            projectIdsWereSupplied &&
            requestedProjectIds.Count > 0)
        {
            throw new InvalidOperationException(
                "AllowAllProjects cannot be combined with explicit project-structure access IDs.");
        }

        var allowedProjectIds = requestedProjectIds
            .Distinct()
            .OrderBy(projectId => projectId)
            .ToList();
        var allowAllProjects = patch.AllowAllProjects ?? normalizedCurrent.AllowAllProjects;
        if (patch.AllowAllProjects == true)
        {
            allowedProjectIds.Clear();
        }
        else if (projectIdsWereSupplied && allowedProjectIds.Count > 0)
        {
            allowAllProjects = false;
        }

        return AgentProjectStructureAccessMetadata.Normalize(
            new AgentProjectStructureAccessSettings
            {
                CanRead = (patch.CanRead ?? normalizedCurrent.CanRead) ||
                    allowAllProjects ||
                    allowedProjectIds.Count > 0,
                CanWrite = patch.CanWrite ?? normalizedCurrent.CanWrite,
                CanWriteNonTaskStructure =
                    patch.CanWriteNonTaskStructure ?? normalizedCurrent.CanWriteNonTaskStructure,
                CanWriteTasks = patch.CanWriteTasks ?? normalizedCurrent.CanWriteTasks,
                CanCreateProjects = patch.CanCreateProjects ?? normalizedCurrent.CanCreateProjects,
                CanCreateSubprojects =
                    patch.CanCreateSubprojects ?? normalizedCurrent.CanCreateSubprojects,
                AllowAllProjects = allowAllProjects,
                AllowedProjectIds = allowedProjectIds
            });
    }

    private static void ValidateTemperature(double? temperature)
    {
        if (temperature is < 0d or > 2d)
        {
            throw new InvalidOperationException("Temperature must be between 0 and 2.");
        }
    }

    private static void ValidateRequiredText(string? value, int maximumLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} cannot be empty.");
        }

        ValidateOptionalText(value, maximumLength, fieldName, allowEmpty: false);
    }

    private static void ValidateOptionalEnum<TEnum>(TEnum? value, string fieldName)
        where TEnum : struct, Enum
    {
        if (value.HasValue && !Enum.IsDefined(value.Value))
        {
            throw new InvalidOperationException($"{fieldName} must be a defined enum value.");
        }
    }

    private static void ValidateOptionalText(
        string? value,
        int maximumLength,
        string fieldName,
        bool allowEmpty)
    {
        if (value is null)
        {
            return;
        }

        if (!allowEmpty && string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} cannot be empty.");
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new InvalidOperationException($"{fieldName} cannot exceed {maximumLength} characters.");
        }
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        var normalized = (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length > MaximumTagCount)
        {
            throw new InvalidOperationException($"An agent can have at most {MaximumTagCount} tags.");
        }

        if (normalized.Any(tag => tag.Length > MaximumTagLength))
        {
            throw new InvalidOperationException($"Agent tags cannot exceed {MaximumTagLength} characters.");
        }

        return normalized;
    }

    private async Task EnsureAuthorizedActorAsync(
        Guid actorAgentId,
        string toolName,
        CancellationToken cancellationToken)
    {
        if (actorAgentId != HrAgentIdentity.AgentId)
        {
            throw new UnauthorizedAccessException("Only the managed HR agent can use agent-administration services.");
        }

        var actor = (await workspaceService.ListAgentsAsync(includeTemplates: true, cancellationToken))
            .FirstOrDefault(agent => agent.Id == actorAgentId);
        if (!HrAgentRuntimeAuthorizationPolicy.IsManagedHrActor(actor))
        {
            throw new UnauthorizedAccessException("Only the active managed HR agent can use agent-administration services.");
        }

        var capabilityCatalog = await workspaceService.ListCapabilitiesAsync(cancellationToken);
        if (!HrAgentRuntimeAuthorizationPolicy.IsToolAuthorized(
                actor!,
                capabilityCatalog,
                toolName,
                requiresCrmScope: false))
        {
            throw new UnauthorizedAccessException(
                $"The managed HR agent is not authorized to invoke '{toolName}'.");
        }
    }

    private static void EnsureTargetCanBeManaged(Guid actorAgentId, Guid targetAgentId)
    {
        if (targetAgentId == Guid.Empty)
        {
            throw new ArgumentException("Target agent id cannot be empty.", nameof(targetAgentId));
        }

        if (targetAgentId == actorAgentId || targetAgentId == HrAgentIdentity.AgentId)
        {
            throw new InvalidOperationException("The managed HR agent cannot update its own identity or authority.");
        }
    }

    private static IReadOnlyList<string> BuildReadinessWarnings(AgentDefinition agent)
    {
        var warnings = new List<string>();
        if (!agent.ProviderProfileId.HasValue)
        {
            warnings.Add("No chat provider is assigned.");
        }

        if (agent.Capabilities.Count == 0)
        {
            warnings.Add("No capabilities are assigned.");
        }

        return warnings;
    }

    private static HrAgentMutationResult MapMutationResult(
        AgentDefinition agent,
        IReadOnlyList<string> warnings)
    {
        return new HrAgentMutationResult(
            agent.Id,
            agent.Name,
            agent.Status,
            agent.UpdatedAtUtc,
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }
}
