using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class ProcessTemplateMermaidExporterTests
{
    [Fact]
    public void Export_returns_mermaid_documents_and_supporting_files()
    {
        var loader = new ProcessTemplatePackLoader();
        var exporter = new ProcessTemplateMermaidExporter(loader);

        var document = exporter.Export("customer-onboarding");

        Assert.Equal("customer-onboarding", document.ProcessKey);
        Assert.Contains("flowchart", document.Flowchart, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sequenceDiagram", document.Sequence, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(document.SupportingFiles, item => item.EndsWith("definition.md", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(document.SupportingFiles, item => item.EndsWith("roles/account-owner.md", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(document.SupportingFiles, item => item.EndsWith("steps/kickoff-approval.md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExportToFolder_copies_mermaid_and_docs()
    {
        var loader = new ProcessTemplatePackLoader();
        var exporter = new ProcessTemplateMermaidExporter(loader);
        var outputFolder = Path.Combine(Path.GetTempPath(), "candoitall-process-template-mermaid-" + Guid.NewGuid().ToString("N"));

        try
        {
            var files = exporter.ExportToFolder("incident-response", outputFolder);

            Assert.Contains(files, item => item.EndsWith("flowchart.mmd", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(files, item => item.EndsWith("sequence.mmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(Path.Combine(outputFolder, "flowchart.mmd")));
            Assert.True(File.Exists(Path.Combine(outputFolder, "sequence.mmd")));
        }
        finally
        {
            if (Directory.Exists(outputFolder))
            {
                Directory.Delete(outputFolder, recursive: true);
            }
        }
    }
}
