using CanDoItAll.SharedKernel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureRequestedNodeKindParser
{
    private static readonly IReadOnlyDictionary<string, ProjectObjectType> EnumTypeAliases = Enum
        .GetValues<ProjectObjectType>()
        .ToDictionary(value => Canonicalize(value.ToString()), value => value);

    private static readonly IReadOnlyDictionary<string, (ProjectObjectType ObjectType, string? ObjectSubtype)> SyntheticKindAliases =
        new Dictionary<string, (ProjectObjectType ObjectType, string? ObjectSubtype)>(StringComparer.OrdinalIgnoreCase)
        {
            ["featureblock"] = (ProjectObjectType.ProjectBlock, "feature"),
            ["architectureblock"] = (ProjectObjectType.ProjectBlock, "architecture"),
            ["implementationblock"] = (ProjectObjectType.ProjectBlock, "implementation"),
            ["revisionblock"] = (ProjectObjectType.ProjectBlock, "revision"),
            ["testingblock"] = (ProjectObjectType.ProjectBlock, "testing"),
            ["promptingblock"] = (ProjectObjectType.ProjectBlock, "prompting"),
            ["researchblock"] = (ProjectObjectType.ProjectBlock, "research"),
            ["financialblock"] = (ProjectObjectType.ProjectBlock, "financial"),
            ["marketingblock"] = (ProjectObjectType.ProjectBlock, "marketing"),
            ["operationsblock"] = (ProjectObjectType.ProjectBlock, "operations"),
            ["deliveryblock"] = (ProjectObjectType.ProjectBlock, "delivery"),
            ["riskblock"] = (ProjectObjectType.ProjectBlock, "risk"),
            ["complianceblock"] = (ProjectObjectType.ProjectBlock, "compliance"),
            ["supportblock"] = (ProjectObjectType.ProjectBlock, "support"),
            ["deploymentblock"] = (ProjectObjectType.ProjectBlock, "deployment"),
            ["reposblock"] = (ProjectObjectType.ProjectBlock, "repos"),
            ["dockersblock"] = (ProjectObjectType.ProjectBlock, "dockers"),
            ["taskflowblock"] = (ProjectObjectType.ProjectBlock, "task-flow"),
            ["backlogblock"] = (ProjectObjectType.ProjectBlock, "backlog"),
            ["serverblock"] = (ProjectObjectType.ProjectBlock, "server"),
            ["computerblock"] = (ProjectObjectType.ProjectBlock, "computer"),
            ["routerblock"] = (ProjectObjectType.ProjectBlock, "router"),
            ["wifiblock"] = (ProjectObjectType.ProjectBlock, "wifi"),
            ["task"] = (ProjectObjectType.WorkItem, "task"),
            ["issue"] = (ProjectObjectType.WorkItem, "issue"),
            ["revision"] = (ProjectObjectType.WorkItem, "revision"),
            ["feedback"] = (ProjectObjectType.WorkItem, "feedback"),
            ["payment"] = (ProjectObjectType.WorkItem, "payment"),
            ["send"] = (ProjectObjectType.WorkItem, "send")
        };

    private static readonly IReadOnlyDictionary<ProjectObjectType, IReadOnlyDictionary<string, string>> SubtypeAliases =
        new Dictionary<ProjectObjectType, IReadOnlyDictionary<string, string>>
        {
            [ProjectObjectType.ProjectBlock] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["feature"] = "feature",
                ["featureblock"] = "feature",
                ["architecture"] = "architecture",
                ["architectureblock"] = "architecture",
                ["implementation"] = "implementation",
                ["implementationblock"] = "implementation",
                ["revision"] = "revision",
                ["revisionblock"] = "revision",
                ["testing"] = "testing",
                ["testingblock"] = "testing",
                ["prompting"] = "prompting",
                ["promptingblock"] = "prompting",
                ["research"] = "research",
                ["researchblock"] = "research",
                ["financial"] = "financial",
                ["financialblock"] = "financial",
                ["marketing"] = "marketing",
                ["marketingblock"] = "marketing",
                ["operations"] = "operations",
                ["operationsblock"] = "operations",
                ["delivery"] = "delivery",
                ["deliveryblock"] = "delivery",
                ["risk"] = "risk",
                ["riskblock"] = "risk",
                ["compliance"] = "compliance",
                ["complianceblock"] = "compliance",
                ["support"] = "support",
                ["supportblock"] = "support",
                ["deployment"] = "deployment",
                ["deploymentblock"] = "deployment",
                ["repos"] = "repos",
                ["repositories"] = "repos",
                ["reposblock"] = "repos",
                ["dockers"] = "dockers",
                ["docker"] = "dockers",
                ["dockersblock"] = "dockers",
                ["taskflow"] = "task-flow",
                ["taskflowblock"] = "task-flow",
                ["backlog"] = "backlog",
                ["backlogblock"] = "backlog",
                ["server"] = "server",
                ["serverblock"] = "server",
                ["computer"] = "computer",
                ["computerblock"] = "computer",
                ["router"] = "router",
                ["routerblock"] = "router",
                ["wifi"] = "wifi",
                ["wifiblock"] = "wifi"
            },
            [ProjectObjectType.WorkItem] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["task"] = "task",
                ["issue"] = "issue",
                ["revision"] = "revision",
                ["feedback"] = "feedback",
                ["payment"] = "payment",
                ["send"] = "send"
            }
        };

    public static (ProjectObjectType ObjectType, string? ObjectSubtype) ResolveRequired(string? rawObjectType, string? rawObjectSubtype)
    {
        if (!TryResolve(rawObjectType, rawObjectSubtype, out var objectType, out var objectSubtype))
        {
            throw new JsonException($"Unsupported objectType '{rawObjectType}'. Use a canonical type like 'ProjectBlock' with a lowercase subtype such as 'feature', or a typed alias like 'FeatureBlock'.");
        }

        return (objectType!.Value, objectSubtype);
    }

    public static (ProjectObjectType ObjectType, string? ObjectSubtype) ResolveRequired(JsonElement? rawObjectType, string? rawObjectSubtype)
    {
        return ResolveRequired(ResolveObjectTypeText(rawObjectType), rawObjectSubtype);
    }

    public static (ProjectObjectType? ObjectType, string? ObjectSubtype) ResolveOptional(string? rawObjectType, string? rawObjectSubtype)
    {
        if (string.IsNullOrWhiteSpace(rawObjectType))
        {
            return (null, rawObjectSubtype?.Trim());
        }

        if (!TryResolve(rawObjectType, rawObjectSubtype, out var objectType, out var objectSubtype))
        {
            throw new JsonException($"Unsupported objectType '{rawObjectType}'. Use a canonical type like 'ProjectBlock' with a lowercase subtype such as 'feature', or a typed alias like 'FeatureBlock'.");
        }

        return (objectType, objectSubtype);
    }

    public static (ProjectObjectType? ObjectType, string? ObjectSubtype) ResolveOptional(JsonElement? rawObjectType, string? rawObjectSubtype)
    {
        return ResolveOptional(ResolveObjectTypeText(rawObjectType), rawObjectSubtype);
    }

    public static string? NormalizeSubtypeForType(ProjectObjectType objectType, string? rawObjectSubtype)
    {
        return NormalizeSubtype(objectType, rawObjectSubtype);
    }

    private static string? ResolveObjectTypeText(JsonElement? rawObjectType)
    {
        if (!rawObjectType.HasValue ||
            rawObjectType.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var value = rawObjectType.Value;
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt32(out var numericValue) ||
                !Enum.IsDefined(typeof(ProjectObjectType), numericValue))
            {
                throw new JsonException($"Unsupported objectType numeric value '{value.GetRawText()}'. Use a defined ProjectObjectType value or canonical type name.");
            }

            return ((ProjectObjectType)numericValue).ToString();
        }

        throw new JsonException("objectType must be a string enum name, typed alias, or numeric ProjectObjectType value.");
    }

    private static bool TryResolve(
        string? rawObjectType,
        string? rawObjectSubtype,
        out ProjectObjectType? objectType,
        out string? objectSubtype)
    {
        objectType = null;
        objectSubtype = NormalizeSubtype(null, rawObjectSubtype);
        if (string.IsNullOrWhiteSpace(rawObjectType))
        {
            return false;
        }

        var normalizedType = Canonicalize(rawObjectType);
        if (SyntheticKindAliases.TryGetValue(normalizedType, out var synthetic))
        {
            objectType = synthetic.ObjectType;
            objectSubtype = NormalizeSubtype(synthetic.ObjectType, rawObjectSubtype) ?? synthetic.ObjectSubtype;
            return true;
        }

        if (!EnumTypeAliases.TryGetValue(normalizedType, out var resolvedType))
        {
            return false;
        }

        objectType = resolvedType;
        objectSubtype = NormalizeSubtype(resolvedType, rawObjectSubtype);
        return true;
    }

    private static string? NormalizeSubtype(ProjectObjectType? objectType, string? rawObjectSubtype)
    {
        if (string.IsNullOrWhiteSpace(rawObjectSubtype))
        {
            return null;
        }

        var trimmed = rawObjectSubtype.Trim();
        if (!objectType.HasValue)
        {
            return trimmed;
        }

        var normalizedSubtype = Canonicalize(trimmed);
        if (SubtypeAliases.TryGetValue(objectType.Value, out var aliasMap) &&
            aliasMap.TryGetValue(normalizedSubtype, out var canonicalSubtype))
        {
            return canonicalSubtype;
        }

        return trimmed;
    }

    private static string Canonicalize(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}

internal sealed class ProjectStructureNodeCreateInputJsonConverter : JsonConverter<ProjectStructureNodeCreateInput>
{
    public override ProjectStructureNodeCreateInput Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var model = JsonSerializer.Deserialize<ProjectStructureNodeCreateJsonModel>(ref reader, options)
            ?? throw new JsonException("Project-structure node create payload was empty.");
        var resolved = ProjectStructureRequestedNodeKindParser.ResolveRequired(model.ObjectType, model.ObjectSubtype);

        return new ProjectStructureNodeCreateInput(
            resolved.ObjectType,
            model.Title ?? string.Empty,
            model.Subtitle ?? string.Empty,
            model.Notes ?? string.Empty,
            model.ParentNodeKey,
            model.X,
            model.Y,
            model.StartUtc,
            model.EndUtc,
            resolved.ObjectSubtype,
            model.Media,
            model.MetadataJson,
            model.LeaseToken,
            model.DurationSeconds);
    }

    public override void Write(Utf8JsonWriter writer, ProjectStructureNodeCreateInput value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(
            writer,
            new ProjectStructureNodeCreateJsonModel
            {
                ObjectType = JsonSerializer.SerializeToElement(value.ObjectType.ToString(), options),
                Title = value.Title,
                Subtitle = value.Subtitle,
                Notes = value.Notes,
                ParentNodeKey = value.ParentNodeKey,
                X = value.X,
                Y = value.Y,
                StartUtc = value.StartUtc,
                EndUtc = value.EndUtc,
                ObjectSubtype = value.ObjectSubtype,
                Media = value.Media,
                MetadataJson = value.MetadataJson,
                LeaseToken = value.LeaseToken,
                DurationSeconds = value.DurationSeconds
            },
            options);
    }

    private sealed class ProjectStructureNodeCreateJsonModel
    {
        public JsonElement? ObjectType { get; set; }

        public string? Title { get; set; }

        public string? Subtitle { get; set; }

        public string? Notes { get; set; }

        public string? ParentNodeKey { get; set; }

        public double? X { get; set; }

        public double? Y { get; set; }

        public DateTimeOffset? StartUtc { get; set; }

        public DateTimeOffset? EndUtc { get; set; }

        public string? ObjectSubtype { get; set; }

        public ProjectObjectMediaPayload? Media { get; set; }

        public string? MetadataJson { get; set; }

        public string? LeaseToken { get; set; }

        public int? DurationSeconds { get; set; }
    }
}

internal sealed class ProjectStructureNodeEditInputJsonConverter : JsonConverter<ProjectStructureNodeEditInput>
{
    public override ProjectStructureNodeEditInput Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var model = JsonSerializer.Deserialize<ProjectStructureNodeEditJsonModel>(ref reader, options)
            ?? throw new JsonException("Project-structure node update payload was empty.");
        var resolved = ProjectStructureRequestedNodeKindParser.ResolveOptional(model.ObjectType, model.ObjectSubtype);

        return new ProjectStructureNodeEditInput(
            model.Title ?? string.Empty,
            model.Subtitle ?? string.Empty,
            model.Notes ?? string.Empty,
            resolved.ObjectType,
            resolved.ObjectSubtype,
            model.StartUtc,
            model.EndUtc,
            model.MetadataJson,
            model.LeaseToken,
            model.DurationSeconds);
    }

    public override void Write(Utf8JsonWriter writer, ProjectStructureNodeEditInput value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(
            writer,
            new ProjectStructureNodeEditJsonModel
            {
                Title = value.Title,
                Subtitle = value.Subtitle,
                Notes = value.Notes,
                ObjectType = value.ObjectType.HasValue
                    ? JsonSerializer.SerializeToElement(value.ObjectType.Value.ToString(), options)
                    : null,
                ObjectSubtype = value.ObjectSubtype,
                StartUtc = value.StartUtc,
                EndUtc = value.EndUtc,
                MetadataJson = value.MetadataJson,
                LeaseToken = value.LeaseToken,
                DurationSeconds = value.DurationSeconds
            },
            options);
    }

    private sealed class ProjectStructureNodeEditJsonModel
    {
        public string? Title { get; set; }

        public string? Subtitle { get; set; }

        public string? Notes { get; set; }

        public JsonElement? ObjectType { get; set; }

        public string? ObjectSubtype { get; set; }

        public DateTimeOffset? StartUtc { get; set; }

        public DateTimeOffset? EndUtc { get; set; }

        public string? MetadataJson { get; set; }

        public string? LeaseToken { get; set; }

        public int? DurationSeconds { get; set; }
    }
}
