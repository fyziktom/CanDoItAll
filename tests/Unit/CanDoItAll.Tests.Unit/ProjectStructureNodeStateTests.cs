using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.Workbench;

public sealed class ProjectStructureNodeStateTests {
    [Fact]
    public void Legacy_marker_projection_and_canonical_row_have_the_same_expectation() {
        var row = new ProjectObjectRecord {
            ProjectId = Guid.NewGuid(), NodeKey = "custom:legacy-marker", ObjectType = ProjectObjectType.Note,
            MetadataJson = """{"markerSet":{"markers":[{"icon":"flag","tone":"red","label":"Review"}]}}"""
        };
        var mapped = ProjectWorkbenchNodeMapper.MapStructureNode(row);
        var displayed = mapped with { Markers = ProjectNodeLegacyMetadata.ReadLegacyMarkers(row.MetadataJson) };
        Assert.NotEmpty(displayed.Markers);
        var expected = ProjectStructureNodeStateGuard.Capture(displayed,
            new ProjectWriteAdmission(Guid.NewGuid(), row.ProjectId, Guid.NewGuid()));
        ProjectStructureNodeStateGuard.EnsureCurrent(expected, row);
        row.Notes = "Changed after read";
        Assert.Throws<ProjectStructureEditConflictException>(() => ProjectStructureNodeStateGuard.EnsureCurrent(expected, row));
    }
}
