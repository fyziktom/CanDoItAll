using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectStructureImportService(ProjectWorkbenchService projectWorkbenchService)
{
    public async Task<ProjectStructureImportResult> ImportAsync(
        ProjectStructureImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ProjectStructureAgentException(400, "ImportTitleRequired", "Import title is required.");
        }

        var containerParentNodeKey = string.IsNullOrWhiteSpace(request.ParentNodeKey)
            ? $"project:{request.ProjectId}"
            : request.ParentNodeKey.Trim();
        var warnings = new List<string>();

        var container = await projectWorkbenchService.CreateObjectAsync(
            request.ProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                request.Title.Trim(),
                $"{request.SourceKind} import",
                $"Imported via project-structure MCP from {request.SourceKind}.",
                containerParentNodeKey,
                null,
                null,
                null,
                null,
                request.ContainerBlockSubtype,
                null,
                null),
            cancellationToken);

        string? sourceNodeId = null;
        if (request.SourceAsset is not null)
        {
            ValidateBase64Payload(request.SourceAsset.Base64Data);
            var sourceAssetNode = await projectWorkbenchService.CreateObjectAsync(
                request.ProjectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.File,
                    request.SourceAsset.FileName,
                    $"{request.SourceKind} source",
                    $"Source asset captured for {request.SourceKind} import.",
                    container.Id,
                    null,
                    null,
                    null,
                    null,
                    ResolveSourceAssetSubtype(request.SourceKind, request.SourceAsset),
                    request.SourceAsset,
                    null),
                cancellationToken);
            sourceNodeId = sourceAssetNode.Id;
        }

        var plan = BuildImportPlan(request, warnings);
        var createdNodeIds = new List<string> { container.Id };
        if (!string.IsNullOrWhiteSpace(sourceNodeId))
        {
            createdNodeIds.Add(sourceNodeId);
        }

        var mappedNodeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rootNode in plan.RootNodes)
        {
            await CreateImportedNodeAsync(
                request,
                rootNode,
                container.Id,
                createdNodeIds,
                mappedNodeIds,
                cancellationToken);
        }

        foreach (var link in plan.Links)
        {
            if (!mappedNodeIds.TryGetValue(link.SourceKey, out var sourceId) ||
                !mappedNodeIds.TryGetValue(link.TargetKey, out var targetId))
            {
                warnings.Add($"Skipped imported link '{link.SourceKey}' -> '{link.TargetKey}' because one endpoint did not map to a created node.");
                continue;
            }

            await projectWorkbenchService.LinkObjectsAsync(
                request.ProjectId,
                sourceId,
                targetId,
                link.Kind,
                cancellationToken);
        }

        return new ProjectStructureImportResult(request.ProjectId, container.Id, sourceNodeId, createdNodeIds, warnings);
    }

    private async Task CreateImportedNodeAsync(
        ProjectStructureImportRequest request,
        ImportedNodeDraft draft,
        string parentNodeId,
        ICollection<string> createdNodeIds,
        IDictionary<string, string> mappedNodeIds,
        CancellationToken cancellationToken)
    {
        var hasChildren = draft.Children.Count > 0;
        var createdNode = await projectWorkbenchService.CreateObjectAsync(
            request.ProjectId,
            new ProjectObjectCreateRequest(
                hasChildren ? ProjectObjectType.ProjectBlock : ProjectObjectType.WorkItem,
                draft.Title,
                string.Empty,
                draft.Notes,
                parentNodeId,
                null,
                null,
                null,
                null,
                hasChildren ? request.ContainerBlockSubtype : request.LeafWorkItemSubtype,
                null,
                null),
            cancellationToken);

        createdNodeIds.Add(createdNode.Id);
        mappedNodeIds[draft.Key] = createdNode.Id;

        foreach (var child in draft.Children)
        {
            await CreateImportedNodeAsync(
                request,
                child,
                createdNode.Id,
                createdNodeIds,
                mappedNodeIds,
                cancellationToken);
        }
    }

    private static ProjectStructureImportPlan BuildImportPlan(ProjectStructureImportRequest request, ICollection<string> warnings)
    {
        return request.SourceKind switch
        {
            ProjectStructureImportSourceKind.Mermaid => ParseMermaid(request, warnings),
            ProjectStructureImportSourceKind.DocxOutline => ParseDocx(request, warnings),
            ProjectStructureImportSourceKind.XmindMap => ParseXmind(request, warnings),
            ProjectStructureImportSourceKind.JsonOutline => ParseJsonOutline(request),
            _ => throw new ProjectStructureAgentException(400, "UnsupportedImportSource", $"Import source '{request.SourceKind}' is not supported.")
        };
    }

    private static ProjectStructureImportPlan ParseMermaid(ProjectStructureImportRequest request, ICollection<string> warnings)
    {
        var sourceText = ResolveRequiredSourceText(request);
        if (sourceText.Contains("mindmap", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Mermaid mindmap import uses indentation depth as hierarchy.");
            return new ProjectStructureImportPlan(ParseMermaidMindmap(sourceText), []);
        }

        if (sourceText.Contains("flowchart", StringComparison.OrdinalIgnoreCase) ||
            sourceText.Contains("graph ", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Mermaid flowchart import maps all extracted nodes under one container and translates edges into depends-on links.");
            return ParseMermaidFlowchart(sourceText, warnings);
        }

        throw new ProjectStructureAgentException(400, "UnsupportedMermaidDiagram", "Only Mermaid mindmap and flowchart imports are currently supported.");
    }

    private static ProjectStructureImportPlan ParseDocx(ProjectStructureImportRequest request, ICollection<string> warnings)
    {
        var bytes = ResolveRequiredSourceBytes(request, "DOCX import requires a source file.");
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry is null)
        {
            throw new ProjectStructureAgentException(400, "InvalidDocxSource", "The provided DOCX source did not contain word/document.xml.");
        }

        using var documentStream = documentEntry.Open();
        var document = XDocument.Load(documentStream);
        XNamespace wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var headings = document.Descendants(wordNamespace + "p")
            .Select(paragraph => new ParsedOutlineHeading(
                ResolveHeadingLevel(paragraph
                    .Descendants(wordNamespace + "pStyle")
                    .Attributes(wordNamespace + "val")
                    .Select(attribute => attribute.Value)
                    .FirstOrDefault() ?? string.Empty),
                string.Concat(paragraph.Descendants(wordNamespace + "t").Select(node => node.Value)).Trim()))
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToList();

        if (headings.Count == 0)
        {
            throw new ProjectStructureAgentException(400, "EmptyDocxSource", "The DOCX source did not contain any importable text.");
        }

        if (headings.All(item => item.Level < 0))
        {
            warnings.Add("DOCX source had no heading styles. Paragraphs were imported as flat work items.");
        }

        return new ProjectStructureImportPlan(BuildOutlineTree(headings), []);
    }

    private static ProjectStructureImportPlan ParseXmind(ProjectStructureImportRequest request, ICollection<string> warnings)
    {
        var bytes = ResolveRequiredSourceBytes(request, "XMind import requires a source file.");
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var jsonEntry = archive.GetEntry("content.json");
        if (jsonEntry is not null)
        {
            using var jsonStream = jsonEntry.Open();
            using var reader = new StreamReader(jsonStream, Encoding.UTF8);
            return ParseXmindJson(reader.ReadToEnd());
        }

        var xmlEntry = archive.GetEntry("content.xml");
        if (xmlEntry is not null)
        {
            using var xmlStream = xmlEntry.Open();
            var document = XDocument.Load(xmlStream);
            XNamespace xmindNamespace = "urn:xmind:xmap:xmlns:content:2.0";
            var topic = document.Descendants(xmindNamespace + "topic").FirstOrDefault();
            if (topic is null)
            {
                throw new ProjectStructureAgentException(400, "EmptyXmindSource", "The XMind source did not contain any topics.");
            }

            return new ProjectStructureImportPlan([ParseXmindXmlTopic(topic, xmindNamespace)], []);
        }

        warnings.Add("Unsupported XMind package layout. Only content.json and content.xml are currently understood.");
        return new ProjectStructureImportPlan([], []);
    }

    private static ProjectStructureImportPlan ParseJsonOutline(ProjectStructureImportRequest request)
    {
        var sourceText = ResolveRequiredSourceText(request);
        using var document = JsonDocument.Parse(sourceText);
        var rootNodes = new List<ImportedNodeDraft>();
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                rootNodes.Add(ParseJsonOutlineNode(item));
            }
        }
        else if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            rootNodes.Add(ParseJsonOutlineNode(document.RootElement));
        }
        else
        {
            throw new ProjectStructureAgentException(400, "InvalidJsonOutline", "JSON outline import expects an object or array.");
        }

        return new ProjectStructureImportPlan(rootNodes, []);
    }

    private static ProjectStructureImportPlan ParseMermaidFlowchart(string sourceText, ICollection<string> warnings)
    {
        var nodes = new Dictionary<string, ImportedNodeDraft>(StringComparer.OrdinalIgnoreCase);
        var links = new List<ImportedLinkDraft>();
        var lines = sourceText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("flowchart", StringComparison.OrdinalIgnoreCase) &&
                           !line.StartsWith("graph ", StringComparison.OrdinalIgnoreCase) &&
                           !line.StartsWith("%%", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var line in lines)
        {
            var segments = line.Split("-->", StringSplitOptions.TrimEntries);
            if (segments.Length != 2)
            {
                warnings.Add($"Skipped Mermaid flowchart line '{line}' because it did not match a simple '-->' edge.");
                continue;
            }

            var source = ParseMermaidFlowchartNode(segments[0]);
            var target = ParseMermaidFlowchartNode(segments[1]);
            nodes[source.Key] = source;
            nodes[target.Key] = target;
            links.Add(new ImportedLinkDraft(source.Key, target.Key, ProjectObjectLinkKind.DependsOn));
        }

        return new ProjectStructureImportPlan(nodes.Values.ToList(), links);
    }

    private static List<ImportedNodeDraft> ParseMermaidMindmap(string sourceText)
    {
        var lines = sourceText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.TrimStart().StartsWith("mindmap", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.TrimStart().StartsWith("%%", StringComparison.OrdinalIgnoreCase))
            .Select(line => new ParsedIndentedLine(CountIndent(line), ExtractMermaidLabel(line)))
            .Where(line => !string.IsNullOrWhiteSpace(line.Label))
            .ToList();
        if (lines.Count == 0)
        {
            throw new ProjectStructureAgentException(400, "EmptyMermaidMindmap", "The Mermaid mindmap source did not contain any importable nodes.");
        }

        var rootNodes = new List<ImportedNodeDraft>();
        var stack = new Stack<(int Indent, ImportedNodeDraft Node)>();
        foreach (var line in lines)
        {
            var node = new ImportedNodeDraft(CreateImportNodeKey(line.Label), line.Label, string.Empty, []);
            while (stack.Count > 0 && line.Indent <= stack.Peek().Indent)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                rootNodes.Add(node);
            }
            else
            {
                stack.Peek().Node.Children.Add(node);
            }

            stack.Push((line.Indent, node));
        }

        return rootNodes;
    }

    private static ImportedNodeDraft ParseJsonOutlineNode(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ProjectStructureAgentException(400, "InvalidJsonOutlineNode", "Each JSON outline node must be an object.");
        }

        var title = element.TryGetProperty("title", out var titleElement)
            ? titleElement.GetString()
            : element.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ProjectStructureAgentException(400, "JsonOutlineTitleRequired", "Each JSON outline node must provide a title or name.");
        }

        var notes = element.TryGetProperty("notes", out var notesElement)
            ? notesElement.GetString() ?? string.Empty
            : string.Empty;
        var node = new ImportedNodeDraft(CreateImportNodeKey(title), title.Trim(), notes.Trim(), []);
        if (element.TryGetProperty("children", out var childrenElement) &&
            childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenElement.EnumerateArray())
            {
                node.Children.Add(ParseJsonOutlineNode(child));
            }
        }

        return node;
    }

    private static ProjectStructureImportPlan ParseXmindJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ProjectStructureAgentException(400, "InvalidXmindJson", "XMind content.json must contain an array of sheets.");
        }

        var rootNodes = new List<ImportedNodeDraft>();
        foreach (var sheet in document.RootElement.EnumerateArray())
        {
            if (sheet.TryGetProperty("rootTopic", out var rootTopic))
            {
                rootNodes.Add(ParseXmindJsonTopic(rootTopic));
            }
        }

        if (rootNodes.Count == 0)
        {
            throw new ProjectStructureAgentException(400, "EmptyXmindSource", "The XMind source did not contain any root topics.");
        }

        return new ProjectStructureImportPlan(rootNodes, []);
    }

    private static ImportedNodeDraft ParseXmindJsonTopic(JsonElement topic)
    {
        var title = topic.TryGetProperty("title", out var titleElement)
            ? titleElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ProjectStructureAgentException(400, "XmindTopicTitleRequired", "Each XMind topic must contain a title.");
        }

        var node = new ImportedNodeDraft(CreateImportNodeKey(title), title.Trim(), string.Empty, []);
        if (topic.TryGetProperty("children", out var children) &&
            children.ValueKind == JsonValueKind.Object &&
            children.TryGetProperty("attached", out var attached) &&
            attached.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in attached.EnumerateArray())
            {
                node.Children.Add(ParseXmindJsonTopic(child));
            }
        }

        return node;
    }

    private static ImportedNodeDraft ParseXmindXmlTopic(XElement topic, XNamespace ns)
    {
        var title = topic.Element(ns + "title")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ProjectStructureAgentException(400, "XmindTopicTitleRequired", "Each XMind topic must contain a title.");
        }

        var node = new ImportedNodeDraft(CreateImportNodeKey(title), title, string.Empty, []);
        foreach (var childTopic in topic
                     .Elements(ns + "children")
                     .Elements(ns + "topics")
                     .Elements(ns + "topic"))
        {
            node.Children.Add(ParseXmindXmlTopic(childTopic, ns));
        }

        return node;
    }

    private static List<ImportedNodeDraft> BuildOutlineTree(IReadOnlyList<ParsedOutlineHeading> headings)
    {
        var rootNodes = new List<ImportedNodeDraft>();
        var stack = new Stack<(int Level, ImportedNodeDraft Node)>();
        foreach (var heading in headings)
        {
            var effectiveLevel = heading.Level >= 0 ? heading.Level : stack.Count == 0 ? 0 : stack.Peek().Level;
            var node = new ImportedNodeDraft(CreateImportNodeKey(heading.Text), heading.Text, string.Empty, []);
            while (stack.Count > 0 && effectiveLevel <= stack.Peek().Level)
            {
                stack.Pop();
            }

            if (stack.Count == 0)
            {
                rootNodes.Add(node);
            }
            else
            {
                stack.Peek().Node.Children.Add(node);
            }

            stack.Push((effectiveLevel, node));
        }

        return rootNodes;
    }

    private static int ResolveHeadingLevel(string styleValue)
    {
        if (styleValue.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(styleValue["Heading".Length..], out var level))
        {
            return Math.Max(0, level - 1);
        }

        return -1;
    }

    private static ImportedNodeDraft ParseMermaidFlowchartNode(string segment)
    {
        var trimmed = segment.Trim();
        var label = ExtractMermaidLabel(trimmed);
        var key = trimmed.Split(['[', '(', '{', '>'], 2, StringSplitOptions.TrimEntries).FirstOrDefault();
        return new ImportedNodeDraft(
            CreateImportNodeKey(string.IsNullOrWhiteSpace(key) ? label : key),
            string.IsNullOrWhiteSpace(label) ? trimmed : label,
            string.Empty,
            []);
    }

    private static string ResolveRequiredSourceText(ProjectStructureImportRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SourceText))
        {
            return request.SourceText.Trim();
        }

        var bytes = ResolveRequiredSourceBytes(request, $"Import source '{request.SourceKind}' requires either source text or a readable source asset.");
        return Encoding.UTF8.GetString(bytes).Trim();
    }

    private static byte[] ResolveRequiredSourceBytes(ProjectStructureImportRequest request, string errorMessage)
    {
        if (request.SourceAsset is null)
        {
            throw new ProjectStructureAgentException(400, "SourceAssetRequired", errorMessage);
        }

        ValidateBase64Payload(request.SourceAsset.Base64Data);
        return Convert.FromBase64String(request.SourceAsset.Base64Data);
    }

    private static void ValidateBase64Payload(string? base64Data)
    {
        if (string.IsNullOrWhiteSpace(base64Data))
        {
            throw new ProjectStructureAgentException(400, "SourcePayloadRequired", "The provided source asset did not include any content.");
        }

        try
        {
            _ = Convert.FromBase64String(base64Data.Trim());
        }
        catch (FormatException ex)
        {
            throw new ProjectStructureAgentException(400, "InvalidBase64Payload", "The provided source asset content was not valid base64.", ex.Message);
        }
    }

    private static string ResolveSourceAssetSubtype(ProjectStructureImportSourceKind sourceKind, ProjectObjectMediaPayload media)
    {
        if (sourceKind == ProjectStructureImportSourceKind.DocxOutline)
        {
            return "docx";
        }

        if (sourceKind == ProjectStructureImportSourceKind.Mermaid)
        {
            return "mermaid";
        }

        if (sourceKind == ProjectStructureImportSourceKind.XmindMap)
        {
            return "archive";
        }

        return ProjectObjectMetadataSerializer.InferFileSubtype(string.Empty, media.FileName, media.ContentType)
            .ToString()
            .ToLowerInvariant();
    }

    private static string ExtractMermaidLabel(string line)
    {
        var trimmed = line.Trim();
        foreach (var (start, end) in new[] { ('[', ']'), ('(', ')'), ('{', '}'), ('<', '>') })
        {
            var startIndex = trimmed.IndexOf(start);
            var endIndex = trimmed.LastIndexOf(end);
            if (startIndex >= 0 && endIndex > startIndex)
            {
                var candidate = trimmed[(startIndex + 1)..endIndex].Trim();
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
        }

        return trimmed
            .TrimStart('*', '-', '+')
            .Replace("::icon", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string CreateImportNodeKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Guid.NewGuid().ToString("N");
        }

        var sanitized = new string(raw
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        sanitized = sanitized.Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? Guid.NewGuid().ToString("N") : sanitized;
    }

    private static int CountIndent(string line)
    {
        var indent = 0;
        foreach (var character in line)
        {
            if (character == ' ')
            {
                indent++;
                continue;
            }

            if (character == '\t')
            {
                indent += 4;
                continue;
            }

            break;
        }

        return indent;
    }

    private sealed record ParsedIndentedLine(int Indent, string Label);

    private sealed record ParsedOutlineHeading(int Level, string Text);

    private sealed record ImportedLinkDraft(string SourceKey, string TargetKey, ProjectObjectLinkKind Kind);

    private sealed record ProjectStructureImportPlan(IReadOnlyList<ImportedNodeDraft> RootNodes, IReadOnlyList<ImportedLinkDraft> Links);

    private sealed record ImportedNodeDraft(string Key, string Title, string Notes, List<ImportedNodeDraft> Children);
}
