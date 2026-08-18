using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkspaceProcessRunArtifactPathTests
{
    [Theory]
    [InlineData("artifacts/process-runs/dotnet-run/20260624-194753350/startup.json")]
    [InlineData("artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/dotnet-run/20260624-194753350/startup.json")]
    public void TryResolveRunId_ignores_dotnet_run_runtime_receipt_namespace(string path)
    {
        var result = WorkspaceProcessRunArtifactPath.TryResolveRunId(
            path,
            out var processRunId,
            out var artifactSuffix);

        Assert.False(result);
        Assert.Equal(string.Empty, processRunId);
        Assert.Equal(string.Empty, artifactSuffix);
    }

    [Fact]
    public void TryBuildRecoverableCurrentRunPath_maps_ellipsized_current_run_ref_to_scoped_artifact_path()
    {
        var currentRunId = Guid.Parse("2a4b7a65-93fe-42b9-b613-14b87b669f76");
        var scope = WorkspaceScopeDescriptor.Organization("e5df9ad633dbc6974a0678a74976013c");

        var result = WorkspaceProcessRunArtifactPath.TryBuildRecoverableCurrentRunPath(
            "artifacts/process-runs/2a.../steps/classify-dotnet-application.md",
            currentRunId.ToString("D"),
            scope,
            out var currentRunPath);

        Assert.True(result);
        Assert.Equal(
            $"artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/{currentRunId:D}/steps/classify-dotnet-application.md",
            currentRunPath);
    }

    [Fact]
    public void TryBuildRecoverableCurrentRunPath_maps_punctuation_damaged_current_run_ref()
    {
        var currentRunId = Guid.Parse("762e75b4-3fa7-4a72-b801-0b1f06b5b7c8");

        var result = WorkspaceProcessRunArtifactPath.TryBuildRecoverableCurrentRunPath(
            "artifacts/process-runs/762e75b4-3fa7-4a72-b801-0b1f06b.5b7c8/steps/architecture-handoff.md",
            currentRunId.ToString("D"),
            WorkspaceScopeDescriptor.Sandbox,
            out var currentRunPath);

        Assert.True(result);
        Assert.Equal(
            $"artifacts/process-runs/{currentRunId:D}/steps/architecture-handoff.md",
            currentRunPath);
    }

    [Fact]
    public void TryBuildRecoverableCurrentRunPath_refuses_valid_other_run_ref()
    {
        var currentRunId = Guid.Parse("762e75b4-3fa7-4a72-b801-0b1f06b5b7c8");
        var otherRunId = Guid.Parse("fd416a92-a29e-442d-ba0b-c2742d8eb2e0");

        var result = WorkspaceProcessRunArtifactPath.TryBuildRecoverableCurrentRunPath(
            $"artifacts/process-runs/{otherRunId:D}/steps/architecture-handoff.md",
            currentRunId.ToString("D"),
            WorkspaceScopeDescriptor.Sandbox,
            out var currentRunPath);

        Assert.False(result);
        Assert.Equal(string.Empty, currentRunPath);
    }

    [Fact]
    public void TryBuildRecoverableCurrentRunPath_refuses_unrelated_malformed_run_ref()
    {
        var currentRunId = Guid.Parse("762e75b4-3fa7-4a72-b801-0b1f06b5b7c8");

        var result = WorkspaceProcessRunArtifactPath.TryBuildRecoverableCurrentRunPath(
            "artifacts/process-runs/not-the-run/steps/architecture-handoff.md",
            currentRunId.ToString("D"),
            WorkspaceScopeDescriptor.Sandbox,
            out var currentRunPath);

        Assert.False(result);
        Assert.Equal(string.Empty, currentRunPath);
    }
}
