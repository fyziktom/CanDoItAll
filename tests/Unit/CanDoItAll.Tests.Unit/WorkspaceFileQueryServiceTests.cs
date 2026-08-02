using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFileQueryServiceTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceFileQueryServiceTests", Guid.NewGuid().ToString("N"));
    private readonly List<string> externalRoots = [];

    [Fact]
    public void ListDirectory_returns_direct_children_without_recursive_traversal()
    {
        var appRoot = CreateDirectory("apps", "FolderShape");
        WriteFile(appRoot, "Program.cs", "Console.WriteLine(\"ok\");");
        WriteFile(appRoot, "Features", "ReportService.cs", "public sealed class ReportService {}");
        var service = CreateService();

        var result = service.ListDirectory("apps/FolderShape", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("workspace_list_directory", result.Receipt.Operation);
        Assert.Equal("*", result.SearchPattern);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/FolderShape/Program.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/FolderShape/Features", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => string.Equals(item.RelativePath, "apps/FolderShape/Features/ReportService.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListFiles_supports_recursive_globstar_all_pattern()
    {
        var appRoot = CreateDirectory("apps", "TrailReport");
        WriteFile(appRoot, "Program.cs", "Console.WriteLine(\"ok\");");
        WriteFile(appRoot, "Features", "ReportService.cs", "public sealed class ReportService {}");
        WriteFile(appRoot, "obj", "Debug", "Generated.cs", "public sealed class Generated {}");
        var service = CreateService();

        var result = service.ListFiles("apps/TrailReport", "**/*", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("**/*", result.SearchPattern);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/TrailReport/Program.cs", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/TrailReport/Features/ReportService.cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => item.PathKind == "directory");
        Assert.DoesNotContain(result.Entries, item => item.RelativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListFiles_supports_recursive_globstar_extension_pattern()
    {
        var appRoot = CreateDirectory("apps", "MenuPlanner");
        WriteFile(appRoot, "Program.cs", "Console.WriteLine(\"ok\");");
        WriteFile(appRoot, "Components", "Pages", "Home.razor", "<h1>Home</h1>");
        WriteFile(appRoot, "Components", "Layout", "MainLayout.razor", "<main>@Body</main>");
        WriteFile(appRoot, "README.md", "# Menu Planner");
        var service = CreateService();

        var result = service.ListFiles("apps/MenuPlanner", "**/*.razor", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("**/*.razor", result.SearchPattern);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/MenuPlanner/Components/Pages/Home.razor", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "apps/MenuPlanner/Components/Layout/MainLayout.razor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => item.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => item.RelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListFiles_does_not_traverse_reparse_point_directories()
    {
        var appRoot = CreateDirectory("apps", "LinkedTree");
        WriteFile(appRoot, "Program.cs", "Console.WriteLine(\"ok\");");
        var outsideRoot = CreateExternalDirectory("outside");
        WriteFile(outsideRoot, "Secret.cs", "internal sealed class Secret {}");
        Directory.CreateSymbolicLink(Path.Combine(appRoot, "linked"), outsideRoot);
        var service = CreateService();

        var result = service.ListFiles("apps/LinkedTree", "**/*.cs", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Contains(result.Entries, item => item.RelativePath.EndsWith("Program.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Entries, item => item.RelativePath.Contains("Secret.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(
            result.Entries,
            item => item.RelativePath.Replace('\\', '/').Contains("/linked/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListFiles_truncated_result_reports_incomplete_discovery_and_retry_guidance()
    {
        var appRoot = CreateDirectory("apps", "BoundedList");
        WriteFile(appRoot, "First.cs", "public sealed class First {}");
        WriteFile(appRoot, "Second.cs", "public sealed class Second {}");
        var service = CreateService();

        var result = service.ListFiles("apps/BoundedList", "**/*.cs", 1);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.IsTruncated);
        Assert.Single(result.Entries);
        Assert.Contains("incomplete", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Narrow relativePath or searchPattern", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ListFiles_returns_accessible_matches_and_marks_denied_descendants_incomplete()
    {
        var appRoot = CreateDirectory("apps", "PartiallyReadable");
        var deniedRoot = CreateDirectory("apps", "PartiallyReadable", "Denied");
        WriteFile(appRoot, "Accessible", "Calculator.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        WriteFile(deniedRoot, "Hidden.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var service = CreateService(enumerateDirectoryEntries: Enumerate);

        var result = service.ListFiles("apps/PartiallyReadable", "**/*.csproj", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.IsTruncated);
        Assert.Contains(
            result.Entries,
            entry => entry.RelativePath.EndsWith("Accessible/Calculator.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            result.Entries,
            entry => entry.RelativePath.EndsWith("Hidden.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("incomplete", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("could not be inspected", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file-scan or result budget", result.Message, StringComparison.OrdinalIgnoreCase);

        IReadOnlyList<string> Enumerate(string directory)
            => string.Equals(directory, deniedRoot, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("Denied test descendant.")
                : Directory.EnumerateFileSystemEntries(directory).ToArray();
    }

    [Fact]
    public void SearchText_returns_accessible_matches_and_does_not_claim_completeness_after_descendant_denial()
    {
        var appRoot = CreateDirectory("apps", "PartiallySearchable");
        var deniedRoot = CreateDirectory("apps", "PartiallySearchable", "Denied");
        WriteFile(appRoot, "Accessible", "Program.cs", "calculator needle");
        WriteFile(deniedRoot, "Hidden.cs", "calculator needle");
        var service = CreateService(enumerateDirectoryEntries: Enumerate);

        var result = service.SearchText("calculator needle", "apps/PartiallySearchable", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.IsTruncated);
        Assert.Single(result.Matches);
        Assert.EndsWith(
            "Accessible/Program.cs",
            result.Matches[0].RelativePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("incomplete", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("could not be inspected", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file-scan or result budget", result.Message, StringComparison.OrdinalIgnoreCase);

        IReadOnlyList<string> Enumerate(string directory)
            => string.Equals(directory, deniedRoot, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("Denied test descendant.")
                : Directory.EnumerateFileSystemEntries(directory).ToArray();
    }

    [Fact]
    public void SearchText_zero_matches_after_file_budget_reports_inconclusive_result_and_retry_guidance()
    {
        var appRoot = CreateDirectory("apps", "BoundedSearch");
        for (var index = 0; index <= 512; index++)
        {
            WriteFile(appRoot, $"File{index:D3}.txt", "irrelevant content");
        }

        var service = CreateService();

        var result = service.SearchText("needle", "apps/BoundedSearch", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.IsTruncated);
        Assert.Empty(result.Matches);
        Assert.Contains("first 512", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("incomplete", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("narrow relativePath and retry", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No matches found for", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ListAndSearch_stop_at_traversal_budget_for_many_nonmatching_directories()
    {
        var root = CreateDirectory("apps", "TraversalBudget");
        var directories = Enumerable.Range(0, 8)
            .Select(index => CreateDirectory("apps", "TraversalBudget", $"Directory{index}"))
            .ToArray();
        var repeatedDirectories = Enumerable.Range(0, 2_050)
            .Select(index => directories[index % directories.Length])
            .ToArray();
        var service = CreateService(enumerateDirectoryEntries: Enumerate);

        var listResult = service.ListFiles(
            "apps/TraversalBudget",
            "**/*.csproj",
            maxResults: 20);
        var searchResult = service.SearchText(
            "missing needle",
            "apps/TraversalBudget",
            maxResults: 20);

        Assert.True(listResult.Succeeded, listResult.Message);
        Assert.True(listResult.IsTruncated);
        Assert.Empty(listResult.Entries);
        Assert.Contains("traversal budget", listResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before treating the listing as conclusive", listResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(searchResult.Succeeded, searchResult.Message);
        Assert.True(searchResult.IsTruncated);
        Assert.Empty(searchResult.Matches);
        Assert.Contains("traversal budget", searchResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("narrow relativePath and retry", searchResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file-scan or result budget", searchResult.Message, StringComparison.OrdinalIgnoreCase);

        IReadOnlyList<string> Enumerate(string directory)
            => string.Equals(directory, root, StringComparison.OrdinalIgnoreCase)
                ? repeatedDirectories
                : [];
    }

    [Fact]
    public void Text_guard_search_returns_accessible_sibling_and_marks_denied_file_incomplete()
    {
        var probeRoot = CreateDirectory("text-guard-probe");
        var accessiblePath = Path.Combine(probeRoot, "accessible.txt");
        var deniedPath = Path.Combine(probeRoot, "denied.txt");
        File.WriteAllText(accessiblePath, "probe needle");
        File.WriteAllText(deniedPath, "probe needle");
        var accessDeniedGuard = new WorkspaceTextContentGuard(
            path => string.Equals(path, deniedPath, StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("Sensitive native access failure.")
                : File.OpenRead(path));
        var readFailureGuard = new WorkspaceTextContentGuard(
            _ => throw new IOException("Transient read failure."));
        var binaryGuard = new WorkspaceTextContentGuard(
            _ => new MemoryStream([0, 1, 2, 3]));
        var queryService = CreateService(accessDeniedGuard);

        var searchResult = queryService.SearchText(
            "needle",
            "text-guard-probe",
            maxResults: 20);

        Assert.True(searchResult.Succeeded, searchResult.Message);
        Assert.True(searchResult.IsTruncated);
        var match = Assert.Single(searchResult.Matches);
        Assert.EndsWith("accessible.txt", match.RelativePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 searchable file could not be read", searchResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("before treating the search as conclusive", searchResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("file-scan or result budget", searchResult.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sensitive native access failure", searchResult.Message, StringComparison.Ordinal);

        Assert.Throws<UnauthorizedAccessException>(() =>
            queryService.ReadTextFile("text-guard-probe/denied.txt", maxCharacters: 100));
        Assert.Throws<UnauthorizedAccessException>(() =>
            queryService.DiffTextFiles(
                "text-guard-probe/denied.txt",
                "text-guard-probe/accessible.txt",
                maxLines: 20));

        var readResult = readFailureGuard.LoadForRead(
            accessiblePath,
            "text-guard-probe/accessible.txt",
            maxCharacters: 100);
        var diffResult = readFailureGuard.LoadForDiff(accessiblePath, "text-guard-probe/accessible.txt");
        var searchReadFailure = readFailureGuard.TryLoadForSearch(
            accessiblePath,
            "text-guard-probe/accessible.txt",
            out _);
        var binaryFailure = binaryGuard.TryLoadForSearch(
            accessiblePath,
            "text-guard-probe/accessible.txt",
            out _);

        Assert.False(readResult.Succeeded);
        Assert.Equal(WorkspaceTextGuardFailure.ReadFailed, readResult.Failure);
        Assert.DoesNotContain("Transient read failure", readResult.Message, StringComparison.Ordinal);
        Assert.False(diffResult.Succeeded);
        Assert.Equal(WorkspaceTextGuardFailure.ReadFailed, diffResult.Failure);
        Assert.DoesNotContain("Transient read failure", diffResult.Message, StringComparison.Ordinal);
        Assert.Equal(WorkspaceTextGuardFailure.Inaccessible, searchReadFailure);
        Assert.Equal(WorkspaceTextGuardFailure.Binary, binaryFailure);
    }

    [Fact]
    public void Text_guard_uses_one_bounded_stream_for_size_probe_binary_probe_and_decode()
    {
        var probeRoot = CreateDirectory("text-guard-bounded-stream");
        var probePath = Path.Combine(probeRoot, "growing.txt");
        File.WriteAllText(probePath, "small initial content");
        var oversizedContent = Enumerable.Repeat((byte)'a', (256 * 1024) + 1).ToArray();
        var oversizedGuard = new WorkspaceTextContentGuard(
            _ => new MemoryStream(oversizedContent, writable: false));
        var failingGuard = new WorkspaceTextContentGuard(
            _ => new FailAfterFirstReadStream("content that requires another read"u8.ToArray()));

        var oversizedRead = oversizedGuard.LoadForRead(
            probePath,
            "text-guard-bounded-stream/growing.txt",
            maxCharacters: 100);
        var failedRead = failingGuard.LoadForRead(
            probePath,
            "text-guard-bounded-stream/growing.txt",
            maxCharacters: 100);
        var failedDiff = failingGuard.LoadForDiff(
            probePath,
            "text-guard-bounded-stream/growing.txt");
        var failedSearch = failingGuard.TryLoadForSearch(
            probePath,
            "text-guard-bounded-stream/growing.txt",
            out _);

        Assert.False(oversizedRead.Succeeded);
        Assert.Equal(WorkspaceTextGuardFailure.TooLarge, oversizedRead.Failure);
        Assert.Contains("262144 bytes", oversizedRead.Message, StringComparison.Ordinal);
        Assert.False(failedRead.Succeeded);
        Assert.Equal(WorkspaceTextGuardFailure.ReadFailed, failedRead.Failure);
        Assert.DoesNotContain("Injected stream read failure", failedRead.Message, StringComparison.Ordinal);
        Assert.False(failedDiff.Succeeded);
        Assert.Equal(WorkspaceTextGuardFailure.ReadFailed, failedDiff.Failure);
        Assert.Equal(WorkspaceTextGuardFailure.Inaccessible, failedSearch);
    }

    [Fact]
    public void StatPath_missing_path_returns_failed_result_and_receipt()
    {
        var service = CreateService();

        var result = service.StatPath("missing/project.csproj");

        Assert.False(result.Succeeded);
        Assert.False(result.Exists);
        Assert.Equal("missing", result.PathKind);
        Assert.Equal("Failed", result.Receipt.Outcome);
        Assert.Contains("does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StatPath_existing_file_and_directory_remain_successful()
    {
        var directory = CreateDirectory("apps", "ExistingProject");
        WriteFile(directory, "ExistingProject.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var service = CreateService();

        var directoryResult = service.StatPath("apps/ExistingProject");
        var fileResult = service.StatPath("apps/ExistingProject/ExistingProject.csproj");

        Assert.True(directoryResult.Succeeded, directoryResult.Message);
        Assert.True(directoryResult.Exists);
        Assert.Equal("directory", directoryResult.PathKind);
        Assert.Equal("Succeeded", directoryResult.Receipt.Outcome);
        Assert.True(fileResult.Succeeded, fileResult.Message);
        Assert.True(fileResult.Exists);
        Assert.Equal("file", fileResult.PathKind);
        Assert.Equal("Succeeded", fileResult.Receipt.Outcome);
    }

    [Fact]
    public void ListFiles_rejects_regex_like_search_pattern_without_enumerating()
    {
        var projectMediaRoot = CreateDirectory("managed-files", "project-media", "files", "quote");
        WriteFile(projectMediaRoot, "xray.pdf", "%PDF");
        var service = CreateService();

        var result = service.ListFiles(
            "managed-files/project-media/files/quote",
            @".*xray.*\.pdf.*|.*xray.*.*\.pdf.*",
            20);

        Assert.False(result.Succeeded);
        Assert.Contains("glob syntax, not regex", result.Message, StringComparison.Ordinal);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void ListFiles_allows_common_glob_extension_pattern()
    {
        var projectMediaRoot = CreateDirectory("managed-files", "project-media", "files", "quote");
        WriteFile(projectMediaRoot, "xray.pdf", "%PDF");
        var service = CreateService();

        var result = service.ListFiles(
            "managed-files/project-media/files/quote",
            "*.*",
            20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Contains(result.Entries, item => string.Equals(
            item.RelativePath,
            "managed-files/project-media/files/quote/xray.pdf",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListFiles_reports_managed_project_media_alias_correction()
    {
        var service = CreateService();

        var result = service.ListFiles(
            "managed_files/project_media/files/quote",
            "*",
            20);

        Assert.False(result.Succeeded);
        Assert.Contains("managed-files/project-media/files/quote", result.Message, StringComparison.Ordinal);
        Assert.Contains("hyphenated segments", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ListFiles_does_not_report_alias_correction_for_unrelated_missing_path()
    {
        var service = CreateService();

        var result = service.ListFiles(
            "missing//path",
            "*",
            20);

        Assert.False(result.Succeeded);
        Assert.Contains("Workspace path 'missing//path' does not exist.", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hyphenated segments", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ListFiles_normalizes_embedded_trailing_globstar_path()
    {
        var projectMediaRoot = CreateDirectory("managed-files", "project-media", "files", "f28c07cd982c4d2dbcf23e60a32eca72");
        WriteFile(projectMediaRoot, "x-ray-machine-agent-quotation-list2018.pdf", "%PDF");
        WriteFile(projectMediaRoot, "converted", "x-ray-machine-agent-quotation-list2018.md", "# Quote");
        var service = CreateService();

        var result = service.ListFiles(
            "managed-files/project-media/files/f28c07cd982c4d2dbcf23e60a32eca72**",
            maxResults: 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("managed-files/project-media/files/f28c07cd982c4d2dbcf23e60a32eca72", result.RootPath);
        Assert.Equal("**/*", result.SearchPattern);
        Assert.Contains(result.Entries, item => string.Equals(
            item.RelativePath,
            "managed-files/project-media/files/f28c07cd982c4d2dbcf23e60a32eca72/x-ray-machine-agent-quotation-list2018.pdf",
            StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(
            item.RelativePath,
            "managed-files/project-media/files/f28c07cd982c4d2dbcf23e60a32eca72/converted/x-ray-machine-agent-quotation-list2018.md",
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ListFiles_supports_external_target_globstar_pattern()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var externalRoot = CreateExternalDirectory("RecipeCards");
        WriteFile(externalRoot, "RecipeCards.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        WriteFile(externalRoot, "Components", "Pages", "Index.razor", "<h1>Recipes</h1>");
        var service = CreateService();
        var alias = BuildExternalTargetAlias(externalRoot);

        var result = service.ListFiles(alias, "**/*", 20);

        Assert.True(result.Succeeded, result.Message);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, $"{alias}/RecipeCards.csproj", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, $"{alias}/Components/Pages/Index.razor", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }

            foreach (var externalRoot in externalRoots)
            {
                if (Directory.Exists(externalRoot))
                {
                    Directory.Delete(externalRoot, recursive: true);
                }
            }
        }
        catch
        {
        }
    }

    private WorkspaceFileQueryService CreateService(
        WorkspaceTextContentGuard? textContentGuard = null,
        Func<string, IReadOnlyList<string>>? enumerateDirectoryEntries = null)
    {
        Directory.CreateDirectory(workspaceRoot);
        var pathPolicy = new WorkspacePathPolicy(workspaceRoot);
        return new WorkspaceFileQueryService(
            pathPolicy,
            new WorkspaceFileReceiptWriter(workspaceRoot),
            textContentGuard ?? new WorkspaceTextContentGuard(),
            enumerateDirectoryEntries);
    }

    private string CreateDirectory(params string[] segments)
    {
        var path = Path.Combine(new[] { workspaceRoot }.Concat(segments).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateExternalDirectory(params string[] segments)
    {
        var root = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceFileQueryServiceExternal", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(new[] { root }.Concat(segments).ToArray());
        Directory.CreateDirectory(path);
        externalRoots.Add(root);
        return path;
    }

    private static void WriteFile(string rootPath, params string[] segmentsAndContent)
    {
        var content = segmentsAndContent[^1];
        var path = Path.Combine(new[] { rootPath }.Concat(segmentsAndContent[..^1]).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string BuildExternalTargetAlias(string fullPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var root = Path.GetPathRoot(normalizedFullPath)
            ?? throw new InvalidOperationException($"Could not resolve a drive root for '{fullPath}'.");
        var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmedRoot.Length != 2 || trimmedRoot[1] != ':')
        {
            throw new InvalidOperationException($"External-target alias tests require a drive-letter path. Received '{fullPath}'.");
        }

        var driveLetter = char.ToUpperInvariant(trimmedRoot[0]);
        var relativeWithinDrive = normalizedFullPath.Length <= root.Length
            ? string.Empty
            : normalizedFullPath[root.Length..]
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

        return string.IsNullOrWhiteSpace(relativeWithinDrive)
            ? $"external-target/{driveLetter}"
            : WorkspacePathPolicy.NormalizeRelativePath(Path.Combine("external-target", driveLetter.ToString(), relativeWithinDrive));
    }

    private sealed class FailAfterFirstReadStream(byte[] content) : MemoryStream(content, writable: false)
    {
        private int readCount;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (readCount++ > 0)
            {
                throw new IOException("Injected stream read failure.");
            }

            return base.Read(buffer, offset, Math.Min(count, 8));
        }
    }
}
