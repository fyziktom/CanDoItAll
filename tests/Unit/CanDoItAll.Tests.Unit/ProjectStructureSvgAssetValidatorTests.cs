using System.Text;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureSvgAssetValidatorTests
{
    [Fact]
    public void Validate_accepts_well_formed_svg_with_escaped_text()
    {
        var media = CreateSvg("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <text>Legend &amp; use notes</text>
            </svg>
            """);

        ProjectStructureSvgAssetValidator.Validate(media);
    }

    [Fact]
    public void Validate_rejects_unescaped_ampersand_with_agent_visible_location()
    {
        var media = CreateSvg("""
            <svg xmlns="http://www.w3.org/2000/svg">
              <text>Legend & use notes</text>
            </svg>
            """);

        var exception = Assert.Throws<ProjectStructureAgentException>(
            () => ProjectStructureSvgAssetValidator.Validate(media));

        Assert.Equal("InvalidSvgXml", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Contains("line 2", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&amp;", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_dtd_declarations()
    {
        var media = CreateSvg("""
            <!DOCTYPE svg [<!ENTITY label "unsafe">]>
            <svg xmlns="http://www.w3.org/2000/svg"><text>&label;</text></svg>
            """);

        var exception = Assert.Throws<ProjectStructureAgentException>(
            () => ProjectStructureSvgAssetValidator.Validate(media));

        Assert.Equal("InvalidSvgXml", exception.ErrorCode);
    }

    [Fact]
    public void Validate_ignores_non_svg_media()
    {
        var media = new ProjectObjectMediaPayload(
            "preview.png",
            "image/png",
            Convert.ToBase64String([0x89, 0x50, 0x4e, 0x47]));

        ProjectStructureSvgAssetValidator.Validate(media);
    }

    private static ProjectObjectMediaPayload CreateSvg(string svg)
        => new(
            "garden-plan.svg",
            "image/svg+xml",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)));
}
