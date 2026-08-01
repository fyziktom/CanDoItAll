using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowSourceIngestionArchitectureTests
{
    private static readonly string SourceIngestionDirectory = Path.Combine(
        FindRepositoryRoot(),
        "src",
        "MAF",
        "WorkflowExecutors",
        "Standard",
        "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace");

    [Fact]
    public void ConversionRequestKeepsSourcePath()
    {
        AssertProperty(typeof(WorkspaceDocumentMarkdownConversionRequest), "SourcePath");
    }

    [Fact]
    public void ConversionRequestExposesMaximumCharacterLimit()
    {
        AssertProperty(typeof(WorkspaceDocumentMarkdownConversionRequest), "MaxCharacters");
    }

    [Fact]
    public void ConversionRequestDoesNotOwnOutputPath()
    {
        Assert.Null(typeof(WorkspaceDocumentMarkdownConversionRequest).GetProperty("OutputPath"));
    }

    [Fact]
    public void ConversionResultExposesMarkdown()
    {
        AssertProperty(typeof(WorkspaceDocumentMarkdownConversionResult), "Markdown");
    }

    [Fact]
    public void ConversionResultExposesTotalMarkdownCharacters()
    {
        AssertProperty(typeof(WorkspaceDocumentMarkdownConversionResult), "TotalMarkdownCharacters");
    }

    [Fact]
    public void ConversionResultExposesTruncationState()
    {
        AssertProperty(typeof(WorkspaceDocumentMarkdownConversionResult), "IsTruncated");
    }

    [Fact]
    public void ConversionResultDoesNotOwnOutputPath()
    {
        Assert.Null(typeof(WorkspaceDocumentMarkdownConversionResult).GetProperty("OutputPath"));
    }

    [Fact]
    public void SourceIngestionExecutorIsNotPartial()
    {
        var source = File.ReadAllText(Path.Combine(SourceIngestionDirectory, "SourceIngestionWorkflowExecutor.cs"));

        Assert.DoesNotContain("partial class SourceIngestionWorkflowExecutor", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceIngestionExecutorDependsOnSharedDocumentConverter()
    {
        var constructor = Assert.Single(typeof(SourceIngestionWorkflowExecutor).GetConstructors());

        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IWorkspaceDocumentMarkdownConverter));
    }

    [Theory]
    [InlineData("PdfDocument.Open")]
    [InlineData("ReadDocx(")]
    [InlineData("word/document.xml")]
    [InlineData("Regex.Replace(html")]
    [InlineData("\".xls\" or \".xlsx\"")]
    public void SourceIngestionDoesNotOwnDocumentFormatParsers(string forbiddenMarker)
    {
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(SourceIngestionDirectory, "*.cs")
                .Where(path =>
                    Path.GetFileName(path).Contains("SourceIngestion", StringComparison.Ordinal) ||
                    Path.GetFileName(path).StartsWith("WorkflowSource", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.DoesNotContain(forbiddenMarker, source, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceIngestionProjectDoesNotReferencePdfPig()
    {
        var projectPath = Path.Combine(
            SourceIngestionDirectory,
            "CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace.csproj");
        var project = File.ReadAllText(projectPath);

        Assert.DoesNotContain("PdfPig", project, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("WorkflowSourceCandidateCollector.cs", "WorkflowSourceCandidateCollector")]
    [InlineData("WorkflowSourceFileResolver.cs", "WorkflowSourceFileResolver")]
    [InlineData("WorkflowSourceDocumentReader.cs", "WorkflowSourceDocumentReader")]
    public void SourceIngestionCollaboratorIsSeparateSealedNonPartialType(
        string fileName,
        string typeName)
    {
        var path = Path.Combine(SourceIngestionDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected source-ingestion collaborator '{fileName}' was not found.");

        var source = File.ReadAllText(path);
        Assert.Contains($"sealed class {typeName}", source, StringComparison.Ordinal);
        Assert.DoesNotContain($"partial class {typeName}", source, StringComparison.Ordinal);
    }

    private static void AssertProperty(Type type, string propertyName)
    {
        Assert.NotNull(type.GetProperty(propertyName));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
