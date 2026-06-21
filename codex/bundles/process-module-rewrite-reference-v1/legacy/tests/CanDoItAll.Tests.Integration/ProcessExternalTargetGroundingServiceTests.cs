using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessExternalTargetGroundingServiceTests
{
    [Fact]
    public void ResolveProjectStructureGroundingTarget_returns_typed_target_and_scaffold_contract()
    {
        var result = ProcessExternalTargetGroundingService.ResolveProjectStructureGroundingTarget(
            """
            Dispatcher fetched the live project structure for `RoadmapApp`.
            Grounded external target paths from the selected project structure:
            - `C:\work\apps\RoadmapApp` mapped to `external-target/C/work/apps/RoadmapApp` from Product root (custom:target)
            """);

        Assert.True(result.HasTarget);
        Assert.Equal(@"C:\work\apps\RoadmapApp", result.AbsolutePath);
        Assert.Equal("external-target/C/work/apps/RoadmapApp", result.MappedAlias);
        Assert.NotNull(result.ScaffoldTarget);
        Assert.Equal("external-target/C/work/apps", result.ScaffoldTarget.ParentAlias);
        Assert.Equal("RoadmapApp", result.ScaffoldTarget.LeafName);
    }

    [Fact]
    public void ResolveProjectStructureGroundingTarget_ignores_prohibited_targets()
    {
        var result = ProcessExternalTargetGroundingService.ResolveProjectStructureGroundingTarget(
            """
            Project constraints:
            - Do not inspect, copy, write, or use sibling target C:\work\apps\OldSample.
            """);

        Assert.False(result.HasTarget);
        Assert.Equal(string.Empty, result.MappedAlias);
    }

    [Theory]
    [InlineData("external-target/C/work/apps/Allowed/../Sibling/Program.cs")]
    [InlineData(@"C:\work\apps\Allowed\..\Sibling\Program.cs")]
    public void InspectReferences_rejects_escaped_sibling_targets(string escapedReference)
    {
        var inspection = ProcessExternalTargetGroundingService.InspectReferences(
            $"Validation evidence references {escapedReference}.",
            ["external-target/C/work/apps/Allowed"]);

        Assert.True(inspection.HasOutOfScopeReference);
        Assert.Contains("outside the current grounded product root", inspection.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sibling", inspection.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InspectReferences_allows_descendants_under_current_grounded_root()
    {
        var inspection = ProcessExternalTargetGroundingService.InspectReferences(
            "Validation evidence references external-target/C/work/apps/Allowed/src/Program.cs.",
            ["external-target/C/work/apps/Allowed"]);

        Assert.False(inspection.HasOutOfScopeReference);
        Assert.Equal(string.Empty, inspection.Summary);
    }

    [Fact]
    public void RedactUnallowedReferencesForPrompt_omits_stale_paths_after_escape_normalization()
    {
        var redacted = ProcessExternalTargetGroundingService.RedactUnallowedReferencesForPrompt(
            @"Previous run summary named C:\work\apps\Allowed\..\Sibling\Program.cs as evidence.",
            ["external-target/C/work/apps/Allowed"]);

        Assert.Contains("[stale external-target path omitted]", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("Sibling", redacted, StringComparison.OrdinalIgnoreCase);
    }
}
