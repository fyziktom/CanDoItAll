using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentChatContextLimits
{
    public const int MaximumSourceKindLength = 100;
    public const int MaximumSourceIdLength = 512;
    public const int MaximumContributorIdLength = 200;
    public const int MaximumDisplayNameLength = 200;
    public const int MaximumScopeLabelLength = 200;
    public const int MaximumFragments = 16;
    public const int MaximumAggregateContentLength = 64_000;
    public const int MaximumTransientContextLength = MaximumAggregateContentLength + 2_048;
    public const int MaximumAgentAccessEntries = 500;
}

public readonly record struct AgentChatHandleId
{
    public AgentChatHandleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An active chat handle id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static AgentChatHandleId Create()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString("N");
}

public readonly record struct AgentChatContextScopeId
{
    public AgentChatContextScopeId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An agent chat context scope id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static AgentChatContextScopeId Create()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString("N");
}

public readonly record struct AgentChatContextSourceKind
{
    public AgentChatContextSourceKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Trim().Length > AgentChatContextLimits.MaximumSourceKindLength)
        {
            throw new ArgumentException(
                $"An agent chat context source kind cannot exceed {AgentChatContextLimits.MaximumSourceKindLength} characters.",
                nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; } = string.Empty;

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString()
        => Value;
}

public readonly record struct AgentChatContextSourceId
{
    public AgentChatContextSourceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Trim().Length > AgentChatContextLimits.MaximumSourceIdLength)
        {
            throw new ArgumentException(
                $"An agent chat context source id cannot exceed {AgentChatContextLimits.MaximumSourceIdLength} characters.",
                nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; } = string.Empty;

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString()
        => Value;
}

public readonly record struct AgentChatContextContributorId
{
    public AgentChatContextContributorId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Trim().Length > AgentChatContextLimits.MaximumContributorIdLength)
        {
            throw new ArgumentException(
                $"An agent chat context contributor id cannot exceed {AgentChatContextLimits.MaximumContributorIdLength} characters.",
                nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; } = string.Empty;

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString()
        => Value;
}

public sealed record AgentChatContextSource
{
    public AgentChatContextSource(
        AgentChatContextSourceKind kind,
        AgentChatContextSourceId id)
    {
        if (kind.IsEmpty)
        {
            throw new ArgumentException("An agent chat context source kind is required.", nameof(kind));
        }

        if (id.IsEmpty)
        {
            throw new ArgumentException("An agent chat context source id is required.", nameof(id));
        }

        Kind = kind;
        Id = id;
    }

    public AgentChatContextSourceKind Kind { get; }

    public AgentChatContextSourceId Id { get; }
}

[Flags]
public enum AgentChatContextPermission
{
    None = 0,
    Read = 1,
    Mutate = 2
}

public sealed record AgentChatContextAgentAccess
{
    public AgentChatContextAgentAccess(
        Guid agentId,
        AgentChatContextPermission permissions,
        string scopeLabel)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        if (permissions == AgentChatContextPermission.None)
        {
            throw new ArgumentException("At least one context permission is required.", nameof(permissions));
        }

        const AgentChatContextPermission allowedPermissions =
            AgentChatContextPermission.Read |
            AgentChatContextPermission.Mutate;
        if ((permissions & ~allowedPermissions) != AgentChatContextPermission.None)
        {
            throw new ArgumentException("The context permissions contain an undefined value.", nameof(permissions));
        }

        if (permissions.HasFlag(AgentChatContextPermission.Mutate) &&
            !permissions.HasFlag(AgentChatContextPermission.Read))
        {
            throw new ArgumentException("Context mutation permission requires read permission.", nameof(permissions));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(scopeLabel);
        if (scopeLabel.Trim().Length > AgentChatContextLimits.MaximumScopeLabelLength)
        {
            throw new ArgumentException(
                $"An agent chat context scope label cannot exceed {AgentChatContextLimits.MaximumScopeLabelLength} characters.",
                nameof(scopeLabel));
        }

        AgentId = agentId;
        Permissions = permissions;
        ScopeLabel = scopeLabel.Trim();
    }

    public Guid AgentId { get; }

    public AgentChatContextPermission Permissions { get; }

    public string ScopeLabel { get; }

    public bool CanRead => Permissions.HasFlag(AgentChatContextPermission.Read);

    public bool CanMutate => Permissions.HasFlag(AgentChatContextPermission.Mutate);
}

public sealed record AgentChatContextFragment
{
    public const int MaximumContentLength = 32_000;

    public AgentChatContextFragment(
        AgentChatContextContributorId contributorId,
        int order,
        string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (contributorId.IsEmpty)
        {
            throw new ArgumentException("An agent chat context contributor id is required.", nameof(contributorId));
        }

        if (content.Length > MaximumContentLength)
        {
            throw new ArgumentException(
                $"An agent chat context fragment cannot exceed {MaximumContentLength} characters.",
                nameof(content));
        }

        ContributorId = contributorId;
        Order = order;
        Content = content.Trim();
    }

    public AgentChatContextContributorId ContributorId { get; }

    public int Order { get; }

    public string Content { get; }
}

public enum AgentChatContextScopeAccessMode
{
    Unrestricted,
    AllowListed
}

public enum AgentChatContextAccessState
{
    Ready,
    Loading,
    Failed
}

public sealed record AgentChatContextScope
{
    private readonly IReadOnlyDictionary<Guid, AgentChatContextAgentAccess> accessByAgentId;

    public AgentChatContextScope(
        AgentChatContextScopeId id,
        AgentChatContextSource source,
        string displayName,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IReadOnlyList<AgentChatContextAgentAccess>? agentAccess = null,
        AgentChatContextScopeAccessMode accessMode = AgentChatContextScopeAccessMode.AllowListed,
        AgentChatContextAccessState accessState = AgentChatContextAccessState.Ready)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (id.IsEmpty)
        {
            throw new ArgumentException("An agent chat context scope id is required.", nameof(id));
        }

        if (source.Kind.IsEmpty)
        {
            throw new ArgumentException("An agent chat context source kind is required.", nameof(source));
        }

        if (source.Id.IsEmpty)
        {
            throw new ArgumentException("An agent chat context source id is required.", nameof(source));
        }

        if (displayName.Trim().Length > AgentChatContextLimits.MaximumDisplayNameLength)
        {
            throw new ArgumentException(
                $"An agent chat context display name cannot exceed {AgentChatContextLimits.MaximumDisplayNameLength} characters.",
                nameof(displayName));
        }

        if (agentAccess?.Count > AgentChatContextLimits.MaximumAgentAccessEntries)
        {
            throw new ArgumentException(
                $"An agent chat context scope cannot contain more than {AgentChatContextLimits.MaximumAgentAccessEntries} agent access entries.",
                nameof(agentAccess));
        }

        if (!Enum.IsDefined(accessMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(accessMode),
                accessMode,
                "The agent chat context access mode is invalid.");
        }

        if (!Enum.IsDefined(accessState))
        {
            throw new ArgumentOutOfRangeException(
                nameof(accessState),
                accessState,
                "The agent chat context access state is invalid.");
        }

        var normalizedAgentAccess = agentAccess?.ToArray() ?? [];
        var accessLookup = new Dictionary<Guid, AgentChatContextAgentAccess>(normalizedAgentAccess.Length);
        foreach (var access in normalizedAgentAccess)
        {
            if (!accessLookup.TryAdd(access.AgentId, access))
            {
                throw new ArgumentException(
                    $"Agent chat context access contains duplicate agent id '{access.AgentId:N}'.",
                    nameof(agentAccess));
            }
        }

        Id = id;
        Source = source;
        DisplayName = displayName.Trim();
        WorkspaceScope = workspaceScope;
        AgentAccess = normalizedAgentAccess;
        AccessMode = accessMode;
        AccessState = accessState;
        accessByAgentId = accessLookup;
    }

    public AgentChatContextScopeId Id { get; }

    public AgentChatContextSource Source { get; }

    public string DisplayName { get; }

    public WorkspaceScopeDescriptor? WorkspaceScope { get; }

    public IReadOnlyList<AgentChatContextAgentAccess> AgentAccess { get; }

    public AgentChatContextScopeAccessMode AccessMode { get; }

    public AgentChatContextAccessState AccessState { get; }

    public AgentChatContextAgentAccess? FindAccess(Guid agentId)
        => accessByAgentId.TryGetValue(agentId, out var access) ? access : null;
}

public sealed record AgentChatContextSnapshot
{
    public AgentChatContextSnapshot(
        AgentChatContextScope Scope,
        IReadOnlyList<AgentChatContextFragment> Fragments,
        long Version,
        DateTimeOffset CapturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(Scope);
        ArgumentNullException.ThrowIfNull(Fragments);
        if (Version < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Version), Version, "A context snapshot version cannot be negative.");
        }

        this.Scope = Scope;
        this.Fragments = Fragments.ToArray();
        this.Version = Version;
        this.CapturedAtUtc = CapturedAtUtc;
    }

    public AgentChatContextScope Scope { get; }

    public IReadOnlyList<AgentChatContextFragment> Fragments { get; }

    public long Version { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public AgentChatContextAgentAccess? FindAccess(Guid agentId)
        => Scope.FindAccess(agentId);

    public bool CanRead(Guid agentId)
        => Scope.AccessState == AgentChatContextAccessState.Ready &&
           (Scope.AccessMode == AgentChatContextScopeAccessMode.Unrestricted ||
            FindAccess(agentId)?.CanRead == true);
}

public sealed record AgentChatExecutionCompleted
{
    public AgentChatExecutionCompleted(
        AgentChatContextScopeId scopeId,
        AgentChatContextSource source,
        Guid agentId,
        Guid chatSessionId,
        Guid executionRunId,
        DateTimeOffset completedAtUtc)
    {
        if (scopeId.IsEmpty)
        {
            throw new ArgumentException("An agent chat context scope id is required.", nameof(scopeId));
        }

        ArgumentNullException.ThrowIfNull(source);
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        if (chatSessionId == Guid.Empty)
        {
            throw new ArgumentException("A chat session id is required.", nameof(chatSessionId));
        }

        if (executionRunId == Guid.Empty)
        {
            throw new ArgumentException("An execution run id is required.", nameof(executionRunId));
        }

        ScopeId = scopeId;
        Source = source;
        AgentId = agentId;
        ChatSessionId = chatSessionId;
        ExecutionRunId = executionRunId;
        CompletedAtUtc = completedAtUtc;
    }

    public AgentChatContextScopeId ScopeId { get; }

    public AgentChatContextSource Source { get; }

    public Guid AgentId { get; }

    public Guid ChatSessionId { get; }

    public Guid ExecutionRunId { get; }

    public DateTimeOffset CompletedAtUtc { get; }
}

public enum AgentChatCatalogTab
{
    Agents,
    ActiveChats
}

public enum ActiveAgentChatVisibility
{
    Visible,
    Hidden
}

public enum ActiveAgentChatRunState
{
    Idle,
    Running,
    AwaitingApproval
}

public sealed record AgentChatIdentity
{
    public AgentChatIdentity(
        Guid agentId,
        string name,
        string? roleTitle,
        string? avatarImageUrl)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        AgentId = agentId;
        Name = name.Trim();
        RoleTitle = roleTitle?.Trim() ?? string.Empty;
        AvatarImageUrl = avatarImageUrl?.Trim() ?? string.Empty;
    }

    public Guid AgentId { get; }

    public string Name { get; }

    public string RoleTitle { get; }

    public string AvatarImageUrl { get; }
}

public sealed record ActiveAgentChat(
    AgentChatHandleId HandleId,
    AgentChatIdentity Agent,
    Guid? ChatSessionId,
    ActiveAgentChatVisibility Visibility,
    ActiveAgentChatRunState RunState,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityUtc,
    DateTimeOffset? HiddenAtUtc)
{
    public bool IsVisible => Visibility == ActiveAgentChatVisibility.Visible;

    public bool CanStop => RunState != ActiveAgentChatRunState.Running;
}

public sealed record FloatingAgentChatSettings(
    int HiddenActiveChatRetentionMinutes = 10,
    int MaximumActiveChats = 12,
    int MaximumPreparedAgents = 0,
    bool AdaptivePreparationEnabled = true,
    int PreparedResourceIdleRetentionMinutes = 10)
{
    public static FloatingAgentChatSettings Default { get; } = new();

    [JsonIgnore]
    public TimeSpan HiddenActiveChatRetention
        => TimeSpan.FromMinutes(HiddenActiveChatRetentionMinutes);

    [JsonIgnore]
    public TimeSpan PreparedResourceIdleRetention
        => TimeSpan.FromMinutes(PreparedResourceIdleRetentionMinutes);
}

public static class FloatingAgentChatSettingsValidator
{
    public const int MaximumRetentionMinutes = 24 * 60;
    public const int MaximumActiveChatLimit = 50;
    public const int MaximumPreparedAgentLimit = 20;

    public static FloatingAgentChatSettings Normalize(FloatingAgentChatSettings? settings)
    {
        settings ??= FloatingAgentChatSettings.Default;
        Validate(settings);
        return settings;
    }

    public static void Validate(FloatingAgentChatSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateRange(
            settings.HiddenActiveChatRetentionMinutes,
            1,
            MaximumRetentionMinutes,
            nameof(settings.HiddenActiveChatRetentionMinutes));
        ValidateRange(
            settings.MaximumActiveChats,
            1,
            MaximumActiveChatLimit,
            nameof(settings.MaximumActiveChats));
        ValidateRange(
            settings.MaximumPreparedAgents,
            0,
            MaximumPreparedAgentLimit,
            nameof(settings.MaximumPreparedAgents));
        ValidateRange(
            settings.PreparedResourceIdleRetentionMinutes,
            1,
            MaximumRetentionMinutes,
            nameof(settings.PreparedResourceIdleRetentionMinutes));
    }

    private static void ValidateRange(int value, int minimum, int maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The value must be between {minimum} and {maximum}.");
        }
    }
}
