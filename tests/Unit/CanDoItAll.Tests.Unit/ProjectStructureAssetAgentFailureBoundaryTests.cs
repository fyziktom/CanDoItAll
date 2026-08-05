using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAssetAgentFailureBoundaryTests
{
    private const string SensitivePath = @"C:\private\project\asset.bin";
    private const string SensitiveNodeId = "secret-node-id";

    public static IEnumerable<object[]> ExpectedAssetFailures()
    {
        yield return [ProjectStructureAssetAgentFailureKind.AssetTypeRequired, 400, "AssetTypeRequired"];
        yield return [ProjectStructureAssetAgentFailureKind.FileNameRequired, 400, "FileNameRequired"];
        yield return [ProjectStructureAssetAgentFailureKind.MediaPayloadRequired, 400, "MediaPayloadRequired"];
        yield return [ProjectStructureAssetAgentFailureKind.MediaPayloadTooLarge, 413, "MediaPayloadTooLarge"];
        yield return [ProjectStructureAssetAgentFailureKind.InvalidBase64Payload, 400, "InvalidBase64Payload"];
        yield return [ProjectStructureAssetAgentFailureKind.MediaSourceRequired, 400, "MediaSourceRequired"];
        yield return [ProjectStructureAssetAgentFailureKind.NodeNotFound, 404, "NodeNotFound"];
        yield return [ProjectStructureAssetAgentFailureKind.AssetRequired, 400, "AssetRequired"];
    }

    [Theory]
    [MemberData(nameof(ExpectedAssetFailures))]
    public void Reviewed_asset_failure_is_visible_retryable_and_sanitized(
        object kindValue,
        int expectedStatusCode,
        string expectedErrorCode)
    {
        var kind = Assert.IsType<ProjectStructureAssetAgentFailureKind>(kindValue);
        var exception = ProjectStructureAssetAgentFailureBoundary.Create(kind);

        Assert.Equal(expectedStatusCode, exception.StatusCode);
        Assert.Equal(expectedErrorCode, exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Null(exception.Details);
        Assert.DoesNotContain(SensitivePath, exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SensitiveNodeId, exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(MafAgentToolFailureMapper.TryMap(exception, out var result));
        Assert.Equal(exception.SafeMessage, result.Message);
        Assert.True(result.CanRetryWithCorrectedInput);
    }

    [Fact]
    public void Reviewed_asset_failure_table_covers_every_declared_kind()
    {
        var coveredKinds = ExpectedAssetFailures()
            .Select(item => Assert.IsType<ProjectStructureAssetAgentFailureKind>(item[0]))
            .ToHashSet();

        Assert.True(Enum.GetValues<ProjectStructureAssetAgentFailureKind>().ToHashSet().SetEquals(coveredKinds));
    }

    [Fact]
    public void Lease_conflict_is_visible_but_not_corrected_input_retryable_and_contains_no_owner_details()
    {
        var now = DateTimeOffset.UtcNow;
        var conflict = new ProjectStructureLeaseConflict(
            ProjectStructureLeaseScopeKind.Project,
            "secret-scope-key",
            "secret-agent-id",
            "Secret Agent Name",
            "secret-machine-name",
            SensitivePath,
            "secret-branch",
            "secret-reason-with-lease-token",
            now,
            now,
            now.AddMinutes(5));

        var exception = new ProjectStructureLeaseConflictException(conflict);

        Assert.True(exception.IsSafeToExpose);
        Assert.False(exception.CanRetryWithCorrectedInput);
        Assert.Null(exception.Details);
        Assert.Same(conflict, exception.Conflict);
        Assert.True(MafAgentToolFailureMapper.TryMap(exception, out var result));
        Assert.False(result.CanRetryWithCorrectedInput);
        Assert.Equal(exception.SafeMessage, result.Message);
        Assert.DoesNotContain(conflict.ScopeKey, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conflict.AgentId, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conflict.AgentName, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conflict.MachineName, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conflict.RepositoryRoot, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conflict.BranchName, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(conflict.Reason, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lease-token", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unrecognized_asset_failure_kind_remains_opaque_to_the_agent()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProjectStructureAssetAgentFailureBoundary.Create(
                (ProjectStructureAssetAgentFailureKind)int.MaxValue));

        Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
    }

    [Fact]
    public void Default_project_structure_exception_remains_opaque_to_the_agent()
    {
        var exception = new ProjectStructureAgentException(
            500,
            "UnexpectedAssetFailure",
            $"Unexpected failure while reading '{SensitivePath}'.",
            new { NodeId = SensitiveNodeId });

        Assert.False(exception.IsSafeToExpose);
        Assert.False(exception.CanRetryWithCorrectedInput);
        Assert.False(MafAgentToolFailureMapper.TryMap(exception, out _));
    }
}
