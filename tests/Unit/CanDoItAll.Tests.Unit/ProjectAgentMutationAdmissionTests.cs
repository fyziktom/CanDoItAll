using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit.ProjectStructure;

public sealed class ProjectAgentMutationAdmissionTests {
    [Fact]
    public void Admission_copies_grants_before_caller_collections_or_flags_change() {
        var profileId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var lifetime = new AgentProjectStructureLifetime(profileId, projectId, Guid.NewGuid());
        var access = new AgentProjectStructureAccessSettings {
            CanRead = true, CanWriteNonTaskStructure = true,
            AllowedProjectIds = [projectId], AllowedProjectLifetimes = [lifetime]
        };
        var admission = new ProjectAgentMutationAdmission(Guid.NewGuid(), profileId, access, null, ProjectAgentMutationDomain.NonTaskStructure, null);
        access.CanWriteTasks = true;
        access.AllowAllProjects = true;
        access.AllowedProjectIds.Clear();
        access.AllowedProjectLifetimes.Clear();
        Assert.True(admission.CanRead);
        Assert.True(admission.CanWriteStructure);
        Assert.False(admission.CanWriteTasks);
        Assert.False(admission.AllowAllProjects);
        Assert.Equal(projectId, Assert.Single(admission.ProjectIds));
        Assert.Equal(lifetime, Assert.Single(admission.ProjectLifetimes));
    }

    [Fact]
    public void Native_request_serialization_cannot_create_or_recover_Agent_authority() {
        var profileId = Guid.NewGuid();
        var project = new ProjectWriteAdmission(profileId, Guid.NewGuid(), Guid.NewGuid());
        var admission = new ProjectAgentMutationAdmission(Guid.NewGuid(), profileId,
            new() { CanRead = true, CanWrite = true, AllowAllProjects = true }, null, ProjectAgentMutationDomain.Tasks, null);
        var original = new ProjectObjectCreateRequest(ProjectObjectType.WorkItem, "Task", "", "", $"project:{project.ProjectId}") {
            ExpectedProjectAdmission = project, AgentMutationAdmission = admission
        };
        var json = JsonSerializer.Serialize(original);
        Assert.DoesNotContain(nameof(ProjectObjectCreateRequest.AgentMutationAdmission), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ProjectObjectCreateRequest.ExpectedProjectAdmission), json, StringComparison.Ordinal);
        var restored = Assert.IsType<ProjectObjectCreateRequest>(JsonSerializer.Deserialize<ProjectObjectCreateRequest>(json));
        Assert.Equal(original.Title, restored.Title);
        Assert.Null(restored.AgentMutationAdmission);
        Assert.Null(restored.ExpectedProjectAdmission);
    }

    [Fact]
    public void Agent_context_serialization_preserves_public_identity_without_fabricating_internal_grants() {
        var profileId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var original = new ProjectStructureAgentContext(agentId.ToString("D"), "Source", "test", "test", "", "session") {
            AgentMutationAdmission = new(agentId, profileId, new() { CanRead = true, CanWrite = true, AllowAllProjects = true },
                null, ProjectAgentMutationDomain.NonTaskStructure, "original-operation"),
            ExpectedProjectAdmission = new(profileId, Guid.NewGuid(), Guid.NewGuid())
        };
        var json = JsonSerializer.Serialize(original);
        var restored = Assert.IsType<ProjectStructureAgentContext>(JsonSerializer.Deserialize<ProjectStructureAgentContext>(json));
        Assert.Equal(original.AgentId, restored.AgentId);
        Assert.Equal(original.SessionId, restored.SessionId);
        Assert.Null(restored.AgentMutationAdmission);
        Assert.Null(restored.ExpectedProjectAdmission);
        Assert.DoesNotContain("original-operation", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_mutation_domain_is_rejected_before_an_admission_exists() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectAgentMutationAdmission(Guid.NewGuid(), Guid.NewGuid(),
            new(), null, (ProjectAgentMutationDomain)int.MaxValue, null));
    }
}
