using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceFileQueryService
{
    private const int MaxSearchFiles = 512;
    private const int MaxDiffInputLines = 240;

    private readonly WorkspacePathPolicy pathPolicy;
    private readonly WorkspaceFileReceiptWriter receiptWriter;
    private readonly WorkspaceTextContentGuard textContentGuard;

    public WorkspaceFileQueryService(
        WorkspacePathPolicy pathPolicy,
        WorkspaceFileReceiptWriter receiptWriter,
        WorkspaceTextContentGuard textContentGuard)
    {
        this.pathPolicy = pathPolicy;
        this.receiptWriter = receiptWriter;
        this.textContentGuard = textContentGuard;
    }

    public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(relativePath, allowWorkspaceRoot: true, out var resolution, out var validationMessage))
        {
            return new WorkspaceFileListResult(
                Succeeded: false,
                Message: validationMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_list_files", false, "Denied", validationMessage, string.Empty, [], [], startedAtUtc),
                RootPath: ".",
                SearchPattern: NormalizeSearchPattern(searchPattern),
                Entries: [],
                IsTruncated: false);
        }

        if (File.Exists(resolution.FullPath))
        {
            var entry = CreateListEntry(resolution.FullPath);
            var resolvedFileMessage = $"Resolved '{entry.RelativePath}' as a workspace file.";
            return new WorkspaceFileListResult(
                Succeeded: true,
                Message: resolvedFileMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_list_files", false, "Succeeded", resolvedFileMessage, string.Empty, [entry.RelativePath], [], startedAtUtc),
                RootPath: resolution.RelativePath,
                SearchPattern: NormalizeSearchPattern(searchPattern),
                Entries: [entry],
                IsTruncated: false);
        }

        if (!Directory.Exists(resolution.FullPath))
        {
            var missingPathMessage = $"Workspace path '{relativePath ?? "."}' does not exist.";
            return new WorkspaceFileListResult(
                Succeeded: false,
                Message: missingPathMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_list_files", false, "Failed", missingPathMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
                RootPath: resolution.RelativePath,
                SearchPattern: NormalizeSearchPattern(searchPattern),
                Entries: [],
                IsTruncated: false);
        }

        var limit = Math.Clamp(maxResults, 1, 400);
        var entries = new List<WorkspaceFileListEntry>();
        var truncated = false;

        var normalizedSearchPattern = NormalizeSearchPattern(searchPattern);
        var enumerationSearchPattern = GetEnumerationSearchPattern(normalizedSearchPattern);
        foreach (var path in Directory.EnumerateFileSystemEntries(
                     resolution.FullPath,
                     enumerationSearchPattern,
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = true,
                         AttributesToSkip = 0
                     }))
        {
            if (ShouldIgnorePath(path))
            {
                continue;
            }

            if (!MatchesSearchPattern(resolution.FullPath, path, normalizedSearchPattern))
            {
                continue;
            }

            if (entries.Count >= limit)
            {
                truncated = true;
                break;
            }

            entries.Add(CreateListEntry(path));
        }

        entries = entries
            .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var listMessage = entries.Count == 0
            ? $"No workspace paths matched '{NormalizeSearchPattern(searchPattern)}' under '{resolution.RelativePath}'."
            : $"Listed {entries.Count} workspace path(s) under '{resolution.RelativePath}'.";

        return new WorkspaceFileListResult(
            Succeeded: true,
            Message: listMessage,
            Receipt: receiptWriter.CreateReceipt("workspace_list_files", false, "Succeeded", listMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
            RootPath: resolution.RelativePath,
            SearchPattern: NormalizeSearchPattern(searchPattern),
            Entries: entries,
            IsTruncated: truncated);
    }

    public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(query))
        {
            const string message = "Provide a search query.";
            return new WorkspaceTextSearchResult(
                Succeeded: false,
                Message: message,
                Receipt: receiptWriter.CreateReceipt("workspace_search", false, "Denied", message, string.Empty, [], [], startedAtUtc),
                Query: string.Empty,
                RootPath: ".",
                Matches: [],
                IsTruncated: false);
        }

        if (!pathPolicy.TryResolveWorkspacePath(relativePath, allowWorkspaceRoot: true, out var resolution, out var validationMessage))
        {
            return new WorkspaceTextSearchResult(
                Succeeded: false,
                Message: validationMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_search", false, "Denied", validationMessage, string.Empty, [], [], startedAtUtc),
                Query: query,
                RootPath: ".",
                Matches: [],
                IsTruncated: false);
        }

        if (!File.Exists(resolution.FullPath) && !Directory.Exists(resolution.FullPath))
        {
            var missingPathMessage = $"Workspace path '{relativePath ?? "."}' does not exist.";
            return new WorkspaceTextSearchResult(
                Succeeded: false,
                Message: missingPathMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_search", false, "Failed", missingPathMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
                Query: query,
                RootPath: resolution.RelativePath,
                Matches: [],
                IsTruncated: false);
        }

        var terms = TokenizeQuery(query);
        if (terms.Count == 0)
        {
            const string message = "The search query did not contain any searchable terms.";
            return new WorkspaceTextSearchResult(
                Succeeded: false,
                Message: message,
                Receipt: receiptWriter.CreateReceipt("workspace_search", false, "Denied", message, string.Empty, [resolution.RelativePath], [], startedAtUtc),
                Query: query,
                RootPath: resolution.RelativePath,
                Matches: [],
                IsTruncated: false);
        }

        if (File.Exists(resolution.FullPath))
        {
            return SearchSingleFile(query, terms, resolution, startedAtUtc);
        }

        var limit = Math.Clamp(maxResults, 1, 50);
        var matches = new List<WorkspaceTextSearchMatch>();
        var skippedGuardedFiles = 0;
        var fileLimitReached = false;
        var searchedFiles = 0;

        foreach (var filePath in EnumerateSearchFiles(resolution.FullPath))
        {
            if (searchedFiles >= MaxSearchFiles)
            {
                fileLimitReached = true;
                break;
            }

            searchedFiles++;
            var relativeFilePath = pathPolicy.ToRelativePath(filePath);
            var guardFailure = textContentGuard.TryLoadForSearch(filePath, relativeFilePath, out var text);
            if (guardFailure != WorkspaceTextGuardFailure.None)
            {
                skippedGuardedFiles++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var score = terms.Sum(term => CountOccurrences(text, term));
            if (score <= 0)
            {
                continue;
            }

            matches.Add(new WorkspaceTextSearchMatch(
                RelativePath: relativeFilePath,
                Score: score,
                Snippet: BuildSearchSnippet(text, terms)));
        }

        matches = matches
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var truncated = fileLimitReached || matches.Count > limit;
        if (matches.Count > limit)
        {
            matches = matches.Take(limit).ToList();
        }

        var resultMessage = matches.Count == 0
            ? $"No matches found for '{query}'."
            : $"Found {matches.Count} workspace match(es) for '{query}'.";

        if (skippedGuardedFiles > 0)
        {
            resultMessage += $" Skipped {skippedGuardedFiles} binary or oversized file(s).";
        }

        return new WorkspaceTextSearchResult(
            Succeeded: true,
            Message: resultMessage,
            Receipt: receiptWriter.CreateReceipt("workspace_search", false, "Succeeded", resultMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
            Query: query,
            RootPath: resolution.RelativePath,
            Matches: matches,
            IsTruncated: truncated);
    }

    public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return new WorkspaceTextFileReadResult(
                Succeeded: false,
                Message: validationMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_read_file", false, "Denied", validationMessage, string.Empty, [], [], startedAtUtc),
                Path: string.Empty,
                Content: string.Empty,
                TotalCharacters: 0,
                IsTruncated: false);
        }

        if (!File.Exists(resolution.FullPath))
        {
            var missingFileMessage = $"File '{path}' does not exist in the workspace.";
            return new WorkspaceTextFileReadResult(
                Succeeded: false,
                Message: missingFileMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_read_file", false, "Failed", missingFileMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
                Path: resolution.RelativePath,
                Content: string.Empty,
                TotalCharacters: 0,
                IsTruncated: false);
        }

        var loaded = textContentGuard.LoadForRead(resolution.FullPath, resolution.RelativePath, maxCharacters);
        if (!loaded.Succeeded)
        {
            var outcome = loaded.Failure is WorkspaceTextGuardFailure.TooLarge or WorkspaceTextGuardFailure.Binary
                ? "Denied"
                : "Failed";
            return new WorkspaceTextFileReadResult(
                Succeeded: false,
                Message: loaded.Message,
                Receipt: receiptWriter.CreateReceipt("workspace_read_file", false, outcome, loaded.Message, string.Empty, [resolution.RelativePath], [], startedAtUtc),
                Path: resolution.RelativePath,
                Content: string.Empty,
                TotalCharacters: 0,
                IsTruncated: false);
        }

        var readMessage = loaded.IsTruncated
            ? $"Read a bounded preview of '{resolution.RelativePath}'."
            : $"Read '{resolution.RelativePath}'.";
        return new WorkspaceTextFileReadResult(
            Succeeded: true,
            Message: readMessage,
            Receipt: receiptWriter.CreateReceipt("workspace_read_file", false, "Succeeded", readMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
            Path: resolution.RelativePath,
            Content: loaded.Content,
            TotalCharacters: loaded.TotalCharacters,
            IsTruncated: loaded.IsTruncated);
    }

    public WorkspacePathStatResult StatPath(string path)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(path, allowWorkspaceRoot: false, out var resolution, out var validationMessage))
        {
            return new WorkspacePathStatResult(
                Succeeded: false,
                Message: validationMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_stat_path", false, "Denied", validationMessage, string.Empty, [], [], startedAtUtc),
                Path: string.Empty,
                Exists: false,
                PathKind: "missing",
                SizeBytes: null,
                LastWriteTimeUtc: null,
                ChildCount: null);
        }

        if (File.Exists(resolution.FullPath))
        {
            var info = new FileInfo(resolution.FullPath);
            var statMessage = $"'{resolution.RelativePath}' is a workspace file.";
            return new WorkspacePathStatResult(
                Succeeded: true,
                Message: statMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_stat_path", false, "Succeeded", statMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
                Path: resolution.RelativePath,
                Exists: true,
                PathKind: "file",
                SizeBytes: info.Length,
                LastWriteTimeUtc: info.LastWriteTimeUtc,
                ChildCount: null);
        }

        if (Directory.Exists(resolution.FullPath))
        {
            var info = new DirectoryInfo(resolution.FullPath);
            var childCount = Directory.EnumerateFileSystemEntries(resolution.FullPath, "*", new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = 0
            }).Count();
            var message = $"'{resolution.RelativePath}' is a workspace directory.";
            return new WorkspacePathStatResult(
                Succeeded: true,
                Message: message,
                Receipt: receiptWriter.CreateReceipt("workspace_stat_path", false, "Succeeded", message, string.Empty, [resolution.RelativePath], [], startedAtUtc),
                Path: resolution.RelativePath,
                Exists: true,
                PathKind: "directory",
                SizeBytes: null,
                LastWriteTimeUtc: info.LastWriteTimeUtc,
                ChildCount: childCount);
        }

        var missingMessage = $"Workspace path '{resolution.RelativePath}' does not exist.";
        return new WorkspacePathStatResult(
            Succeeded: true,
            Message: missingMessage,
            Receipt: receiptWriter.CreateReceipt("workspace_stat_path", false, "Succeeded", missingMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
            Path: resolution.RelativePath,
            Exists: false,
            PathKind: "missing",
            SizeBytes: null,
            LastWriteTimeUtc: null,
            ChildCount: null);
    }

    public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        if (!pathPolicy.TryResolveWorkspacePath(leftPath, allowWorkspaceRoot: false, out var leftResolution, out var leftValidation))
        {
            return CreateDiffFailure(leftValidation, string.Empty, string.Empty, startedAtUtc);
        }

        if (!pathPolicy.TryResolveWorkspacePath(rightPath, allowWorkspaceRoot: false, out var rightResolution, out var rightValidation))
        {
            return CreateDiffFailure(rightValidation, leftResolution.RelativePath, string.Empty, startedAtUtc);
        }

        if (!File.Exists(leftResolution.FullPath))
        {
            return CreateDiffFailure($"Left path '{leftResolution.RelativePath}' does not exist as a file in the workspace.", leftResolution.RelativePath, rightResolution.RelativePath, startedAtUtc);
        }

        if (!File.Exists(rightResolution.FullPath))
        {
            return CreateDiffFailure($"Right path '{rightResolution.RelativePath}' does not exist as a file in the workspace.", leftResolution.RelativePath, rightResolution.RelativePath, startedAtUtc);
        }

        var leftLoaded = textContentGuard.LoadForDiff(leftResolution.FullPath, leftResolution.RelativePath);
        if (!leftLoaded.Succeeded)
        {
            return CreateDiffFailure(leftLoaded.Message, leftResolution.RelativePath, rightResolution.RelativePath, startedAtUtc);
        }

        var rightLoaded = textContentGuard.LoadForDiff(rightResolution.FullPath, rightResolution.RelativePath);
        if (!rightLoaded.Succeeded)
        {
            return CreateDiffFailure(rightLoaded.Message, leftResolution.RelativePath, rightResolution.RelativePath, startedAtUtc);
        }

        var diffPreview = BuildTextDiff(
            leftResolution.RelativePath,
            rightResolution.RelativePath,
            leftLoaded.Content,
            rightLoaded.Content,
            Math.Clamp(maxLines, 20, 400),
            out var addedLineCount,
            out var removedLineCount,
            out var isTruncated);

        var message = addedLineCount == 0 && removedLineCount == 0
            ? $"'{leftResolution.RelativePath}' and '{rightResolution.RelativePath}' are identical."
            : $"Computed a bounded diff between '{leftResolution.RelativePath}' and '{rightResolution.RelativePath}'.";

        return new WorkspaceTextDiffResult(
            Succeeded: true,
            Message: message,
            Receipt: receiptWriter.CreateReceipt("workspace_diff_text", false, "Succeeded", message, string.Empty, [leftResolution.RelativePath, rightResolution.RelativePath], [], startedAtUtc),
            LeftPath: leftResolution.RelativePath,
            RightPath: rightResolution.RelativePath,
            DiffPreview: diffPreview,
            AddedLineCount: addedLineCount,
            RemovedLineCount: removedLineCount,
            IsTruncated: isTruncated);
    }

    private WorkspaceTextSearchResult SearchSingleFile(
        string query,
        IReadOnlyList<string> terms,
        WorkspacePathResolution resolution,
        DateTimeOffset startedAtUtc)
    {
        var guardFailure = textContentGuard.TryLoadForSearch(resolution.FullPath, resolution.RelativePath, out var text);
        if (guardFailure != WorkspaceTextGuardFailure.None)
        {
            var failureMessage = guardFailure switch
            {
                WorkspaceTextGuardFailure.TooLarge => $"File '{resolution.RelativePath}' exceeds the safe search limit and cannot be searched directly.",
                WorkspaceTextGuardFailure.Binary => $"File '{resolution.RelativePath}' appears to be binary and cannot be searched as text.",
                _ => $"Failed to read '{resolution.RelativePath}' for search."
            };
            var outcome = guardFailure is WorkspaceTextGuardFailure.TooLarge or WorkspaceTextGuardFailure.Binary ? "Denied" : "Failed";
            return new WorkspaceTextSearchResult(
                Succeeded: false,
                Message: failureMessage,
                Receipt: receiptWriter.CreateReceipt("workspace_search", false, outcome, failureMessage, string.Empty, [resolution.RelativePath], [], startedAtUtc),
                Query: query,
                RootPath: resolution.RelativePath,
                Matches: [],
                IsTruncated: false);
        }

        var score = terms.Sum(term => CountOccurrences(text, term));
        var matches = score <= 0
            ? Array.Empty<WorkspaceTextSearchMatch>()
            : [new WorkspaceTextSearchMatch(resolution.RelativePath, score, BuildSearchSnippet(text, terms))];
        var message = matches.Length == 0
            ? $"No matches found for '{query}'."
            : $"Found {matches.Length} workspace match(es) for '{query}'.";

        return new WorkspaceTextSearchResult(
            Succeeded: true,
            Message: message,
            Receipt: receiptWriter.CreateReceipt("workspace_search", false, "Succeeded", message, string.Empty, [resolution.RelativePath], [], startedAtUtc),
            Query: query,
            RootPath: resolution.RelativePath,
            Matches: matches,
            IsTruncated: false);
    }

    private WorkspaceTextDiffResult CreateDiffFailure(string message, string leftPath, string rightPath, DateTimeOffset startedAtUtc)
    {
        return new WorkspaceTextDiffResult(
            Succeeded: false,
            Message: message,
            Receipt: receiptWriter.CreateReceipt("workspace_diff_text", false, "Failed", message, string.Empty, BuildTargetPathList(leftPath, rightPath), [], startedAtUtc),
            LeftPath: leftPath,
            RightPath: rightPath,
            DiffPreview: string.Empty,
            AddedLineCount: 0,
            RemovedLineCount: 0,
            IsTruncated: false);
    }

    private WorkspaceFileListEntry CreateListEntry(string fullPath)
    {
        return File.Exists(fullPath)
            ? new WorkspaceFileListEntry(
                RelativePath: pathPolicy.ToRelativePath(fullPath),
                PathKind: "file",
                SizeBytes: new FileInfo(fullPath).Length,
                LastWriteTimeUtc: File.GetLastWriteTimeUtc(fullPath))
            : new WorkspaceFileListEntry(
                RelativePath: pathPolicy.ToRelativePath(fullPath),
                PathKind: "directory",
                SizeBytes: null,
                LastWriteTimeUtc: Directory.Exists(fullPath) ? Directory.GetLastWriteTimeUtc(fullPath) : null);
    }

    private IEnumerable<string> EnumerateSearchFiles(string rootPath)
    {
        if (File.Exists(rootPath))
        {
            if (!ShouldIgnoreSearchPath(rootPath, rootPath))
            {
                yield return rootPath;
            }

            yield break;
        }

        foreach (var filePath in Directory.EnumerateFiles(
                     rootPath,
                     "*",
                     new EnumerationOptions
                     {
                         RecurseSubdirectories = true,
                         IgnoreInaccessible = true,
                         AttributesToSkip = 0
                     }))
        {
            if (ShouldIgnoreSearchPath(rootPath, filePath))
            {
                continue;
            }

            yield return filePath;
        }
    }

    private static IReadOnlyList<string> TokenizeQuery(string query)
    {
        return query
            .Split([' ', '\r', '\n', '\t', ',', '.', ';', ':', '(', ')', '[', ']', '{', '}', '/', '\\', '"', '\''], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += term.Length;
        }

        return count;
    }

    private static string BuildSearchSnippet(string text, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var matchIndex = terms
            .Select(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(0)
            .Min();
        var start = Math.Max(0, matchIndex - 80);
        var length = Math.Min(text.Length - start, 220);
        var snippet = text.Substring(start, length).ReplaceLineEndings(" ");
        return start > 0 ? "..." + snippet.Trim() : snippet.Trim();
    }

    private static string BuildTextDiff(
        string leftLabel,
        string rightLabel,
        string leftText,
        string rightText,
        int maxPreviewLines,
        out int addedLineCount,
        out int removedLineCount,
        out bool isTruncated)
    {
        var leftLines = SplitLines(leftText);
        var rightLines = SplitLines(rightText);
        isTruncated = leftLines.Length > MaxDiffInputLines || rightLines.Length > MaxDiffInputLines;
        if (isTruncated)
        {
            leftLines = leftLines.Take(MaxDiffInputLines).ToArray();
            rightLines = rightLines.Take(MaxDiffInputLines).ToArray();
        }

        var lcs = BuildLcsMatrix(leftLines, rightLines);
        var builder = new StringBuilder();
        builder.AppendLine($"--- {leftLabel}");
        builder.AppendLine($"+++ {rightLabel}");

        addedLineCount = 0;
        removedLineCount = 0;
        var emittedPreviewLines = 0;
        var previewTruncated = false;
        var leftIndex = 0;
        var rightIndex = 0;

        while (leftIndex < leftLines.Length || rightIndex < rightLines.Length)
        {
            if (leftIndex < leftLines.Length
                && rightIndex < rightLines.Length
                && string.Equals(leftLines[leftIndex], rightLines[rightIndex], StringComparison.Ordinal))
            {
                leftIndex++;
                rightIndex++;
                continue;
            }

            if (rightIndex < rightLines.Length
                && (leftIndex == leftLines.Length || lcs[leftIndex, rightIndex + 1] >= lcs[leftIndex + 1, rightIndex]))
            {
                addedLineCount++;
                AppendDiffLine(builder, '+', rightLines[rightIndex], maxPreviewLines, ref emittedPreviewLines, ref previewTruncated);
                rightIndex++;
                continue;
            }

            if (leftIndex < leftLines.Length)
            {
                removedLineCount++;
                AppendDiffLine(builder, '-', leftLines[leftIndex], maxPreviewLines, ref emittedPreviewLines, ref previewTruncated);
                leftIndex++;
            }
        }

        if (previewTruncated)
        {
            builder.AppendLine("... diff preview truncated ...");
            isTruncated = true;
        }

        return builder.ToString().TrimEnd();
    }

    private static string[] SplitLines(string text)
        => text.ReplaceLineEndings("\n").Split('\n');

    private static int[,] BuildLcsMatrix(IReadOnlyList<string> leftLines, IReadOnlyList<string> rightLines)
    {
        var matrix = new int[leftLines.Count + 1, rightLines.Count + 1];
        for (var leftIndex = leftLines.Count - 1; leftIndex >= 0; leftIndex--)
        {
            for (var rightIndex = rightLines.Count - 1; rightIndex >= 0; rightIndex--)
            {
                matrix[leftIndex, rightIndex] = string.Equals(leftLines[leftIndex], rightLines[rightIndex], StringComparison.Ordinal)
                    ? matrix[leftIndex + 1, rightIndex + 1] + 1
                    : Math.Max(matrix[leftIndex + 1, rightIndex], matrix[leftIndex, rightIndex + 1]);
            }
        }

        return matrix;
    }

    private static void AppendDiffLine(StringBuilder builder, char prefix, string value, int maxPreviewLines, ref int emittedPreviewLines, ref bool previewTruncated)
    {
        if (emittedPreviewLines >= maxPreviewLines)
        {
            previewTruncated = true;
            return;
        }

        builder.Append(prefix);
        builder.AppendLine(value);
        emittedPreviewLines++;
    }

    private bool ShouldIgnoreSearchPath(string searchRootPath, string fullPath)
    {
        if (ShouldIgnorePath(fullPath))
        {
            return true;
        }

        var searchRootRelativePath = pathPolicy.ToRelativePath(searchRootPath);
        var fileRelativePath = pathPolicy.ToRelativePath(fullPath);
        var targetedExternalSearch = IsExternalTargetRelativePath(searchRootRelativePath);
        if (!targetedExternalSearch && IsExternalTargetRelativePath(fileRelativePath))
        {
            return true;
        }

        return WorkspaceRetrievalNoisePolicy.ShouldExcludeFromAmbientRetrieval(pathPolicy.WorkspaceRoot, fullPath);
    }

    private static bool IsExternalTargetRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var normalized = relativePath.Replace('\\', '/').Trim().TrimStart('/');
        return string.Equals(normalized, "external-target", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIgnorePath(string fullPath)
    {
        return fullPath.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || fullPath.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || fullPath.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchPattern(string searchPattern)
        => string.IsNullOrWhiteSpace(searchPattern) ? "*" : searchPattern.Trim();

    private static string GetEnumerationSearchPattern(string normalizedSearchPattern)
    {
        var pattern = normalizedSearchPattern.Replace('\\', '/');
        if (pattern.Contains('/', StringComparison.Ordinal) ||
            pattern.Contains("**", StringComparison.Ordinal))
        {
            return "*";
        }

        return pattern;
    }

    private static bool MatchesSearchPattern(string rootFullPath, string candidateFullPath, string normalizedSearchPattern)
    {
        var pattern = normalizedSearchPattern.Replace('\\', '/').TrimStart('/');
        if (pattern.StartsWith("./", StringComparison.Ordinal))
        {
            pattern = pattern[2..];
        }

        if (pattern is "*" or "**" or "**/*")
        {
            return true;
        }

        var target = pattern.Contains('/', StringComparison.Ordinal)
            ? Path.GetRelativePath(rootFullPath, candidateFullPath).Replace('\\', '/')
            : Path.GetFileName(candidateFullPath);

        return MatchesGlob(target, pattern);
    }

    private static bool MatchesGlob(string value, string pattern)
    {
        if (pattern.StartsWith("**/", StringComparison.Ordinal) &&
            MatchesGlob(value, pattern[3..]))
        {
            return true;
        }

        var builder = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '*')
            {
                var nextIsStar = index + 1 < pattern.Length && pattern[index + 1] == '*';
                if (nextIsStar)
                {
                    index++;
                    var followedBySlash = index + 1 < pattern.Length && pattern[index + 1] == '/';
                    if (followedBySlash)
                    {
                        index++;
                        builder.Append("(?:.*/)?");
                    }
                    else
                    {
                        builder.Append(".*");
                    }

                    continue;
                }

                builder.Append("[^/]*");
                continue;
            }

            if (character == '?')
            {
                builder.Append("[^/]");
                continue;
            }

            if (character == '/')
            {
                builder.Append('/');
                continue;
            }

            builder.Append(Regex.Escape(character.ToString()));
        }

        builder.Append('$');
        return Regex.IsMatch(value, builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static IReadOnlyList<string> BuildTargetPathList(string? path, string? destinationPath)
    {
        return new[] { path, destinationPath }
            .OfType<string>()
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
