using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public sealed class AgentHandoffSettings
{
    public bool Enabled { get; set; }

    public Guid? EntryAgentId { get; set; }

    public bool ReturnToPrevious { get; set; }

    public int MaxHandoffDepth { get; set; } = AgentHandoffMetadata.DefaultMaxHandoffDepth;

    public string HandoffInstructions { get; set; } = string.Empty;

    public bool EmitAgentResponseEvents { get; set; }

    public bool EmitAgentResponseUpdateEvents { get; set; } = true;

    public List<AgentHandoffRouteSettings> Routes { get; set; } = [];
}

public sealed class AgentHandoffRouteSettings
{
    public Guid SourceAgentId { get; set; }

    public Guid TargetAgentId { get; set; }

    public bool Enabled { get; set; } = true;

    public string Reason { get; set; } = string.Empty;
}

public sealed record AgentHandoffValidationResult(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool Succeeded => Errors.Count == 0;
}

public static class AgentHandoffMetadata
{
    public const string RootPropertyName = "handoff";
    public const int DefaultMaxHandoffDepth = 8;
    public const int MinimumMaxHandoffDepth = 1;
    public const int MaximumMaxHandoffDepth = 32;

    private const string EnabledPropertyName = "enabled";
    private const string EntryAgentIdPropertyName = "entryAgentId";
    private const string ReturnToPreviousPropertyName = "returnToPrevious";
    private const string MaxHandoffDepthPropertyName = "maxHandoffDepth";
    private const string HandoffInstructionsPropertyName = "handoffInstructions";
    private const string EmitAgentResponseEventsPropertyName = "emitAgentResponseEvents";
    private const string EmitAgentResponseUpdateEventsPropertyName = "emitAgentResponseUpdateEvents";
    private const string RoutesPropertyName = "routes";
    private const string SourceAgentIdPropertyName = "sourceAgentId";
    private const string TargetAgentIdPropertyName = "targetAgentId";
    private const string ReasonPropertyName = "reason";

    public static AgentHandoffSettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return Normalize(new AgentHandoffSettings());
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var handoff = root?[RootPropertyName]?.AsObject();
            if (handoff is null)
            {
                return Normalize(new AgentHandoffSettings());
            }

            var settings = new AgentHandoffSettings
            {
                Enabled = TryReadBoolean(handoff, EnabledPropertyName),
                EntryAgentId = TryReadGuid(handoff, EntryAgentIdPropertyName),
                ReturnToPrevious = TryReadBoolean(handoff, ReturnToPreviousPropertyName),
                MaxHandoffDepth = TryReadInt32(handoff, MaxHandoffDepthPropertyName, DefaultMaxHandoffDepth),
                HandoffInstructions = TryReadString(handoff, HandoffInstructionsPropertyName),
                EmitAgentResponseEvents = TryReadBoolean(handoff, EmitAgentResponseEventsPropertyName),
                EmitAgentResponseUpdateEvents = TryReadBoolean(handoff, EmitAgentResponseUpdateEventsPropertyName, defaultValue: true)
            };

            if (handoff[RoutesPropertyName] is JsonArray routes)
            {
                foreach (var routeNode in routes.OfType<JsonObject>())
                {
                    settings.Routes.Add(ReadRoute(routeNode));
                }
            }

            return Normalize(settings);
        }
        catch (JsonException)
        {
            return Normalize(new AgentHandoffSettings());
        }
    }

    public static string Write(
        string? configurationJson,
        AgentHandoffSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentHandoffSettings());
        var root = ParseObject(configurationJson);
        if (IsDefault(normalized))
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        root[RootPropertyName] = new JsonObject
        {
            [EnabledPropertyName] = normalized.Enabled,
            [EntryAgentIdPropertyName] = normalized.EntryAgentId?.ToString("D") ?? string.Empty,
            [ReturnToPreviousPropertyName] = normalized.ReturnToPrevious,
            [MaxHandoffDepthPropertyName] = normalized.MaxHandoffDepth,
            [HandoffInstructionsPropertyName] = normalized.HandoffInstructions,
            [EmitAgentResponseEventsPropertyName] = normalized.EmitAgentResponseEvents,
            [EmitAgentResponseUpdateEventsPropertyName] = normalized.EmitAgentResponseUpdateEvents,
            [RoutesPropertyName] = new JsonArray(
                normalized.Routes
                    .Select(WriteRoute)
                    .ToArray<JsonNode?>())
        };

        return root.ToJsonString();
    }

    public static AgentHandoffSettings Normalize(AgentHandoffSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var routes = settings.Routes
            .Where(route => route is not null)
            .Select(NormalizeRoute)
            .GroupBy(route => new { route.SourceAgentId, route.TargetAgentId })
            .Select(group => group.First())
            .OrderBy(route => route.SourceAgentId)
            .ThenBy(route => route.TargetAgentId)
            .ToList();

        return new AgentHandoffSettings
        {
            Enabled = settings.Enabled,
            EntryAgentId = settings.EntryAgentId == Guid.Empty ? null : settings.EntryAgentId,
            ReturnToPrevious = settings.ReturnToPrevious,
            MaxHandoffDepth = settings.MaxHandoffDepth,
            HandoffInstructions = NormalizeText(settings.HandoffInstructions),
            EmitAgentResponseEvents = settings.EmitAgentResponseEvents,
            EmitAgentResponseUpdateEvents = settings.EmitAgentResponseUpdateEvents,
            Routes = routes
        };
    }

    public static AgentHandoffValidationResult Validate(AgentHandoffSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();
        var warnings = new List<string>();
        var routeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enabledRouteCount = 0;
        foreach (var route in settings.Routes.Where(route => route is not null).Select(NormalizeRoute))
        {
            if (!route.Enabled)
            {
                continue;
            }

            enabledRouteCount++;
            if (route.SourceAgentId == Guid.Empty)
            {
                errors.Add("Handoff route sourceAgentId is required.");
            }

            if (route.TargetAgentId == Guid.Empty)
            {
                errors.Add("Handoff route targetAgentId is required.");
            }

            if (route.SourceAgentId != Guid.Empty &&
                route.SourceAgentId == route.TargetAgentId)
            {
                errors.Add($"Handoff route '{route.SourceAgentId:D}' cannot target the same agent.");
            }

            var routeKey = $"{route.SourceAgentId:D}->{route.TargetAgentId:D}";
            if (!routeKeys.Add(routeKey))
            {
                errors.Add($"Handoff route '{routeKey}' is duplicated.");
            }
        }

        if (!settings.Enabled)
        {
            return new AgentHandoffValidationResult(errors, warnings);
        }

        if (enabledRouteCount == 0)
        {
            errors.Add("Handoff is enabled but no enabled routes are configured.");
        }

        if (settings.MaxHandoffDepth is < MinimumMaxHandoffDepth or > MaximumMaxHandoffDepth)
        {
            errors.Add($"Handoff maxHandoffDepth must be between {MinimumMaxHandoffDepth} and {MaximumMaxHandoffDepth}.");
        }

        if (settings.ReturnToPrevious && enabledRouteCount == 0)
        {
            warnings.Add("Handoff returnToPrevious is enabled without routes; the workflow will stay on the entry agent.");
        }

        return new AgentHandoffValidationResult(errors, warnings);
    }

    public static AgentHandoffValidationResult Validate(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return Validate(new AgentHandoffSettings());
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return document.RootElement.TryGetProperty(RootPropertyName, out _)
                ? Validate(Read(configurationJson))
                : Validate(new AgentHandoffSettings());
        }
        catch (JsonException exception)
        {
            return new AgentHandoffValidationResult(
                [$"Agent configuration JSON is invalid: {exception.Message}"],
                []);
        }
    }

    public static IReadOnlySet<Guid> ResolveParticipantAgentIds(
        AgentHandoffSettings settings,
        Guid defaultEntryAgentId)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var agentIds = new HashSet<Guid>
        {
            settings.EntryAgentId.GetValueOrDefault(defaultEntryAgentId)
        };

        foreach (var route in settings.Routes.Where(route => route.Enabled))
        {
            if (route.SourceAgentId != Guid.Empty)
            {
                agentIds.Add(route.SourceAgentId);
            }

            if (route.TargetAgentId != Guid.Empty)
            {
                agentIds.Add(route.TargetAgentId);
            }
        }

        return agentIds;
    }

    private static AgentHandoffRouteSettings ReadRoute(JsonObject route)
    {
        return new AgentHandoffRouteSettings
        {
            SourceAgentId = TryReadGuid(route, SourceAgentIdPropertyName) ?? Guid.Empty,
            TargetAgentId = TryReadGuid(route, TargetAgentIdPropertyName) ?? Guid.Empty,
            Enabled = TryReadBoolean(route, EnabledPropertyName, defaultValue: true),
            Reason = TryReadString(route, ReasonPropertyName)
        };
    }

    private static AgentHandoffRouteSettings NormalizeRoute(AgentHandoffRouteSettings route)
    {
        return new AgentHandoffRouteSettings
        {
            SourceAgentId = route.SourceAgentId,
            TargetAgentId = route.TargetAgentId,
            Enabled = route.Enabled,
            Reason = NormalizeText(route.Reason)
        };
    }

    private static JsonObject WriteRoute(AgentHandoffRouteSettings route)
    {
        return new JsonObject
        {
            [SourceAgentIdPropertyName] = route.SourceAgentId.ToString("D"),
            [TargetAgentIdPropertyName] = route.TargetAgentId.ToString("D"),
            [EnabledPropertyName] = route.Enabled,
            [ReasonPropertyName] = route.Reason
        };
    }

    private static bool IsDefault(AgentHandoffSettings settings)
    {
        return !settings.Enabled &&
               settings.EntryAgentId is null &&
               !settings.ReturnToPrevious &&
               settings.MaxHandoffDepth == DefaultMaxHandoffDepth &&
               string.IsNullOrWhiteSpace(settings.HandoffInstructions) &&
               !settings.EmitAgentResponseEvents &&
               settings.EmitAgentResponseUpdateEvents &&
               settings.Routes.Count == 0;
    }

    private static JsonObject ParseObject(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(configurationJson)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static bool TryReadBoolean(JsonObject node, string propertyName, bool defaultValue = false)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<bool>(out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static string TryReadString(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<string>(out var parsedValue)
            ? parsedValue
            : string.Empty;
    }

    private static int TryReadInt32(JsonObject node, string propertyName, int defaultValue)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<int>(out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static Guid? TryReadGuid(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<string>(out var parsedValue) &&
               Guid.TryParse(parsedValue, out var guid)
            ? guid
            : null;
    }

    private static string NormalizeText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
