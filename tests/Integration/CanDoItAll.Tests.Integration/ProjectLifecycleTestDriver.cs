using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProjectLifecycleTestDriver(IServiceProvider services) {
    private readonly ProjectNativeLifecycleOperations operations = services.GetRequiredService<ProjectNativeLifecycleOperations>();

    internal async Task<ProjectLifecycleSubmission> CaptureDeleteAsync(Guid projectId) {
        var context = await operations.GetContextAsync();
        var definition = await services.GetRequiredService<ProjectDefinitionReader>().ReadAsync(projectId);
        Assert.True(definition.IsSuccess);
        Assert.Equal(context, await operations.GetContextAsync());
        return operations.Freeze(new ProjectLifecycleCommand.Delete(definition.Value!.Expectation),
            ProjectNativeLifecycleOperations.Scope(context), context, Guid.NewGuid()).Submission;
    }

    internal Task<ProjectLifecycleOutcome> SubmitAsync(ProjectLifecycleSubmission original)
        => operations.DispatchAsync(original);

    internal Task<ProjectLifecycleLookup> LookupAsync(ProjectLifecycleSubmission original)
        => operations.LookupAsync(original.Operation);

    internal async Task DeleteAsync(Guid projectId)
        => Assert.IsType<ProjectLifecycleOutcome.Accepted>(await SubmitAsync(await CaptureDeleteAsync(projectId)));

    internal async Task EnsureLinkAsync(Guid parentId, Guid childId) {
        var context = await operations.GetContextAsync();
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var parent = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(parentId));
        var child = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(childId));
        Assert.Equal(context, await operations.GetContextAsync());
        var original = operations.Freeze(new ProjectLifecycleCommand.EnsureLink(parent, child),
            ProjectNativeLifecycleOperations.Scope(context), context, Guid.NewGuid());
        Assert.IsType<ProjectLifecycleOutcome.Accepted>(await SubmitAsync(original.Submission));
    }

    internal async Task<ProjectHierarchyEdgeExpectation> ReadEdgeAsync(ProjectWriteAdmission parent, ProjectWriteAdmission child)
        => await operations.ReadEdgeAsync(await operations.GetContextAsync(), parent, child);

    internal async Task<Func<Task<ProjectLifecycleOutcome>>> CaptureSourceOwnerAsync(ProjectLifecycleCommand command,
        ProjectMutationAuthorization authorization, ProjectLifecycleEffectKind kind) {
        var identity = SHA256.HashData(Guid.NewGuid().ToByteArray());
        var caller = ProjectMutationCallerScope.FromAdmittedExecution(new(identity));
        var context = await services.GetRequiredService<ProjectLifecycleOperationProtocol>().GetWriteContextAsync(caller);
        var original = new ProjectLifecycleSubmission(new(context.DatabaseProfileId, context.Namespace, context.Epoch,
            context.CallerBinding, Guid.NewGuid()), ProjectLifecycleInterpretation.VersionOne, command);
        var access = new SourceOwnerObservation(original);
        var invocation = ProjectLifecycleInvocation.ForGoverned(caller, access, authorization, new(kind, 1, identity), access);
        return () => services.GetRequiredService<ProjectLifecycleWriter>().SubmitAsync(original, invocation);
    }

    private sealed class SourceOwnerObservation(ProjectLifecycleSubmission original) : IProjectLifecycleReceiptAccess, IProjectLifecycleCurrentWriteAccess {
        public Task<bool> CanReadAsync(ProjectLifecycleSubmission submission, CancellationToken cancellationToken)
            => Task.FromResult(submission == original);

        public ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken)
            => new(ProjectLifecycleWriteAccessDecision.Unavailable);
    }
}
