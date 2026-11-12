using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

internal static class ProjectLifecycleFixture {
    internal static async Task DeleteSeededProjectAsync(IServiceProvider services, Guid projectId) {
        var original = await services.GetRequiredService<ProjectsService>().GetAsync(projectId);
        Assert.NotNull(original.ExpectedDefinition);
        var result = await SubmitAsync(services, new ProjectLifecycleCommand.Delete(original.ExpectedDefinition));
        Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(result.Result.Acknowledgement);
    }

    internal static async Task LinkSeededProjectsAsync(IServiceProvider services, Guid parentId, Guid childId) {
        var projects = services.GetRequiredService<ProjectsService>();
        var parent = await projects.GetAsync(parentId);
        var child = await projects.GetAsync(childId);
        Assert.NotNull(parent.ExpectedProjectAdmission);
        Assert.NotNull(child.ExpectedProjectAdmission);
        var result = await SubmitAsync(services, new ProjectLifecycleCommand.EnsureLink(parent.ExpectedProjectAdmission, child.ExpectedProjectAdmission));
        Assert.IsType<ProjectLifecycleAcknowledgement.LinkEnsured>(result.Result.Acknowledgement);
    }

    private static async Task<ProjectLifecycleOutcome.Accepted> SubmitAsync(IServiceProvider services, ProjectLifecycleCommand command) {
        var native = services.GetRequiredService<ProjectNativeLifecycleOperations>();
        var context = await native.GetContextAsync();
        var frozen = native.Freeze(command, ProjectNativeLifecycleOperations.Scope(context), context, Guid.NewGuid());
        var result = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await native.DispatchAsync(frozen.Submission));
        Assert.Null(result.CompletionIssue);
        return result;
    }
}
