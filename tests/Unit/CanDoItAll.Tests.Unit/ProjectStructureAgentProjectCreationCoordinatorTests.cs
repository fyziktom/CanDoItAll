using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureAgentProjectCreationCoordinatorTests {
    [Fact]
    public void Pure_validation_rejects_requests_before_access_is_reserved()
    {
        Assert.Equal(
            "ProjectNameRequired",
            Assert.Throws<ProjectStructureAgentException>(() =>
                ProjectStructureAgentCreationValidation.EnsureProjectRequest(
                    new ProjectStructureProjectSaveRequest(string.Empty, string.Empty, string.Empty, string.Empty))).ErrorCode);
        Assert.Equal(
            "ParentProjectRequired",
            Assert.Throws<ProjectStructureAgentException>(() =>
                ProjectStructureAgentCreationValidation.EnsureSubprojectRequest(
                    Guid.Empty,
                    new ProjectStructureProjectSaveRequest("Child", string.Empty, string.Empty, string.Empty))).ErrorCode);
        Assert.Equal(
            "SubprojectNameRequired",
            Assert.Throws<ProjectStructureAgentException>(() =>
                ProjectStructureAgentCreationValidation.EnsureNodesToSubprojectRequest(
                    Guid.NewGuid(),
                    new ProjectStructureNodesToSubprojectInput(string.Empty, ["node-1"]))).ErrorCode);
        Assert.Equal(
            "SelectedNodesRequired",
            Assert.Throws<ProjectStructureAgentException>(() =>
                ProjectStructureAgentCreationValidation.EnsureNodesToSubprojectRequest(
                    Guid.NewGuid(),
                    new ProjectStructureNodesToSubprojectInput("Child", []))).ErrorCode);
    }

}
