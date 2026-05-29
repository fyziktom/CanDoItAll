using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using ExcelDataReader;
using UglyToad.PdfPig;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class SourceIngestionWorkflowExecutor(IWorkspacePathResolutionService paths) : IWorkflowExecutor
{
    private static readonly char[] PathTrimCharacters = [' ', '\t', '\r', '\n', '`', '\'', '"'];

    static SourceIngestionWorkflowExecutor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.SourceIngestion;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowSourceIngestionExecutorSettings>(context.SettingsJson);
        using var document = JsonDocument.Parse(input.PayloadJson);
        var root = document.RootElement;
        var allowedExtensions = NormalizeExtensions(settings.AllowedExtensions);
        var sourceKeys = NormalizeKeys(settings.SourceKeys);
        var maxFiles = Math.Clamp(settings.MaxFiles, 1, 40);
        var maxCharactersPerFile = Math.Clamp(settings.MaxCharactersPerFile, 1000, 80000);
        var remainingCharacters = Math.Clamp(settings.MaxTotalCharacters, 1000, 240000);
        var candidates = CollectCandidates(root, settings, sourceKeys)
            .GroupBy(candidate => $"{candidate.Kind}:{candidate.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var loaded = new List<WorkflowSourceIngestionDocument>();
        var errors = new List<WorkflowSourceIngestionError>();
        var visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var truncated = false;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (loaded.Count >= maxFiles || remainingCharacters <= 0)
            {
                truncated = true;
                break;
            }

            try
            {
                foreach (var file in ResolveCandidateFiles(candidate, settings, allowedExtensions, maxFiles - loaded.Count))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!visitedFiles.Add(file.FullPath))
                    {
                        continue;
                    }

                    var loadedDocument = ReadSourceDocument(candidate, file, maxCharactersPerFile, remainingCharacters);
                    loaded.Add(loadedDocument);
                    remainingCharacters -= loadedDocument.Text.Length;
                    truncated = truncated || loadedDocument.IsTruncated;
                    if (loaded.Count >= maxFiles || remainingCharacters <= 0)
                    {
                        truncated = true;
                        break;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
            {
                errors.Add(new WorkflowSourceIngestionError(
                    candidate.Key,
                    candidate.Label,
                    candidate.Kind,
                    candidate.Value,
                    candidate.Origin,
                    exception.Message));
            }
        }

        var result = new
        {
            project = TryClone(root, "project"),
            runContext = TryClone(root, "runContext"),
            parentNode = TryClone(root, "parentNode"),
            selectedNodes = TryClone(root, "selectedNodes"),
            parentSubtree = TryClone(root, "parentSubtree"),
            manualInput = TryClone(root, "manualInput"),
            sourceSummary = BuildSourceSummary(loaded, errors, truncated),
            documents = loaded,
            sourceDocuments = loaded,
            sourceErrors = errors,
            loadedSourceCount = loaded.Count,
            failedSourceCount = errors.Count,
            isTruncated = truncated
        };

        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, result));
    }

    private IEnumerable<WorkflowSourceIngestionFile> ResolveCandidateFiles(
        WorkflowSourceCandidate candidate,
        WorkflowSourceIngestionExecutorSettings settings,
        IReadOnlySet<string> allowedExtensions,
        int take)
    {
        if (take <= 0)
        {
            yield break;
        }

        var kind = candidate.Kind;
        var resolvedAsDirectory = string.Equals(kind, "folderPath", StringComparison.OrdinalIgnoreCase) ||
                                  (!string.Equals(kind, "filePath", StringComparison.OrdinalIgnoreCase) && Directory.Exists(ResolvePathForProbe(candidate.Value, settings)));

        if (resolvedAsDirectory)
        {
            var directory = ResolveDirectory(candidate.Value, settings);
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(
                         directory.FullPath,
                         "*",
                         new EnumerationOptions
                         {
                             RecurseSubdirectories = settings.RecursiveFolders,
                             IgnoreInaccessible = true,
                             AttributesToSkip = 0
                         })
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!IsAllowedExtension(file, allowedExtensions))
                {
                    continue;
                }

                yield return new WorkflowSourceIngestionFile(
                    file,
                    ToDisplayPath(file, directory),
                    Path.GetFileName(file));
                count++;
                if (count >= take)
                {
                    yield break;
                }
            }

            yield break;
        }

        var resolvedFile = ResolveFile(candidate.Value, settings);
        if (!IsAllowedExtension(resolvedFile.FullPath, allowedExtensions))
        {
            throw new InvalidOperationException($"Source file '{resolvedFile.RelativePath}' has extension '{Path.GetExtension(resolvedFile.FullPath)}', which is not allowed by this workflow source-ingestion node.");
        }

        yield return new WorkflowSourceIngestionFile(
            resolvedFile.FullPath,
            resolvedFile.RelativePath,
            Path.GetFileName(resolvedFile.FullPath));
    }

    private WorkflowSourceIngestionDocument ReadSourceDocument(
        WorkflowSourceCandidate candidate,
        WorkflowSourceIngestionFile file,
        int maxCharactersPerFile,
        int remainingCharacters)
    {
        var maxCharacters = Math.Max(0, Math.Min(maxCharactersPerFile, remainingCharacters));
        var extension = Path.GetExtension(file.FullPath).ToLowerInvariant();
        var result = extension switch
        {
            ".pdf" => ReadPdf(file.FullPath, maxCharacters),
            ".docx" => ReadDocx(file.FullPath, maxCharacters),
            ".html" or ".htm" => ReadHtml(file.FullPath, maxCharacters),
            ".zip" => ReadZipManifest(file.FullPath, maxCharacters),
            ".xls" or ".xlsx" => ReadWorkbook(file.FullPath, maxCharacters),
            _ => ReadText(file.FullPath, maxCharacters)
        };

        return new WorkflowSourceIngestionDocument(
            candidate.Key,
            candidate.Label,
            candidate.Kind,
            candidate.Origin,
            file.DisplayPath,
            file.FileName,
            extension,
            result.Text,
            result.TotalCharacters,
            result.IsTruncated,
            result.ExtractionStatus);
    }

    private static WorkflowSourceReadResult ReadText(string fullPath, int maxCharacters)
    {
        using var reader = new StreamReader(fullPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ReadBounded(reader, maxCharacters, "text");
    }

    private static WorkflowSourceReadResult ReadPdf(string fullPath, int maxCharacters)
    {
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        using var pdf = PdfDocument.Open(fullPath);
        foreach (var page in pdf.GetPages())
        {
            var pageText = page.Text ?? string.Empty;
            totalCharacters += pageText.Length;
            AppendBounded(builder, $"# Page {page.Number}{Environment.NewLine}{pageText}{Environment.NewLine}", maxCharacters, ref isTruncated);
            if (isTruncated)
            {
                break;
            }
        }

        var text = builder.ToString().Trim();
        return new WorkflowSourceReadResult(
            text,
            totalCharacters,
            isTruncated,
            string.IsNullOrWhiteSpace(text)
                ? $"pdf-pages-{pdf.NumberOfPages}-no-extractable-text"
                : $"pdf-pages-{pdf.NumberOfPages}-text");
    }

    private static WorkflowSourceReadResult ReadDocx(string fullPath, int maxCharacters)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry is null)
        {
            return new WorkflowSourceReadResult(string.Empty, 0, false, "docx-missing-document-xml");
        }

        using var stream = documentEntry.Open();
        var document = XDocument.Load(stream);
        var text = string.Join(
            Environment.NewLine,
            document.Descendants()
                .Where(element => element.Name.LocalName == "t")
                .Select(element => element.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var truncated = text.Length > maxCharacters;
        return new WorkflowSourceReadResult(
            truncated ? text[..maxCharacters] : text,
            text.Length,
            truncated,
            "docx-text");
    }

    private static WorkflowSourceReadResult ReadHtml(string fullPath, int maxCharacters)
    {
        var html = File.ReadAllText(fullPath);
        var text = Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, "<[^>]+>", " ", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
        var truncated = text.Length > maxCharacters;
        return new WorkflowSourceReadResult(
            truncated ? text[..maxCharacters] : text,
            text.Length,
            truncated,
            "html-text");
    }

    private static WorkflowSourceReadResult ReadZipManifest(string fullPath, int maxCharacters)
    {
        using var archive = ZipFile.OpenRead(fullPath);
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var truncated = false;
        foreach (var entry in archive.Entries.OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase).Take(200))
        {
            var line = $"{entry.FullName}\t{entry.Length} bytes";
            totalCharacters += line.Length;
            AppendBounded(builder, line + Environment.NewLine, maxCharacters, ref truncated);
            if (truncated)
            {
                break;
            }
        }

        return new WorkflowSourceReadResult(
            builder.ToString().Trim(),
            totalCharacters,
            truncated || archive.Entries.Count > 200,
            "zip-manifest");
    }

    private static WorkflowSourceReadResult ReadWorkbook(string fullPath, int maxCharacters)
    {
        using var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        do
        {
            var worksheetName = string.IsNullOrWhiteSpace(reader.Name) ? "Sheet" : reader.Name;
            AppendBounded(builder, $"## Worksheet: {worksheetName}{Environment.NewLine}", maxCharacters, ref isTruncated);
            var rowIndex = 0;
            while (reader.Read())
            {
                rowIndex++;
                if (rowIndex > 80)
                {
                    isTruncated = true;
                    break;
                }

                var cells = new List<string>();
                var fieldCount = Math.Min(reader.FieldCount, 20);
                for (var index = 0; index < fieldCount; index++)
                {
                    var value = reader.GetValue(index)?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value) || cells.Count > 0)
                    {
                        cells.Add(value);
                    }
                }

                if (cells.Count == 0)
                {
                    continue;
                }

                var line = string.Join(" | ", cells);
                totalCharacters += line.Length;
                AppendBounded(builder, line + Environment.NewLine, maxCharacters, ref isTruncated);
                if (isTruncated)
                {
                    break;
                }
            }
        }
        while (!isTruncated && reader.NextResult());

        return new WorkflowSourceReadResult(builder.ToString().Trim(), totalCharacters, isTruncated, "workbook-text");
    }

    private static WorkflowSourceReadResult ReadBounded(TextReader reader, int maxCharacters, string extractionStatus)
    {
        var buffer = new char[Math.Min(Math.Max(maxCharacters, 1), 8192)];
        var builder = new StringBuilder(Math.Min(maxCharacters, 8192));
        var totalCharacters = 0;
        var isTruncated = false;

        while (true)
        {
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read == 0)
            {
                break;
            }

            totalCharacters += read;
            AppendBounded(builder, new string(buffer, 0, read), maxCharacters, ref isTruncated);
            if (isTruncated)
            {
                break;
            }
        }

        return new WorkflowSourceReadResult(builder.ToString(), totalCharacters, isTruncated, extractionStatus);
    }

    private static void AppendBounded(StringBuilder builder, string value, int maxCharacters, ref bool isTruncated)
    {
        if (maxCharacters <= 0 || builder.Length >= maxCharacters)
        {
            isTruncated = true;
            return;
        }

        var remaining = maxCharacters - builder.Length;
        if (value.Length <= remaining)
        {
            builder.Append(value);
            return;
        }

        builder.Append(value.AsSpan(0, remaining));
        isTruncated = true;
    }

    private WorkspaceResolvedPath ResolveFile(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveFilePath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source file '{fullPath}' was not found.");
            }

            return new WorkspaceResolvedPath(fullPath, NormalizeAbsoluteDisplayPath(fullPath), IsWorkspacePath: false);
        }
    }

    private WorkspaceResolvedPath ResolveDirectory(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false);
        }
        catch (InvalidOperationException) when (settings.AllowAbsoluteInputPaths && Path.IsPathRooted(path))
        {
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new InvalidOperationException($"Source directory '{fullPath}' was not found.");
            }

            return new WorkspaceResolvedPath(fullPath, NormalizeAbsoluteDisplayPath(fullPath), IsWorkspacePath: false);
        }
    }

    private string ResolvePathForProbe(string value, WorkflowSourceIngestionExecutorSettings settings)
    {
        var path = NormalizeInputPath(value);
        if (Path.IsPathRooted(path))
        {
            if (!settings.AllowAbsoluteInputPaths)
            {
                return path;
            }

            return Path.GetFullPath(path);
        }

        try
        {
            return paths.ResolveDirectoryPath(path, allowMissing: false).FullPath;
        }
        catch (InvalidOperationException)
        {
            return path;
        }
    }

    private static IReadOnlyList<WorkflowSourceCandidate> CollectCandidates(
        JsonElement root,
        WorkflowSourceIngestionExecutorSettings settings,
        IReadOnlySet<string> sourceKeys)
    {
        var candidates = new List<WorkflowSourceCandidate>();
        if (settings.IncludeAdditionalSources &&
            root.TryGetProperty("sources", out var sources) &&
            sources.ValueKind == JsonValueKind.Array)
        {
            foreach (var source in sources.EnumerateArray())
            {
                if (TryReadBoolean(source, "isEnabled", out var isEnabled) && !isEnabled)
                {
                    continue;
                }

                var kind = ReadString(source, "kind");
                if (!IsPathSourceKind(kind))
                {
                    continue;
                }

                var key = ReadString(source, "key");
                if (!ShouldIncludeKey(key, sourceKeys))
                {
                    continue;
                }

                var value = ReadString(source, "value");
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                candidates.Add(new WorkflowSourceCandidate(
                    key,
                    ReadString(source, "label"),
                    kind,
                    value,
                    "additional-source"));
            }
        }

        if (settings.IncludeAdditionalSources &&
            root.TryGetProperty("outputPath", out var outputPathProperty) &&
            outputPathProperty.ValueKind == JsonValueKind.String)
        {
            var outputPath = outputPathProperty.GetString();
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                candidates.Add(new WorkflowSourceCandidate(
                    "outputPath",
                    "Previous executor output",
                    "filePath",
                    outputPath,
                    "executor-output"));
            }
        }

        if (settings.IncludeParentNodePath && root.TryGetProperty("parentNode", out var parentNode))
        {
            AddNodeCandidate(candidates, parentNode, "parent-node", sourceKeys);
        }

        if (settings.IncludeSelectedNodePaths && root.TryGetProperty("selectedNodes", out var selectedNodes))
        {
            AddNodeCandidates(candidates, selectedNodes, "selected-node", sourceKeys);
        }

        if (settings.IncludeParentSubtreePaths && root.TryGetProperty("parentSubtree", out var parentSubtree))
        {
            AddNodeCandidates(candidates, parentSubtree, "parent-subtree", sourceKeys);
        }

        return candidates;
    }

    private static void AddNodeCandidates(
        List<WorkflowSourceCandidate> candidates,
        JsonElement nodes,
        string origin,
        IReadOnlySet<string> sourceKeys)
    {
        if (nodes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var node in nodes.EnumerateArray())
        {
            AddNodeCandidate(candidates, node, origin, sourceKeys);
        }
    }

    private static void AddNodeCandidate(
        List<WorkflowSourceCandidate> candidates,
        JsonElement node,
        string origin,
        IReadOnlySet<string> sourceKeys)
    {
        var nodeId = ReadString(node, "id");
        if (!ShouldIncludeKey(nodeId, sourceKeys))
        {
            return;
        }

        var mediaPath = ReadString(node, "mediaRelativePath");
        var notes = ReadString(node, "notes");
        var candidatePath = !string.IsNullOrWhiteSpace(mediaPath)
            ? mediaPath
            : ExtractPathLine(notes);
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return;
        }

        var kind = LooksLikeFolderPath(candidatePath) ? "folderPath" : "filePath";
        candidates.Add(new WorkflowSourceCandidate(
            string.IsNullOrWhiteSpace(nodeId) ? origin : nodeId,
            ReadString(node, "title"),
            kind,
            candidatePath,
            origin));
    }

    private static JsonElement? TryClone(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.Clone();
    }

    private static string BuildSourceSummary(
        IReadOnlyList<WorkflowSourceIngestionDocument> loaded,
        IReadOnlyList<WorkflowSourceIngestionError> errors,
        bool truncated)
    {
        var sourceText = loaded.Count == 1 ? "source" : "sources";
        var summary = $"Loaded {loaded.Count} {sourceText}";
        if (errors.Count > 0)
        {
            summary += $" with {errors.Count} error(s)";
        }

        if (truncated)
        {
            summary += "; content was truncated to workflow limits";
        }

        return summary + ".";
    }

    private static IReadOnlySet<string> NormalizeKeys(IReadOnlyList<string> sourceKeys)
        => sourceKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> NormalizeExtensions(IReadOnlyList<string> extensions)
        => extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(extension => extension.Trim().StartsWith(".", StringComparison.Ordinal)
                ? extension.Trim().ToLowerInvariant()
                : "." + extension.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool ShouldIncludeKey(string key, IReadOnlySet<string> sourceKeys)
        => sourceKeys.Count == 0 || sourceKeys.Contains(key);

    private static bool IsPathSourceKind(string kind)
        => string.Equals(kind, "filePath", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(kind, "folderPath", StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedExtension(string fullPath, IReadOnlySet<string> allowedExtensions)
        => allowedExtensions.Count == 0 || allowedExtensions.Contains(Path.GetExtension(fullPath));

    private static string NormalizeInputPath(string value)
        => value.Trim(PathTrimCharacters).Replace('/', Path.DirectorySeparatorChar);

    private static string NormalizeAbsoluteDisplayPath(string value)
        => Path.GetFullPath(value).Replace('\\', '/');

    private static string ToDisplayPath(string fullPath, WorkspaceResolvedPath directory)
    {
        if (directory.IsWorkspacePath)
        {
            return NormalizeAbsoluteDisplayPath(fullPath).StartsWith(NormalizeAbsoluteDisplayPath(directory.FullPath), StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(directory.RelativePath, Path.GetRelativePath(directory.FullPath, fullPath)).Replace('\\', '/')
                : NormalizeAbsoluteDisplayPath(fullPath);
        }

        return NormalizeAbsoluteDisplayPath(fullPath);
    }

    private static string ExtractPathLine(string value)
    {
        foreach (var line in value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = ExtractEmbeddedPath(line.Trim(PathTrimCharacters));
            if (Path.IsPathRooted(candidate) ||
                candidate.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("external-target\\", StringComparison.OrdinalIgnoreCase) ||
                LooksLikeRelativeFilePath(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string ExtractEmbeddedPath(string value)
    {
        var index = FindWindowsPathStart(value);
        return index > 0
            ? value[index..].Trim(PathTrimCharacters)
            : value;
    }

    private static int FindWindowsPathStart(string value)
    {
        for (var index = 0; index < value.Length - 2; index++)
        {
            if (IsAsciiLetter(value[index]) &&
                value[index + 1] == ':' &&
                value[index + 2] is '\\' or '/')
            {
                return index;
            }
        }

        return value.IndexOf(@"\\", StringComparison.Ordinal);
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static bool LooksLikeFolderPath(string value)
    {
        var normalized = value.Trim(PathTrimCharacters);
        if (Directory.Exists(normalized))
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(Path.GetExtension(normalized));
    }

    private static bool LooksLikeRelativeFilePath(string value)
        => value.Contains('/') || value.Contains('\\') || !string.IsNullOrWhiteSpace(Path.GetExtension(value));

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.GetRawText();
    }

    private static bool TryReadBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private sealed record WorkflowSourceCandidate(
        string Key,
        string Label,
        string Kind,
        string Value,
        string Origin);

    private sealed record WorkflowSourceIngestionFile(
        string FullPath,
        string DisplayPath,
        string FileName);

    private sealed record WorkflowSourceReadResult(
        string Text,
        int TotalCharacters,
        bool IsTruncated,
        string ExtractionStatus);

    private sealed record WorkflowSourceIngestionDocument(
        string Key,
        string Label,
        string Kind,
        string Origin,
        string Path,
        string FileName,
        string Extension,
        string Text,
        int TotalCharacters,
        bool IsTruncated,
        string ExtractionStatus);

    private sealed record WorkflowSourceIngestionError(
        string Key,
        string Label,
        string Kind,
        string Value,
        string Origin,
        string Message);
}

