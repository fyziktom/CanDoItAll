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
            ["send"] = (ProjectObjectType.WorkItem, "send"),
            ["folder"] = (ProjectObjectType.Repository, "folder"),
            ["foldernode"] = (ProjectObjectType.Repository, "folder"),
            ["localfolder"] = (ProjectObjectType.Repository, "folder"),
            ["directory"] = (ProjectObjectType.Repository, "folder"),
            ["repositoryfolder"] = (ProjectObjectType.Repository, "folder"),
            ["githubrepository"] = (ProjectObjectType.Repository, "remote"),
            ["githubrepo"] = (ProjectObjectType.Repository, "remote"),
            ["gitlabrepository"] = (ProjectObjectType.Repository, "remote"),
            ["gitlabrepo"] = (ProjectObjectType.Repository, "remote"),
            ["repositorylink"] = (ProjectObjectType.Repository, "remote"),
            ["githublink"] = (ProjectObjectType.Link, null),
            ["gitlablink"] = (ProjectObjectType.Link, null),
            ["weblink"] = (ProjectObjectType.Link, null),
            ["url"] = (ProjectObjectType.Link, null),
            ["powershellscript"] = (ProjectObjectType.Script, "powershell"),
            ["powershellruntime"] = (ProjectObjectType.Script, "powershell"),
            ["runtimescript"] = (ProjectObjectType.Script, "powershell"),
            ["scriptnode"] = (ProjectObjectType.Script, "console"),
            ["pythonruntime"] = (ProjectObjectType.Environment, "python"),
            ["pythonenvironment"] = (ProjectObjectType.Environment, "python"),
            ["dotnetruntime"] = (ProjectObjectType.Environment, "dotnet-runtime"),
            ["dotnetwatch"] = (ProjectObjectType.Environment, "dotnet-watch"),
            ["dotnetrelease"] = (ProjectObjectType.Environment, "dotnet-release"),
            ["dockerruntime"] = (ProjectObjectType.Infrastructure, "docker-mode"),
            ["dockercompose"] = (ProjectObjectType.Infrastructure, "docker-mode"),
            ["dockernode"] = (ProjectObjectType.Infrastructure, "docker-mode"),
            ["deploymentfolder"] = (ProjectObjectType.Infrastructure, "deployment-folder"),
            ["pdffile"] = (ProjectObjectType.File, "pdf"),
            ["excelfile"] = (ProjectObjectType.File, "excel"),
            ["spreadsheet"] = (ProjectObjectType.File, "excel"),
            ["docxfile"] = (ProjectObjectType.File, "docx"),
            ["worddocument"] = (ProjectObjectType.File, "docx"),
            ["markdownfile"] = (ProjectObjectType.File, "markdown"),
            ["mermaiddiagram"] = (ProjectObjectType.File, "mermaid"),
            ["logfile"] = (ProjectObjectType.File, "log"),
            ["archivefile"] = (ProjectObjectType.File, "archive"),
            ["audiofile"] = (ProjectObjectType.File, "audio"),
            ["localfile"] = (ProjectObjectType.File, null),
            ["filelink"] = (ProjectObjectType.File, null)
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
            },
            [ProjectObjectType.Repository] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["remote"] = "remote",
                ["remoterepository"] = "remote",
                ["github"] = "remote",
                ["githubrepo"] = "remote",
                ["githubrepository"] = "remote",
                ["gitlab"] = "remote",
                ["gitlabrepo"] = "remote",
                ["gitlabrepository"] = "remote",
                ["local"] = "local",
                ["localrepository"] = "local",
                ["folder"] = "folder",
                ["localfolder"] = "folder",
                ["foldernode"] = "folder",
                ["directory"] = "folder"
            },
            [ProjectObjectType.Script] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["powershell"] = "powershell",
                ["powershellscript"] = "powershell",
                ["powershellruntime"] = "powershell",
                ["ps1"] = "powershell",
                ["console"] = "console",
                ["command"] = "console",
                ["efmigration"] = "ef-migration",
                ["ef"] = "ef-migration",
                ["tailwind"] = "tailwind-watch",
                ["tailwindwatch"] = "tailwind-watch"
            },
            [ProjectObjectType.Environment] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["python"] = "python",
                ["pythonruntime"] = "python",
                ["pythonenvironment"] = "python",
                ["dotnetruntime"] = "dotnet-runtime",
                ["dotnet"] = "dotnet-runtime",
                ["dotnetwatch"] = "dotnet-watch",
                ["watch"] = "dotnet-watch",
                ["dotnetrelease"] = "dotnet-release",
                ["release"] = "dotnet-release"
            },
            [ProjectObjectType.Infrastructure] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["dockermode"] = "docker-mode",
                ["docker"] = "docker-mode",
                ["dockerruntime"] = "docker-mode",
                ["dockercompose"] = "docker-mode",
                ["container"] = "docker-mode",
                ["deploymentfolder"] = "deployment-folder",
                ["folder"] = "deployment-folder",
                ["remote"] = "remote-server",
                ["server"] = "remote-server",
                ["domain"] = "domain",
                ["dns"] = "dns-record",
                ["dnsrecord"] = "dns-record",
                ["database"] = "database",
                ["storage"] = "storage-system",
                ["key"] = "key-reference",
                ["keyreference"] = "key-reference",
                ["ai"] = "ai-link",
                ["ailink"] = "ai-link"
            },
            [ProjectObjectType.File] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pdf"] = "pdf",
                ["pdffile"] = "pdf",
                ["excel"] = "excel",
                ["spreadsheet"] = "excel",
                ["xlsx"] = "excel",
                ["docx"] = "docx",
                ["word"] = "docx",
                ["worddocument"] = "docx",
                ["text"] = "text",
                ["txt"] = "text",
                ["json"] = "json",
                ["markdown"] = "markdown",
                ["md"] = "markdown",
                ["mermaid"] = "mermaid",
                ["diagram"] = "mermaid",
                ["screenshot"] = "screenshot",
                ["log"] = "log",
                ["archive"] = "archive",
                ["zip"] = "archive",
                ["audio"] = "audio"
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
