using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Support;

public static class ProjectLifecycleTestCommands {
    public static async Task<ProjectLifecycleSubmission> CaptureDeleteAsync(IServiceProvider services, Guid projectId,
        CancellationToken cancellationToken = default, ProjectWriteAdmission? expectedProjectAdmission = null) {
        var definition = await services.GetRequiredService<ProjectsService>().GetDefinitionAsync(projectId, cancellationToken);
        if (!definition.IsSuccess || definition.Value is null) {
            throw new InvalidOperationException("The lifecycle fixture requires an existing project definition.");
        }
        if (expectedProjectAdmission is not null && definition.Value.Expectation.Project != expectedProjectAdmission) {
            throw new ProjectWriteAdmissionRejectedException(expectedProjectAdmission);
        }
        return await CaptureAsync(services, new ProjectLifecycleCommand.Delete(definition.Value.Expectation), cancellationToken);
    }

    public static async Task<ProjectLifecycleOutcome.Accepted> DeleteAsync(IServiceProvider services, Guid projectId,
        CancellationToken cancellationToken = default, ProjectWriteAdmission? expectedProjectAdmission = null) {
        var original = await CaptureDeleteAsync(services, projectId, cancellationToken, expectedProjectAdmission);
        return RequireAccepted(await SubmitAsync(services, original, cancellationToken));
    }

    public static async Task<ProjectLifecycleOutcome> EnsureLinkAsync(IServiceProvider services, Guid parentId, Guid childId,
        CancellationToken cancellationToken = default)
        => await SubmitAsync(services, await CaptureLinkAsync(services, parentId, childId, cancellationToken), cancellationToken);

    public static async Task<ProjectLifecycleSubmission> CaptureLinkAsync(IServiceProvider services, Guid parentId, Guid childId,
        CancellationToken cancellationToken = default) {
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var parent = await admissions.CaptureAsync(parentId, cancellationToken)
            ?? throw new InvalidOperationException("The lifecycle fixture requires an existing parent.");
        var child = await admissions.CaptureAsync(childId, cancellationToken)
            ?? throw new InvalidOperationException("The lifecycle fixture requires an existing child.");
        return await CaptureAsync(services, new ProjectLifecycleCommand.EnsureLink(parent, child), cancellationToken);
    }

    public static async Task<ProjectLifecycleOutcome> ReconnectAsync(IServiceProvider services, Guid childId,
        Guid parentId, Guid newParentId, CancellationToken cancellationToken = default) {
        var link = await CaptureLinkAsync(services, parentId, childId, cancellationToken);
        var command = (ProjectLifecycleCommand.EnsureLink)link.Command;
        var operations = services.GetRequiredService<ProjectNativeLifecycleOperations>();
        var edge = await operations.ReadEdgeAsync(await operations.GetContextAsync(cancellationToken),
            command.Parent, command.Child, cancellationToken);
        var newParent = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(newParentId, cancellationToken)
            ?? throw new InvalidOperationException("The lifecycle fixture requires an existing destination parent.");
        return await SubmitAsync(services, await CaptureAsync(services,
            new ProjectLifecycleCommand.ReconnectLink(edge, newParent), cancellationToken), cancellationToken);
    }

    public static async Task<ProjectLifecycleSubmission> CaptureAsync(IServiceProvider services, ProjectLifecycleCommand command,
        CancellationToken cancellationToken = default) {
        var context = await services.GetRequiredService<ProjectNativeLifecycleOperations>().GetContextAsync(cancellationToken);
        return new(new(context.DatabaseProfileId, context.Namespace, context.Epoch, context.CallerBinding, Guid.NewGuid()),
            ProjectLifecycleInterpretation.VersionOne, command);
    }

    public static Task<ProjectLifecycleOutcome> SubmitAsync(IServiceProvider services, ProjectLifecycleSubmission original,
        CancellationToken cancellationToken = default)
        => services.GetRequiredService<ProjectNativeLifecycleOperations>().DispatchAsync(original, cancellationToken);

    public static ProjectLifecycleOutcome.Accepted RequireAccepted(ProjectLifecycleOutcome outcome)
        => outcome is ProjectLifecycleOutcome.Accepted { CompletionIssue: null, DiagnosticsUnavailable: false } accepted
            ? accepted : throw new InvalidOperationException($"The lifecycle fixture did not complete: {outcome}.");
}
