using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentChatPositionLimits
{
    public const int MaximumTokenLength = 100;
    public const int MaximumIdentityLength = 512;
    public const int MaximumLabelLength = 200;
    public const int MaximumRouteLength = 1_024;
    public const int MaximumFactValueLength = 1_000;
    public const int MaximumSelectedEntities = 64;
    public const int MaximumFacts = 32;
}

public readonly record struct AgentChatNavigationIdentity
{
    public AgentChatNavigationIdentity(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An agent chat navigation identity is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public bool IsEmpty => Value == Guid.Empty;

    public static AgentChatNavigationIdentity Create()
        => new(Guid.NewGuid());

    public static AgentChatNavigationIdentity CreateForLocation(
        string baseUri,
        string locationUri)
        => CreateForLocation(baseUri, locationUri, []);

    public static AgentChatNavigationIdentity CreateForLocation(
        string baseUri,
        string locationUri,
        IReadOnlyList<KeyValuePair<string, string?>> queryOverrides)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationUri);
        ArgumentNullException.ThrowIfNull(queryOverrides);
        var baseAddress = new Uri(baseUri, UriKind.Absolute);
        var locationAddress = new Uri(locationUri, UriKind.Absolute);
        if (!baseAddress.IsBaseOf(locationAddress))
        {
            throw new ArgumentException(
                "The agent chat navigation location must be inside the application base URI.",
                nameof(locationUri));
        }

        var locationPath = new Uri(locationAddress.GetLeftPart(UriPartial.Path), UriKind.Absolute);
        var relativePath = baseAddress.MakeRelativeUri(locationPath).ToString();
        var query = locationAddress.Query.TrimStart('?');
        var queryValues = ParseQuery(query);
        foreach (var queryOverride in queryOverrides)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(queryOverride.Key);
            var normalizedKey = queryOverride.Key.Trim().ToLowerInvariant();
            queryValues.Remove(normalizedKey);
            if (queryOverride.Value is not null)
            {
                queryValues.Add(
                    normalizedKey,
                    [queryOverride.Value]);
            }
        }

        var normalizedPath = string.IsNullOrWhiteSpace(relativePath)
            ? "/"
            : relativePath;
        var normalizedQuery = queryValues
            .SelectMany(pair => pair.Value.Select(value =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(value)}"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var normalizedLocation = normalizedQuery.Length == 0
            ? normalizedPath
            : $"{normalizedPath}?{string.Join('&', normalizedQuery)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedLocation));
        return new AgentChatNavigationIdentity(new Guid(hash.AsSpan(0, 16)));
    }

    private static Dictionary<string, List<string>> ParseQuery(string query)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            var rawName = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            var rawValue = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
            var name = DecodeQueryValue(rawName).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!values.TryGetValue(name, out var entries))
            {
                entries = [];
                values.Add(name, entries);
            }

            entries.Add(DecodeQueryValue(rawValue));
        }

        return values;
    }

    private static string DecodeQueryValue(string value)
        => Uri.UnescapeDataString(value.Replace('+', ' '));
}

public sealed record AgentChatContextEntityReference
{
    public AgentChatContextEntityReference(
        string kind,
        string id,
        string displayName)
    {
        Kind = AgentChatPositionText.NormalizeToken(kind, nameof(kind));
        Id = AgentChatPositionText.NormalizeRequired(
            id,
            AgentChatPositionLimits.MaximumIdentityLength,
            nameof(id));
        DisplayName = AgentChatPositionText.NormalizeRequired(
            displayName,
            AgentChatPositionLimits.MaximumLabelLength,
            nameof(displayName));
    }

    public string Kind { get; }

    public string Id { get; }

    public string DisplayName { get; }
}

public sealed record AgentChatContextPositionFact
{
    public AgentChatContextPositionFact(string name, string value)
    {
        Name = AgentChatPositionText.NormalizeToken(name, nameof(name));
        Value = AgentChatPositionText.NormalizeRequired(
            value,
            AgentChatPositionLimits.MaximumFactValueLength,
            nameof(value));
    }

    public string Name { get; }

    public string Value { get; }
}

public sealed record AgentChatWorkspacePosition
{
    public AgentChatWorkspacePosition(
        string tabId,
        string title,
        string route,
        string tabKind,
        Guid? projectId = null,
        string? projectName = null,
        string? phaseName = null)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A workspace-position project id cannot be empty.", nameof(projectId));
        }

        TabId = AgentChatPositionText.NormalizeRequired(
            tabId,
            AgentChatPositionLimits.MaximumIdentityLength,
            nameof(tabId));
        Title = AgentChatPositionText.NormalizeRequired(
            title,
            AgentChatPositionLimits.MaximumLabelLength,
            nameof(title));
        Route = AgentChatPositionText.NormalizeRoute(route, nameof(route));
        TabKind = AgentChatPositionText.NormalizeToken(tabKind, nameof(tabKind));
        ProjectId = projectId;
        ProjectName = AgentChatPositionText.NormalizeOptional(
            projectName,
            AgentChatPositionLimits.MaximumLabelLength,
            nameof(projectName));
        PhaseName = AgentChatPositionText.NormalizeOptional(
            phaseName,
            AgentChatPositionLimits.MaximumLabelLength,
            nameof(phaseName));
    }

    public string TabId { get; }

    public string Title { get; }

    public string Route { get; }

    public string TabKind { get; }

    public Guid? ProjectId { get; }

    public string? ProjectName { get; }

    public string? PhaseName { get; }
}

public sealed record AgentChatSurfacePosition
{
    public AgentChatSurfacePosition(
        string module,
        string surface,
        string view,
        string route,
        AgentChatContextEntityReference? primarySelection = null,
        IReadOnlyList<AgentChatContextEntityReference>? selectedEntities = null,
        IReadOnlyList<AgentChatContextPositionFact>? facts = null)
    {
        var normalizedSelections = selectedEntities?.ToArray() ?? [];
        var normalizedFacts = facts?.ToArray() ?? [];
        if (normalizedSelections.Length > AgentChatPositionLimits.MaximumSelectedEntities)
        {
            throw new ArgumentException(
                $"A surface position cannot contain more than {AgentChatPositionLimits.MaximumSelectedEntities} selected entities.",
                nameof(selectedEntities));
        }

        if (normalizedFacts.Length > AgentChatPositionLimits.MaximumFacts)
        {
            throw new ArgumentException(
                $"A surface position cannot contain more than {AgentChatPositionLimits.MaximumFacts} facts.",
                nameof(facts));
        }

        if (normalizedSelections.Contains(null!))
        {
            throw new ArgumentException("Selected entities cannot contain null entries.", nameof(selectedEntities));
        }

        if (normalizedFacts.Contains(null!))
        {
            throw new ArgumentException("Position facts cannot contain null entries.", nameof(facts));
        }

        Module = AgentChatPositionText.NormalizeToken(module, nameof(module));
        Surface = AgentChatPositionText.NormalizeToken(surface, nameof(surface));
        View = AgentChatPositionText.NormalizeToken(view, nameof(view));
        Route = AgentChatPositionText.NormalizeRoute(route, nameof(route));
        PrimarySelection = primarySelection;
        SelectedEntities = normalizedSelections;
        Facts = normalizedFacts;
    }

    public string Module { get; }

    public string Surface { get; }

    public string View { get; }

    public string Route { get; }

    public AgentChatContextEntityReference? PrimarySelection { get; }

    public IReadOnlyList<AgentChatContextEntityReference> SelectedEntities { get; }

    public IReadOnlyList<AgentChatContextPositionFact> Facts { get; }
}

public sealed record AgentChatContextSurface
{
    public AgentChatContextSurface(
        AgentChatContextSource source,
        string displayName,
        AgentChatSurfacePosition position,
        WorkspaceScopeDescriptor? workspaceScope = null,
        IReadOnlyList<AgentChatContextAgentAccess>? agentAccess = null,
        AgentChatContextScopeAccessMode accessMode = AgentChatContextScopeAccessMode.AllowListed,
        AgentChatContextAccessState accessState = AgentChatContextAccessState.Ready,
        AgentChatContextCompletionRefreshMode completionRefreshMode = AgentChatContextCompletionRefreshMode.None)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(position);
        Source = source;
        DisplayName = AgentChatPositionText.NormalizeRequired(
            displayName,
            AgentChatContextLimits.MaximumDisplayNameLength,
            nameof(displayName));
        if (source.Kind.IsEmpty || source.Id.IsEmpty)
        {
            throw new ArgumentException("A context surface source kind and id are required.", nameof(source));
        }

        if (!Enum.IsDefined(accessMode))
        {
            throw new ArgumentOutOfRangeException(nameof(accessMode), accessMode, "The context surface access mode is undefined.");
        }

        if (!Enum.IsDefined(accessState))
        {
            throw new ArgumentOutOfRangeException(nameof(accessState), accessState, "The context surface access state is undefined.");
        }

        if (!Enum.IsDefined(completionRefreshMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(completionRefreshMode),
                completionRefreshMode,
                "The context surface completion refresh mode is undefined.");
        }

        var normalizedAccess = agentAccess?.ToArray() ?? [];
        if (normalizedAccess.Length > AgentChatContextLimits.MaximumAgentAccessEntries)
        {
            throw new ArgumentException(
                $"A context surface cannot contain more than {AgentChatContextLimits.MaximumAgentAccessEntries} agent access entries.",
                nameof(agentAccess));
        }

        if (normalizedAccess.Length > 1)
        {
            var agentIds = new HashSet<Guid>(normalizedAccess.Length);
            foreach (var access in normalizedAccess)
            {
                if (!agentIds.Add(access.AgentId))
                {
                    throw new ArgumentException("A context surface cannot contain duplicate agent access entries.", nameof(agentAccess));
                }
            }
        }

        Position = position;
        WorkspaceScope = workspaceScope;
        AgentAccess = normalizedAccess;
        AccessMode = accessMode;
        AccessState = accessState;
        CompletionRefreshMode = completionRefreshMode;
    }

    public AgentChatContextSource Source { get; }

    public string DisplayName { get; }

    public AgentChatSurfacePosition Position { get; }

    public WorkspaceScopeDescriptor? WorkspaceScope { get; }

    public IReadOnlyList<AgentChatContextAgentAccess> AgentAccess { get; }

    public AgentChatContextScopeAccessMode AccessMode { get; }

    public AgentChatContextAccessState AccessState { get; }

    public AgentChatContextCompletionRefreshMode CompletionRefreshMode { get; }

    public AgentChatContextScope ToScope(AgentChatContextScopeId scopeId)
        => new(
            scopeId,
            Source,
            DisplayName,
            WorkspaceScope,
            AgentAccess,
            AccessMode,
            AccessState,
            Position,
            CompletionRefreshMode);
}

internal static class AgentChatPositionText
{
    public static string NormalizeToken(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > AgentChatPositionLimits.MaximumTokenLength)
        {
            throw new ArgumentException(
                $"A position token cannot exceed {AgentChatPositionLimits.MaximumTokenLength} characters.",
                parameterName);
        }

        foreach (var character in normalized)
        {
            if (!char.IsLetterOrDigit(character) &&
                character is not '-' and not '_' and not '.')
            {
                throw new ArgumentException(
                    "A position token may contain only letters, digits, hyphens, underscores, and periods.",
                    parameterName);
            }
        }

        return normalized;
    }

    public static string NormalizeRoute(string value, string parameterName)
    {
        var normalized = NormalizeRequired(
            value,
            AgentChatPositionLimits.MaximumRouteLength,
            parameterName);
        if (!normalized.StartsWith('/'))
        {
            throw new ArgumentException("A position route must be application-relative and start with '/'.", parameterName);
        }

        return normalized;
    }

    public static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = Normalize(value, maximumLength, parameterName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("A non-empty sanitized position value is required.", parameterName);
        }

        return normalized;
    }

    public static string? NormalizeOptional(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Normalize(value, maximumLength, parameterName);
    }

    private static string Normalize(
        string value,
        int maximumLength,
        string parameterName)
    {
        var trimmed = value.Trim();
        var requiresNormalization = false;
        var previousWasWhitespace = false;
        foreach (var character in trimmed)
        {
            var isWhitespace = char.IsControl(character) || char.IsWhiteSpace(character);
            if (isWhitespace && (character != ' ' || previousWasWhitespace))
            {
                requiresNormalization = true;
            }

            previousWasWhitespace = isWhitespace;
        }

        if (!requiresNormalization)
        {
            if (trimmed.Length > maximumLength)
            {
                throw new ArgumentException(
                    $"A position value cannot exceed {maximumLength} characters.",
                    parameterName);
            }

            return trimmed;
        }

        var buffer = new char[Math.Min(trimmed.Length, maximumLength)];
        var length = 0;
        previousWasWhitespace = false;
        foreach (var character in trimmed)
        {
            var isWhitespace = char.IsControl(character) || char.IsWhiteSpace(character);
            if (isWhitespace)
            {
                if (!previousWasWhitespace && length > 0)
                {
                    if (length >= maximumLength)
                    {
                        throw new ArgumentException(
                            $"A position value cannot exceed {maximumLength} characters.",
                            parameterName);
                    }

                    buffer[length++] = ' ';
                }

                previousWasWhitespace = true;
                continue;
            }

            if (length >= maximumLength)
            {
                throw new ArgumentException(
                    $"A position value cannot exceed {maximumLength} characters.",
                    parameterName);
            }

            buffer[length++] = character;
            previousWasWhitespace = false;
        }

        return new string(buffer, 0, length).Trim();
    }
}
