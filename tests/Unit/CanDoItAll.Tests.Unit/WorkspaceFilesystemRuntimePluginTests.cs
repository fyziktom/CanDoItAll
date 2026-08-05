using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceFilesystemRuntimePluginTests : IDisposable
{
    private readonly string workspaceRoot = Path.Combine(Path.GetTempPath(), "CanDoItAll.WorkspaceFilesystemRuntimePluginTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListWorkspaceDirectory_delegates_to_shallow_file_service_operation()
    {
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "docs", "nested"));
        File.WriteAllText(Path.Combine(workspaceRoot, "docs", "quote.txt"), "quote");
        File.WriteAllText(Path.Combine(workspaceRoot, "docs", "nested", "details.txt"), "details");
        var plugin = CreatePlugin(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment));

        var result = plugin.ListWorkspaceDirectory("docs");

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal("workspace_list_directory", result.Receipt.Operation);
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "docs/quote.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Entries, item => string.Equals(item.RelativePath, "docs/nested", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Entries, item => string.Equals(item.RelativePath, "docs/nested/details.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HashZipAndUnzip_are_available_without_WorkspaceRuntimePlugin()
    {
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "docs"));
        File.WriteAllText(Path.Combine(workspaceRoot, "docs", "quote.txt"), "quote");
        var plugin = CreatePlugin(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment));

        var hash = plugin.HashWorkspacePath("docs");
        var zip = plugin.ZipWorkspacePath("docs", "archives/docs.zip");
        var unzip = plugin.UnzipWorkspaceArchive("archives/docs.zip", "expanded");

        Assert.True(hash.Succeeded, hash.Message);
        Assert.Equal("workspace_hash_path", hash.Receipt.Operation);
        Assert.True(zip.Succeeded, zip.Message);
        Assert.Equal("workspace_zip_path", zip.Receipt.Operation);
        Assert.True(unzip.Succeeded, unzip.Message);
        Assert.Equal("workspace_unzip_archive", unzip.Receipt.Operation);
        Assert.True(File.Exists(Path.Combine(workspaceRoot, "expanded", "quote.txt")));
    }

    [Fact]
    public void Write_operations_fail_predictably_for_read_only_access()
    {
        Directory.CreateDirectory(workspaceRoot);
        var plugin = CreatePlugin(AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.ReadOnly));

        var exception = Assert.Throws<WorkspaceToolAccessDeniedException>(() => plugin.CreateWorkspaceDirectory("created"));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, result.ErrorCode);
        Assert.Contains("not allowed to write workspace files", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(exception.SafeMessage, result.Message);
        Assert.False(result.CanRetryWithCorrectedInput);
    }

    [Fact]
    public void External_access_denial_maps_normalized_alias_without_native_path()
    {
        const string nativePath = @"C:\operator-private\calculator\Calculator.csproj";
        const string normalizedAlias = "external-target/C/operator-private/calculator/Calculator.csproj";
        var access = new AgentWorkspaceToolAccessSettings
        {
            CanReadFiles = true
        };
        var plugin = CreatePlugin(access);

        var exception = Assert.Throws<WorkspaceToolAccessDeniedException>(() =>
            plugin.StatWorkspacePath(nativePath));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, result.ErrorCode);
        Assert.Equal(exception.SafeMessage, result.Message);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.Contains(normalizedAlias, result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nativePath, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Access_denial_exception_exposes_only_factories_and_normalizes_native_paths()
    {
        const string nativePath = @"C:\operator-private\calculator\Calculator.csproj";
        const string normalizedAlias = "external-target/C/operator-private/calculator/Calculator.csproj";
        var exceptionType = typeof(WorkspaceToolAccessDeniedException);

        Assert.Empty(exceptionType.GetConstructors());
        var constructor = Assert.Single(exceptionType.GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
        Assert.True(constructor.IsPrivate);

        WorkspaceToolAccessDeniedException[] exceptions =
        [
            WorkspaceToolAccessDeniedException.ExternalTargetReadOnly(nativePath),
            WorkspaceToolAccessDeniedException.ExternalTargetNotAuthorized(nativePath),
            WorkspaceToolAccessDeniedException.RecursiveDeleteReadOnlyAncestor(nativePath),
            WorkspaceToolAccessDeniedException.GroundedTargetRootDelete(nativePath),
            WorkspaceToolAccessDeniedException.ProtectedProductDirectoryDelete(nativePath),
            WorkspaceToolAccessDeniedException.ReadOnlyAncestorMutation(
                WorkspaceReadOnlyAncestorMutationOperation.Move,
                nativePath),
            WorkspaceToolAccessDeniedException.InaccessiblePath(nativePath)
        ];

        Assert.All(exceptions, exception =>
        {
            Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, exception.ErrorCode);
            Assert.Equal(exception.Message, exception.SafeMessage);
            Assert.Contains(normalizedAlias, exception.SafeMessage, StringComparison.Ordinal);
            Assert.DoesNotContain(nativePath, exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkspaceToolAccessDeniedException.ReadOnlyAncestorMutation(
                (WorkspaceReadOnlyAncestorMutationOperation)int.MaxValue,
                nativePath));

        const string secondNativePath = @"C:\operator-private\calculator-output";
        const string secondNormalizedAlias = "external-target/C/operator-private/calculator-output";
        var multiPathException = WorkspaceToolAccessDeniedException.InaccessiblePaths(
            nativePath,
            secondNativePath);
        Assert.Contains(normalizedAlias, multiPathException.SafeMessage, StringComparison.Ordinal);
        Assert.Contains(secondNormalizedAlias, multiPathException.SafeMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(nativePath, multiPathException.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secondNativePath, multiPathException.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filesystem_operations_map_unauthorized_access_to_safe_workspace_denial()
    {
        var plugin = CreatePlugin(
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            new UnauthorizedWorkspaceFileService());

        Action[] operations =
        [
            () => plugin.ListWorkspaceDirectory("calculator"),
            () => plugin.ListWorkspaceFiles("calculator", "**/*.csproj"),
            () => plugin.SearchWorkspace("Project Sdk", "calculator"),
            () => plugin.ReadWorkspaceTextFile("calculator/app.csproj"),
            () => plugin.StatWorkspacePath("calculator"),
            () => plugin.HashWorkspacePath("calculator"),
            () => plugin.CreateWorkspaceDirectory("calculator/generated"),
            () => plugin.WriteWorkspaceTextFile("calculator/app.csproj", "content"),
            () => plugin.AppendWorkspaceTextFile("calculator/app.csproj", "content"),
            () => plugin.CopyWorkspacePath("calculator", "calculator-copy"),
            () => plugin.MoveWorkspacePath("calculator", "calculator-moved"),
            () => plugin.DeleteWorkspacePath("calculator/app.csproj"),
            () => plugin.ZipWorkspacePath("calculator", "calculator.zip"),
            () => plugin.UnzipWorkspaceArchive("calculator.zip", "calculator-expanded"),
            () => plugin.DiffWorkspaceTextFiles("calculator/left.cs", "calculator/right.cs")
        ];
        var exceptions = operations
            .Select(operation => Assert.Throws<WorkspaceToolAccessDeniedException>(operation))
            .ToArray();

        Assert.All(exceptions, exception =>
        {
            Assert.True(MafAgentToolFailureMapper.TryMap(exception, out var result));
            Assert.Equal(WorkspaceToolAccessDeniedException.FailureCode, result.ErrorCode);
            Assert.Contains("calculator", result.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(UnauthorizedWorkspaceFileService.SensitiveFailureMessage, result.Message, StringComparison.Ordinal);
            Assert.True(result.CanRetryWithCorrectedInput);
        });
    }

    [Theory]
    [InlineData(FilesystemPathFailureOperation.SinglePath, "directory")]
    [InlineData(FilesystemPathFailureOperation.MultiPath, "directory")]
    [InlineData(FilesystemPathFailureOperation.ManagedAliasMismatch, "managed workspace path")]
    public void Filesystem_operations_map_typed_path_failures_to_safe_retryable_input(
        FilesystemPathFailureOperation operation,
        string expectedMessageFragment)
    {
        var sensitiveDiagnostic = $"Path failure at '{workspaceRoot}'.";
        var failure = operation switch
        {
            FilesystemPathFailureOperation.SinglePath or
            FilesystemPathFailureOperation.MultiPath =>
                WorkspacePathResolutionException.FileRequired(sensitiveDiagnostic),
            FilesystemPathFailureOperation.ManagedAliasMismatch =>
                WorkspacePathResolutionException.ManagedPathAliasMismatch(sensitiveDiagnostic),
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unknown filesystem path failure operation.")
        };
        var plugin = CreatePlugin(
            AgentWorkspaceToolAccessProfiles.CreateSettings(
                AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            new RecordingReadWorkspaceFileService(failure));

        var exception = Assert.Throws<AgentToolInputValidationException>(() =>
            InvokePathFailure(plugin, operation));
        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.Equal(AgentToolInputValidationException.FailureCode, result.ErrorCode);
        Assert.Contains(expectedMessageFragment, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("corrected workspace path", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(workspaceRoot, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(UnexpectedFilesystemFailureKind.InvalidOperation)]
    [InlineData(UnexpectedFilesystemFailureKind.Io)]
    public void Filesystem_execution_helpers_leave_untyped_failures_opaque(
        UnexpectedFilesystemFailureKind failureKind)
    {
        Exception expected = failureKind switch
        {
            UnexpectedFilesystemFailureKind.InvalidOperation =>
                new InvalidOperationException($"Unexpected operation failure at '{workspaceRoot}'."),
            UnexpectedFilesystemFailureKind.Io =>
                new IOException($"Unexpected I/O failure at '{workspaceRoot}'."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Unknown unexpected filesystem failure kind.")
        };
        var plugin = CreatePlugin(
            AgentWorkspaceToolAccessProfiles.CreateSettings(
                AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            new RecordingReadWorkspaceFileService(expected));

        var singlePathException = Record.Exception(() =>
            plugin.ReadWorkspaceTextFile("source.txt"));
        var multiPathException = Record.Exception(() =>
            plugin.CopyWorkspacePath("source.txt", "destination.txt"));

        Assert.Same(expected, singlePathException);
        Assert.Same(expected, multiPathException);
        Assert.False(MafAgentToolFailureMapper.TryMap(singlePathException!, out _));
        Assert.False(MafAgentToolFailureMapper.TryMap(multiPathException!, out _));
    }

    [Fact]
    public void Write_passes_most_specific_writable_external_authority_to_file_service()
    {
        var configuredRoot = $"external-target/C/candoitall-tests/{Guid.NewGuid():N}/product";
        var nestedAuthorityRoot = $"{configuredRoot}/calculator";
        var targetPath = $"{nestedAuthorityRoot}/src/Feature.cs";
        var access = new AgentWorkspaceToolAccessSettings
        {
            CanReadFiles = true,
            CanWriteFiles = true,
            AllowedExternalTargetAliases = [configuredRoot, nestedAuthorityRoot]
        };
        var fileService = new RecordingReadWorkspaceFileService();
        var plugin = CreatePlugin(access, fileService);

        var result = plugin.WriteWorkspaceTextFile(targetPath, "internal sealed class Feature {}");

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(targetPath, fileService.LastWritePath);
        Assert.Equal(nestedAuthorityRoot, fileService.LastWriteAuthorityRoot);
    }

    [Fact]
    public void Append_passes_most_specific_writable_external_authority_to_file_service()
    {
        var configuredRoot = $"external-target/C/candoitall-tests/{Guid.NewGuid():N}/product";
        var nestedAuthorityRoot = $"{configuredRoot}/calculator";
        var targetPath = $"{nestedAuthorityRoot}/src/Feature.cs";
        var access = new AgentWorkspaceToolAccessSettings
        {
            CanReadFiles = true,
            CanWriteFiles = true,
            AllowedExternalTargetAliases = [configuredRoot, nestedAuthorityRoot]
        };
        var fileService = new RecordingReadWorkspaceFileService();
        var plugin = CreatePlugin(access, fileService);

        var result = plugin.AppendWorkspaceTextFile(targetPath, "// appended");

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(targetPath, fileService.LastAppendPath);
        Assert.Equal(nestedAuthorityRoot, fileService.LastAppendAuthorityRoot);
    }

    [Fact]
    public void Multi_path_operations_map_unauthorized_access_without_guessing_the_denied_side()
    {
        var plugin = CreatePlugin(
            AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            new UnauthorizedWorkspaceFileService());
        (Action Invoke, string FirstPath, string SecondPath)[] operations =
        [
            (() => plugin.CopyWorkspacePath("copy-source", "copy-destination"), "copy-source", "copy-destination"),
            (() => plugin.MoveWorkspacePath("move-source", "move-destination"), "move-source", "move-destination"),
            (() => plugin.ZipWorkspacePath("zip-source", "zip-destination.zip"), "zip-source", "zip-destination.zip"),
            (() => plugin.UnzipWorkspaceArchive("unzip-source.zip", "unzip-destination"), "unzip-source.zip", "unzip-destination"),
            (() => plugin.DiffWorkspaceTextFiles("diff-left", "diff-right"), "diff-left", "diff-right")
        ];

        foreach (var operation in operations)
        {
            var exception = Assert.Throws<WorkspaceToolAccessDeniedException>(operation.Invoke);

            Assert.Contains(operation.FirstPath, exception.SafeMessage, StringComparison.Ordinal);
            Assert.Contains(operation.SecondPath, exception.SafeMessage, StringComparison.Ordinal);
            Assert.DoesNotContain(
                UnauthorizedWorkspaceFileService.SensitiveFailureMessage,
                exception.SafeMessage,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Tree_mutations_deny_writable_ancestor_of_invocation_read_only_target()
    {
        var configuredRoot = $"external-target/C/candoitall-tests/{Guid.NewGuid():N}/product";
        var mutationAncestor = $"{configuredRoot}/packages";
        var readOnlyTarget = $"{mutationAncestor}/Inventory";
        var access = new AgentWorkspaceToolAccessSettings
        {
            CanReadFiles = true,
            CanWriteFiles = true,
            AllowedExternalTargetAliases = [configuredRoot]
        };
        var plugin = CreatePlugin(access);
        using var auditScope = WorkspaceExecutionAuditContext.BeginScope(CreateExecutionRun(readOnlyTarget));

        var deleteException = Assert.Throws<WorkspaceToolAccessDeniedException>(() =>
            plugin.DeleteWorkspacePath(mutationAncestor, recursive: true));
        var replaceException = Assert.Throws<WorkspaceToolAccessDeniedException>(() =>
            plugin.CopyWorkspacePath("staging/package", mutationAncestor, overwrite: true));
        var moveException = Assert.Throws<WorkspaceToolAccessDeniedException>(() =>
            plugin.MoveWorkspacePath(mutationAncestor, "staging/moved-package", overwrite: true));

        Assert.Contains("ancestor of a read-only external target", deleteException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ancestor of a read-only external target", replaceException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ancestor of a read-only external target", moveException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Selected_project_root_attachment_flows_through_invocation_audit_to_recursive_reads_only()
    {
        const string nativeRoot =
            @"C:\programovani\dotnet\calculator-e2e-test";
        const string rootAlias =
            "external-target/C/programovani/dotnet/calculator-e2e-test";
        const string projectAlias =
            "external-target/C/programovani/dotnet/calculator-e2e-test/src/Calculator/Calculator.csproj";
        const string siblingAlias =
            "external-target/C/programovani/dotnet/unselected-app";
        var capturedAtUtc = new DateTimeOffset(
            2026,
            8,
            1,
            18,
            0,
            0,
            TimeSpan.Zero);
        var attachmentDraft = Assert.IsType<AgentChatContextAttachmentDraft>(
            AgentChatExternalTargetAccessAttachmentFactory.CreateReadOnlyDraft(
                [nativeRoot],
                new DatabaseProfileGeneration(7),
                capturedAtUtc,
                capturedAtUtc.AddMinutes(5)));
        var context = CreateProjectStructureContext(
            attachmentDraft,
            capturedAtUtc);
        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            Guid.NewGuid(),
            chatSessionId: null,
            "Inspect the selected calculator recursively.",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(7),
            capturedAtUtc.AddMinutes(1));
        var invocationContext = Assert.IsType<ExecutionInvocationContext>(
            invocation.Options.Context);
        var fileService = new RecordingReadWorkspaceFileService();
        var plugin = CreatePlugin(
            new AgentWorkspaceToolAccessSettings
            {
                CanReadFiles = true
            },
            fileService);
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRunWithMetadata(invocationContext.MetadataJson));

        var recursiveList = plugin.ListWorkspaceFiles(
            rootAlias,
            "**/*.csproj");
        var projectRead = plugin.ReadWorkspaceTextFile(projectAlias);
        var denial = Assert.Throws<WorkspaceToolAccessDeniedException>(() =>
            plugin.ListWorkspaceFiles(siblingAlias, "**/*"));

        Assert.True(recursiveList.Succeeded, recursiveList.Message);
        Assert.True(projectRead.Succeeded, projectRead.Message);
        Assert.Equal(rootAlias, fileService.LastListRoot);
        Assert.Equal("**/*.csproj", fileService.LastSearchPattern);
        Assert.Equal(projectAlias, fileService.LastReadPath);
        Assert.Contains(
            "not in this run's allowed external workspace roots",
            denial.SafeMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
        catch
        {
        }
    }

    private WorkspaceFilesystemRuntimePlugin CreatePlugin(
        AgentWorkspaceToolAccessSettings access,
        IWorkspaceFileService? fileService = null)
    {
        Directory.CreateDirectory(workspaceRoot);
        return new WorkspaceFilesystemRuntimePlugin(
            fileService ?? new WorkspaceFileService(workspaceRoot),
            workspaceRoot,
            WorkspaceScopeDescriptor.Sandbox,
            access);
    }

    private static void InvokePathFailure(
        WorkspaceFilesystemRuntimePlugin plugin,
        FilesystemPathFailureOperation operation)
    {
        switch (operation)
        {
            case FilesystemPathFailureOperation.SinglePath:
                plugin.ReadWorkspaceTextFile("source.txt");
                return;
            case FilesystemPathFailureOperation.MultiPath:
                plugin.CopyWorkspacePath("source.txt", "destination.txt");
                return;
            case FilesystemPathFailureOperation.ManagedAliasMismatch:
                plugin.ReadWorkspaceTextFile("managed-files/missing.txt");
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "Unknown filesystem path failure operation.");
        }
    }

    private sealed class UnauthorizedWorkspaceFileService : IWorkspaceFileService
    {
        public const string SensitiveFailureMessage = @"Access denied to C:\operator-private\secret";

        public WorkspaceFileListResult ListDirectory(string? relativePath = null, int maxResults = 100)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileListResult ListFiles(string? relativePath = null, string searchPattern = "*", int maxResults = 100)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceTextFileReadResult ReadTextFile(string path, int maxCharacters = 12000)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspacePathStatResult StatPath(string path)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult CreateDirectory(string path)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite, string authorityRootPath)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult AppendTextFile(string path, string content)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult AppendTextFile(string path, string content, string authorityRootPath)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite, string destinationAuthorityRootPath)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite, string destinationAuthorityRootPath)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceArchiveMutationResult ZipPath(
            string sourcePath,
            string destinationPath,
            bool overwrite = false,
            int maxFiles = 200,
            long maxBytes = 10485760)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceArchiveMutationResult UnzipArchive(
            string sourcePath,
            string destinationPath,
            bool overwrite = false,
            int maxFiles = 200,
            long maxBytes = 10485760)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceArchiveMutationResult UnzipArchive(
            string sourcePath,
            string destinationPath,
            bool overwrite,
            int maxFiles,
            long maxBytes,
            string destinationAuthorityRootPath)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);

        public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160)
            => throw new UnauthorizedAccessException(SensitiveFailureMessage);
    }

    private sealed class RecordingReadWorkspaceFileService(Exception? exception = null)
        : IWorkspaceFileService
    {
        public string LastListRoot { get; private set; } = string.Empty;

        public string LastSearchPattern { get; private set; } = string.Empty;

        public string LastReadPath { get; private set; } = string.Empty;

        public string LastWritePath { get; private set; } = string.Empty;

        public string LastWriteAuthorityRoot { get; private set; } = string.Empty;

        public string LastAppendPath { get; private set; } = string.Empty;

        public string LastAppendAuthorityRoot { get; private set; } = string.Empty;

        public WorkspaceFileListResult ListFiles(
            string? relativePath = null,
            string searchPattern = "*",
            int maxResults = 100)
        {
            LastListRoot = relativePath ?? string.Empty;
            LastSearchPattern = searchPattern;
            return new WorkspaceFileListResult(
                true,
                "Recursive project discovery succeeded.",
                CreateReceipt("workspace_list_files"),
                LastListRoot,
                searchPattern,
                [
                    new WorkspaceFileListEntry(
                        LastListRoot + "/src/Calculator/Calculator.csproj",
                        "file",
                        128,
                        DateTimeOffset.UtcNow)
                ],
                IsTruncated: false);
        }

        public WorkspaceTextFileReadResult ReadTextFile(
            string path,
            int maxCharacters = 12000)
        {
            if (exception is not null)
            {
                throw exception;
            }

            LastReadPath = path;
            const string content = "<Project Sdk=\"Microsoft.NET.Sdk\" />";
            return new WorkspaceTextFileReadResult(
                true,
                "Project file read succeeded.",
                CreateReceipt("workspace_read_file"),
                path,
                content,
                content.Length,
                IsTruncated: false);
        }

        public WorkspaceFileListResult ListDirectory(string? relativePath = null, int maxResults = 100)
            => throw new NotSupportedException();

        public WorkspaceTextSearchResult SearchText(string query, string? relativePath = null, int maxResults = 20)
            => throw new NotSupportedException();

        public WorkspacePathStatResult StatPath(string path)
            => throw new NotSupportedException();

        public WorkspacePathHashResult HashPath(string path, int maxFiles = 200, long maxBytes = 10485760)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult CreateDirectory(string path)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult WriteTextFile(string path, string content, bool overwrite = true)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult WriteTextFile(
            string path,
            string content,
            bool overwrite,
            string authorityRootPath)
        {
            LastWritePath = path;
            LastWriteAuthorityRoot = authorityRootPath;
            return new WorkspaceFileMutationResult(
                true,
                "Write succeeded.",
                CreateReceipt("workspace_write_file"),
                path,
                DestinationPath: null,
                PathKind: "file",
                PathExistedBefore: false,
                CreatedNewPath: true,
                OverwroteExistingPath: false,
                CharacterCount: content.Length);
        }

        public WorkspaceFileMutationResult AppendTextFile(string path, string content)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult AppendTextFile(string path, string content, string authorityRootPath)
        {
            LastAppendPath = path;
            LastAppendAuthorityRoot = authorityRootPath;
            return new WorkspaceFileMutationResult(
                true,
                "Append succeeded.",
                CreateReceipt("workspace_append_file"),
                path,
                DestinationPath: null,
                PathKind: "file",
                PathExistedBefore: true,
                CreatedNewPath: false,
                OverwroteExistingPath: false,
                CharacterCount: content.Length);
        }

        public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite = false)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult CopyPath(string sourcePath, string destinationPath, bool overwrite, string destinationAuthorityRootPath)
            => exception is not null
                ? throw exception
                : throw new NotSupportedException();

        public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite = false)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult MovePath(string sourcePath, string destinationPath, bool overwrite, string destinationAuthorityRootPath)
            => throw new NotSupportedException();

        public WorkspaceFileMutationResult DeletePath(string path, bool recursive = false)
            => throw new NotSupportedException();

        public WorkspaceArchiveMutationResult ZipPath(
            string sourcePath,
            string destinationPath,
            bool overwrite = false,
            int maxFiles = 200,
            long maxBytes = 10485760)
            => throw new NotSupportedException();

        public WorkspaceArchiveMutationResult UnzipArchive(
            string sourcePath,
            string destinationPath,
            bool overwrite = false,
            int maxFiles = 200,
            long maxBytes = 10485760)
            => throw new NotSupportedException();

        public WorkspaceArchiveMutationResult UnzipArchive(
            string sourcePath,
            string destinationPath,
            bool overwrite,
            int maxFiles,
            long maxBytes,
            string destinationAuthorityRootPath)
            => throw new NotSupportedException();

        public WorkspaceTextDiffResult DiffTextFiles(string leftPath, string rightPath, int maxLines = 160)
            => throw new NotSupportedException();

        private static WorkspaceToolReceipt CreateReceipt(string operation)
        {
            var now = DateTimeOffset.UtcNow;
            return new WorkspaceToolReceipt(
                operation,
                MutatesWorkspace: false,
                Boundary: "external-target",
                Outcome: "Succeeded",
                Message: "Succeeded.",
                ReceiptRelativePath: string.Empty,
                TargetPaths: [],
                ArtifactReferences: [],
                StartedAtUtc: now,
                CompletedAtUtc: now);
        }
    }

    private static ExecutionRunRecord CreateExecutionRun(string readOnlyExternalTargetAlias)
    {
        var now = DateTimeOffset.UtcNow;
        var metadataJson = System.Text.Json.JsonSerializer.Serialize(
            new Dictionary<string, string[]>
            {
                [ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] =
                [
                    readOnlyExternalTargetAlias
                ]
            });
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "External target guard test",
            SourceKind: "test",
            SourceId: "workspace-filesystem-runtime-plugin",
            CorrelationId: Guid.NewGuid().ToString("D"),
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static ExecutionRunRecord CreateExecutionRunWithMetadata(
        string metadataJson)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Selected project authority roundtrip test",
            SourceKind: "project-structure",
            SourceId: Guid.NewGuid().ToString("D"),
            CorrelationId: Guid.NewGuid().ToString("D"),
            CausationId: string.Empty,
            RequestedBy: AgentChatContextInvocationFactory.Requester,
            RequestedByKind: AgentChatContextInvocationFactory.RequesterKind,
            MetadataJson: metadataJson,
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static AgentChatContextSnapshot CreateProjectStructureContext(
        AgentChatContextAttachmentDraft attachmentDraft,
        DateTimeOffset capturedAtUtc)
    {
        var projectId = Guid.NewGuid();
        var publication = new AgentChatContextPublication(
            new AgentChatContextScope(
                AgentChatContextScopeId.Create(),
                new AgentChatContextSource(
                    new AgentChatContextSourceKind(
                        AgentChatExternalTargetAccessAttachmentFactory.TrustedSourceKindValue),
                    new AgentChatContextSourceId(projectId.ToString("D"))),
                "Selected calculator runtime",
                WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
                accessMode: AgentChatContextScopeAccessMode.Unrestricted),
            [
                new AgentChatContextContributorPublication(
                    new AgentChatContextFragment(
                        new AgentChatContextContributorId(
                            AgentChatExternalTargetAccessAttachmentFactory.TrustedContributorIdValue),
                        0,
                        "Selected node: Start Calculator"),
                    [attachmentDraft])
            ]);
        var registry = new AgentChatContextRegistry(
            new FixedTimeProvider(capturedAtUtc));
        using var lease = registry.PublishModuleContext(publication);
        return Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
            => utcNow;
    }

    public enum FilesystemPathFailureOperation
    {
        SinglePath,
        MultiPath,
        ManagedAliasMismatch
    }

    public enum UnexpectedFilesystemFailureKind
    {
        InvalidOperation,
        Io
    }
}
