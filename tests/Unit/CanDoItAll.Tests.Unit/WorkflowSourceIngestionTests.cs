using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowSourceIngestionTests
{
    private static readonly WorkflowValueShape JsonShape = new(
        WorkflowValueShapeKind.Json,
        "{}",
        "JSON");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData(".pdf", "markitdown-pdf")]
    [InlineData(".docx", "markitdown-docx")]
    [InlineData(".html", "markitdown-html")]
    [InlineData(".htm", "markitdown-html")]
    [InlineData(".xlsx", "markitdown-xlsx")]
    public async Task DocumentExtensionsDelegateToSharedConverter(
        string extension,
        string expectedStatus)
    {
        using var temp = new TempDirectory();
        var relativePath = $"sources/evidence{extension}";
        temp.Write(relativePath, "fixture");
        var converter = new RecordingDocumentMarkdownConverter
        {
            Markdown = $"converted {extension} evidence"
        };

        using var result = await ExecuteAsync(
            temp.Path,
            converter,
            CreateSettings(extension) with
            {
                MaxCharactersPerFile = 2345,
                MaxTotalCharacters = 9000
            },
            CreateSourcesPayload(("evidence", relativePath)));

        var request = Assert.Single(converter.Requests);
        Assert.Equal(Path.Combine(temp.Path, "sources", $"evidence{extension}"), request.SourcePath);
        Assert.Equal(2345, request.MaxCharacters);
        var source = Assert.Single(result.RootElement.GetProperty("sourceDocuments").EnumerateArray());
        Assert.Equal(converter.Markdown, source.GetProperty("text").GetString());
        Assert.Equal(expectedStatus, source.GetProperty("extractionStatus").GetString());
    }

    [Fact]
    public void CandidateCollectorAppliesEnabledKeyAndEmbeddedPathRules()
    {
        using var payload = JsonDocument.Parse("""
            {
              "sources": [
                { "key": "included", "label": "Included", "kind": "filePath", "value": "sources/include.txt", "isEnabled": true },
                { "key": "disabled", "label": "Disabled", "kind": "filePath", "value": "sources/disabled.txt", "isEnabled": false },
                { "key": "filtered", "label": "Filtered", "kind": "filePath", "value": "sources/filtered.txt", "isEnabled": true }
              ],
              "selectedNodes": [
                { "id": "selected", "title": "Selected", "notes": "Evidence path: C:\\evidence\\selected.pdf" }
              ]
            }
            """);
        var settings = CreateSettings(".txt", ".pdf") with
        {
            IncludeSelectedNodePaths = true
        };
        IReadOnlySet<string> keys = new HashSet<string>(["included", "selected"], StringComparer.OrdinalIgnoreCase);
        var collector = new WorkflowSourceCandidateCollector();

        var candidates = collector.Collect(payload.RootElement, settings, keys);

        Assert.Collection(
            candidates,
            candidate =>
            {
                Assert.Equal("included", candidate.Key);
                Assert.Equal("sources/include.txt", candidate.Value);
                Assert.Equal("additional-source", candidate.Origin);
            },
            candidate =>
            {
                Assert.Equal("selected", candidate.Key);
                Assert.Equal(@"C:\evidence\selected.pdf", candidate.Value);
                Assert.Equal("filePath", candidate.Kind);
                Assert.Equal("selected-node", candidate.Origin);
            });
    }

    [Fact]
    public void CandidateCollectorUsesSyntaxWithoutFilesystemProbing()
    {
        using var temp = new TempDirectory();
        var existingDottedDirectory = temp.FullPath("existing.data");
        Directory.CreateDirectory(existingDottedDirectory);
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            selectedNodes = new[]
            {
                new
                {
                    id = "dotted",
                    title = "Dotted directory",
                    mediaRelativePath = existingDottedDirectory
                },
                new
                {
                    id = "folder",
                    title = "Syntactic folder",
                    mediaRelativePath = "not-created-folder/"
                }
            }
        }, JsonOptions));
        var collector = new WorkflowSourceCandidateCollector();
        var settings = CreateSettings(".data") with
        {
            IncludeSelectedNodePaths = true
        };

        var candidates = collector.Collect(payload.RootElement, settings, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("filePath", Assert.Single(candidates, candidate => candidate.Key == "dotted").Kind);
        Assert.Equal("folderPath", Assert.Single(candidates, candidate => candidate.Key == "folder").Kind);
    }

    [Fact]
    public void FileResolverEnforcesExtensionAndAbsolutePathPolicy()
    {
        using var workspace = new TempDirectory();
        using var external = new TempDirectory();
        workspace.Write("sources/evidence.txt", "workspace");
        external.Write("outside.txt", "external");
        var resolver = new WorkflowSourceFileResolver(new WorkspacePathResolutionService(workspace.Path));
        var relativeCandidate = new WorkflowSourceCandidate(
            "relative",
            "Relative",
            "filePath",
            "sources/evidence.txt",
            "test");
        var absoluteCandidate = relativeCandidate with
        {
            Key = "absolute",
            Value = external.FullPath("outside.txt")
        };
        var settings = CreateSettings(".txt");

        var extensionException = Assert.Throws<InvalidOperationException>(() => resolver.ResolveCandidateFiles(
            relativeCandidate,
            settings,
            new HashSet<string>([".md"], StringComparer.OrdinalIgnoreCase),
            take: 1).ToArray());
        Assert.Contains("not allowed", extensionException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => resolver.ResolveCandidateFiles(
            absoluteCandidate,
            settings,
            new HashSet<string>([".txt"], StringComparer.OrdinalIgnoreCase),
            take: 1).ToArray());

        var resolved = Assert.Single(resolver.ResolveCandidateFiles(
            absoluteCandidate,
            settings with
            {
                AllowAbsoluteInputPaths = true
            },
            new HashSet<string>([".txt"], StringComparer.OrdinalIgnoreCase),
            take: 1));
        Assert.Equal(Path.GetFullPath(external.FullPath("outside.txt")), resolved.FullPath);
        Assert.False(string.IsNullOrWhiteSpace(resolved.DisplayPath));
    }

    [Theory]
    [InlineData(11, 11, false, 10)]
    [InlineData(5, 4, false, 10)]
    [InlineData(5, 8, false, 10)]
    [InlineData(5, 5, true, 10)]
    public async Task DocumentReaderRejectsConverterContractViolations(
        int markdownLength,
        int totalCharacters,
        bool isTruncated,
        int requestedMaximum)
    {
        var converter = new RecordingDocumentMarkdownConverter
        {
            Handler = (request, _) => Task.FromResult(new WorkspaceDocumentMarkdownConversionResult(
                true,
                "converted",
                request.SourcePath,
                new string('x', markdownLength),
                totalCharacters,
                isTruncated,
                string.Empty))
        };
        var reader = new WorkflowSourceDocumentReader(converter);
        var file = new WorkflowSourceIngestionFile(
            Path.GetFullPath("adversarial.html"),
            "adversarial.html",
            "adversarial.html");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync(
            file,
            requestedMaximum,
            CancellationToken.None));

        Assert.Contains("violated its result contract", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConversionFailureBecomesSourceErrorWithoutRawFallback()
    {
        using var temp = new TempDirectory();
        temp.Write("sources/failure.html", "raw fallback must not leak");
        var converter = new RecordingDocumentMarkdownConverter
        {
            Succeeded = false,
            Message = "synthetic conversion failure",
            Markdown = string.Empty
        };

        using var result = await ExecuteAsync(
            temp.Path,
            converter,
            CreateSettings(".html"),
            CreateSourcesPayload(("failure", "sources/failure.html")));

        Assert.Equal(0, result.RootElement.GetProperty("loadedSourceCount").GetInt32());
        Assert.Equal(1, result.RootElement.GetProperty("failedSourceCount").GetInt32());
        Assert.Empty(result.RootElement.GetProperty("sourceDocuments").EnumerateArray());
        var error = Assert.Single(result.RootElement.GetProperty("sourceErrors").EnumerateArray());
        Assert.Contains("synthetic conversion failure", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("raw fallback must not leak", result.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConverterReceivesEffectiveRemainingCharacterBudget()
    {
        using var temp = new TempDirectory();
        temp.Write("sources/first.txt", new string('a', 750));
        temp.Write("sources/second.html", "fixture");
        var converter = new RecordingDocumentMarkdownConverter
        {
            Markdown = new string('b', 1200)
        };

        using var result = await ExecuteAsync(
            temp.Path,
            converter,
            CreateSettings(".txt", ".html") with
            {
                MaxFiles = 2,
                MaxCharactersPerFile = 1200,
                MaxTotalCharacters = 1000
            },
            CreateSourcesPayload(
                ("first", "sources/first.txt"),
                ("second", "sources/second.html")));

        var request = Assert.Single(converter.Requests);
        Assert.Equal(250, request.MaxCharacters);
        var documents = result.RootElement.GetProperty("sourceDocuments").EnumerateArray().ToArray();
        Assert.Equal(2, documents.Length);
        Assert.Equal(250, documents[1].GetProperty("text").GetString()!.Length);
        Assert.True(documents[1].GetProperty("isTruncated").GetBoolean());
        Assert.True(result.RootElement.GetProperty("isTruncated").GetBoolean());
    }

    [Fact]
    public async Task PlainTextBypassesDocumentConverter()
    {
        using var temp = new TempDirectory();
        temp.Write("sources/note.txt", "plain source evidence");
        var converter = new RecordingDocumentMarkdownConverter();

        using var result = await ExecuteAsync(
            temp.Path,
            converter,
            CreateSettings(".txt"),
            CreateSourcesPayload(("note", "sources/note.txt")));

        Assert.Empty(converter.Requests);
        var source = Assert.Single(result.RootElement.GetProperty("sourceDocuments").EnumerateArray());
        Assert.Equal("plain source evidence", source.GetProperty("text").GetString());
        Assert.Equal("text", source.GetProperty("extractionStatus").GetString());
    }

    [Fact]
    public async Task ZipUsesBoundedManifestWithoutConvertingEntryContent()
    {
        using var temp = new TempDirectory();
        var archivePath = temp.FullPath("sources/archive.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("nested/evidence.txt");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("entry body must not be ingested");
        }

        var converter = new RecordingDocumentMarkdownConverter();
        using var result = await ExecuteAsync(
            temp.Path,
            converter,
            CreateSettings(".zip"),
            CreateSourcesPayload(("archive", "sources/archive.zip")));

        Assert.Empty(converter.Requests);
        var source = Assert.Single(result.RootElement.GetProperty("sourceDocuments").EnumerateArray());
        Assert.Contains("nested/evidence.txt", source.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("entry body must not be ingested", source.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.Equal("zip-manifest", source.GetProperty("extractionStatus").GetString());
    }

    [Fact]
    public async Task LegacyXlsDoesNotFallThroughToDocumentConverter()
    {
        using var temp = new TempDirectory();
        temp.WriteBytes("sources/legacy.xls", [0x01, 0x02, 0x03, 0x04]);
        var converter = new RecordingDocumentMarkdownConverter();

        using var result = await ExecuteAsync(
            temp.Path,
            converter,
            CreateSettings(".xls"),
            CreateSourcesPayload(("legacy", "sources/legacy.xls")));

        Assert.Empty(converter.Requests);
        Assert.Equal(0, result.RootElement.GetProperty("loadedSourceCount").GetInt32());
        Assert.Equal(1, result.RootElement.GetProperty("failedSourceCount").GetInt32());
        var error = Assert.Single(result.RootElement.GetProperty("sourceErrors").EnumerateArray());
        Assert.Contains("Legacy XLS extraction failed", error.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateCandidatesLoadFileOnce()
    {
        using var temp = new TempDirectory();
        temp.Write("sources/duplicate.txt", "one document");
        var converter = new RecordingDocumentMarkdownConverter();

        using var result = await ExecuteAsync(
            temp.Path,
            converter,
            CreateSettings(".txt"),
            CreateSourcesPayload(
                ("first", "sources/duplicate.txt"),
                ("second", "sources/duplicate.txt")));

        Assert.Equal(1, result.RootElement.GetProperty("loadedSourceCount").GetInt32());
        Assert.Single(result.RootElement.GetProperty("sourceDocuments").EnumerateArray());
    }

    [Fact]
    public async Task AbsolutePathRequiresExplicitOptIn()
    {
        using var workspace = new TempDirectory();
        using var external = new TempDirectory();
        external.Write("outside.txt", "external evidence");
        var absolutePath = external.FullPath("outside.txt");
        var converter = new RecordingDocumentMarkdownConverter();
        var payload = CreateSourcesPayload(("outside", absolutePath));

        using var denied = await ExecuteAsync(
            workspace.Path,
            converter,
            CreateSettings(".txt"),
            payload);
        using var allowed = await ExecuteAsync(
            workspace.Path,
            converter,
            CreateSettings(".txt") with
            {
                AllowAbsoluteInputPaths = true
            },
            payload);

        Assert.Equal(1, denied.RootElement.GetProperty("failedSourceCount").GetInt32());
        Assert.Equal(0, denied.RootElement.GetProperty("loadedSourceCount").GetInt32());
        Assert.Equal(1, allowed.RootElement.GetProperty("loadedSourceCount").GetInt32());
    }

    [Fact]
    public async Task CancellationFlowsToDocumentConverter()
    {
        using var temp = new TempDirectory();
        temp.Write("sources/waiting.pdf", "fixture");
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var converter = new RecordingDocumentMarkdownConverter
        {
            Handler = async (request, cancellationToken) =>
            {
                observed.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return RecordingDocumentMarkdownConverter.Success(request, string.Empty);
            }
        };
        var executor = CreateExecutor(temp.Path, converter);
        using var cancellationSource = new CancellationTokenSource();

        var execution = ExecuteAsync(
            executor,
            CreateSettings(".pdf"),
            CreateSourcesPayload(("waiting", "sources/waiting.pdf")),
            cancellationSource.Token);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        Assert.Equal(cancellationSource.Token, converter.ObservedCancellationToken);
    }

    private static SourceIngestionWorkflowExecutor CreateExecutor(
        string workspaceRoot,
        IWorkspaceDocumentMarkdownConverter converter)
        => new(new WorkspacePathResolutionService(workspaceRoot), converter);

    private static async Task<JsonDocument> ExecuteAsync(
        string workspaceRoot,
        IWorkspaceDocumentMarkdownConverter converter,
        WorkflowSourceIngestionExecutorSettings settings,
        string payloadJson)
        => JsonDocument.Parse((await ExecuteAsync(
            CreateExecutor(workspaceRoot, converter),
            settings,
            payloadJson,
            CancellationToken.None)).PayloadJson);

    private static async Task<WorkflowNodeExecutionResult> ExecuteAsync(
        SourceIngestionWorkflowExecutor executor,
        WorkflowSourceIngestionExecutorSettings settings,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var settingsJson = JsonSerializer.Serialize(settings, JsonOptions);
        var node = new WorkflowNode(
            new WorkflowNodeId("ingest"),
            WorkflowNodeKind.Executor,
            "Ingest",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: JsonShape,
                ResultShape: JsonShape)
            {
                ExecutorId = executor.Descriptor.Id,
                ExecutorSettingsJson = settingsJson,
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
        var definition = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Source ingestion test",
            string.Empty,
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var context = new WorkflowExecutorExecutionContext(
            definition,
            node,
            executor.Descriptor,
            settingsJson,
            WorkflowExecutorExecutionPolicy.Default);

        return await executor.ExecuteAsync(
            context,
            new WorkflowNodeInput(payloadJson),
            cancellationToken);
    }

    private static WorkflowSourceIngestionExecutorSettings CreateSettings(params string[] extensions)
        => new()
        {
            IncludeAdditionalSources = true,
            IncludeParentNodePath = false,
            IncludeSelectedNodePaths = false,
            IncludeParentSubtreePaths = false,
            AllowedExtensions = extensions,
            MaxFiles = 10,
            MaxCharactersPerFile = 4000,
            MaxTotalCharacters = 12000
        };

    private static string CreateSourcesPayload(params (string Key, string Path)[] sources)
        => JsonSerializer.Serialize(new
        {
            sources = sources.Select(source => new
            {
                key = source.Key,
                label = source.Key,
                kind = "filePath",
                value = source.Path,
                isEnabled = true
            })
        }, JsonOptions);

    private sealed class RecordingDocumentMarkdownConverter : IWorkspaceDocumentMarkdownConverter
    {
        public bool Succeeded { get; init; } = true;

        public string Message { get; init; } = "converted";

        public string Markdown { get; init; } = "converted markdown";

        public Func<WorkspaceDocumentMarkdownConversionRequest, CancellationToken, Task<WorkspaceDocumentMarkdownConversionResult>>? Handler { get; init; }

        public List<WorkspaceDocumentMarkdownConversionRequest> Requests { get; } = [];

        public CancellationToken ObservedCancellationToken { get; private set; }

        public async Task<WorkspaceDocumentMarkdownConversionResult> ConvertToMarkdownAsync(
            WorkspaceDocumentMarkdownConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            ObservedCancellationToken = cancellationToken;
            if (Handler is not null)
            {
                return await Handler(request, cancellationToken);
            }

            if (!Succeeded)
            {
                return new WorkspaceDocumentMarkdownConversionResult(
                    false,
                    Message,
                    request.SourcePath,
                    string.Empty,
                    0,
                    false,
                    Message);
            }

            return Success(request, Markdown);
        }

        public static WorkspaceDocumentMarkdownConversionResult Success(
            WorkspaceDocumentMarkdownConversionRequest request,
            string markdown)
        {
            var maxCharacters = request.MaxCharacters ?? markdown.Length;
            var truncated = markdown.Length > maxCharacters;
            return new WorkspaceDocumentMarkdownConversionResult(
                true,
                "converted",
                request.SourcePath,
                truncated ? markdown[..maxCharacters] : markdown,
                markdown.Length,
                truncated,
                string.Empty);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "candoitall-source-ingestion-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string FullPath(string relativePath)
            => System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

        public void Write(string relativePath, string content)
        {
            var path = FullPath(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void WriteBytes(string relativePath, byte[] content)
        {
            var path = FullPath(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
