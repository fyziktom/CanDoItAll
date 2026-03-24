using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Factory.CanvasAdapters;

namespace CanDoItAll.Tests.Components;

public sealed class PromptSessionAttachmentNodeTests
{
    [Fact]
    public void Link_attachment_uses_link_presentation()
    {
        var attachment = new PromptSessionAttachmentSummary
        {
            Id = "att-link",
            Kind = "link",
            Title = "Spec link",
            LinkUrl = "https://example.com/spec"
        };

        var node = PromptSessionAttachmentNode.BuildNode(attachment, 0, "selection:inputs");

        Assert.Equal("selection:input:att-link", node.Id);
        Assert.Equal("URL", node.Icon);
        Assert.Equal("link", node.MediaKind);
        Assert.Equal("https://example.com/spec", node.Subtitle);
        Assert.Equal("Link", node.StatusPill);
    }

    [Fact]
    public void Pdf_attachment_uses_document_metadata_and_file_badge()
    {
        var attachment = new PromptSessionAttachmentSummary
        {
            Id = "att-pdf",
            Kind = "file",
            Title = "Architecture spec",
            MediaRoute = "/media/spec.pdf",
            MediaOriginalFileName = "architecture-spec.pdf",
            MediaContentType = "application/pdf"
        };

        var node = PromptSessionAttachmentNode.BuildNode(attachment, 1, "selection:inputs");

        Assert.Equal("PDF", node.Icon);
        Assert.Equal("pdf", node.MediaKind);
        Assert.Equal("architecture-spec.pdf", node.Subtitle);
        Assert.Equal("PDF", node.StatusPill);
        Assert.Contains(node.FooterChips, chip => chip.Text == "architecture-spec.pdf");
    }
}
