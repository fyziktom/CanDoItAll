using CanDoItAll.CrmHr.UI.Assignments;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class AssignmentSessionLifetimeTests {
    [Fact]
    public async Task A_late_authorization_answer_cannot_dispatch_a_retired_assignment_or_change_the_next_draft() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var first = await ProjectAssignmentNativeBoundaryTests.PrepareAsync(services, false);
        var next = await ProjectAssignmentNativeBoundaryTests.PrepareAsync(services, false);
        next.Notes = "Keep the next project input";
        var access = new WaitingAccess();
        var dispatched = 0;
        await using var session = Create(services, access, (model, token, receipt) => {
            dispatched++;
            return services.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(model, token, receipt);
        });
        await session.OpenAsync(first.ExpectedProjectAdmission);
        session.Drafts.Relationship.Open(first);
        access.HoldWrite = true;
        var saving = session.SaveRelationshipAsync(default);
        await access.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        session.Retire();
        session.Drafts.Relationship.Open(next);
        await session.OpenAsync(next.ExpectedProjectAdmission);
        var current = session.Drafts.Relationship.Draft;
        access.Release.TrySetResult();

        Assert.False(await saving);
        Assert.Equal(0, dispatched);
        Assert.Same(current, session.Drafts.Relationship.Draft);
        Assert.Equal(next.ProjectId, current.ProjectId);
        Assert.Equal(next.Notes, current.Notes);
        Assert.False(session.IsUnknown);
        Assert.Empty(await services.GetRequiredService<ProjectPartyIntegrationService>().ListAssignmentsDetailedAsync(first.ProjectId));
    }

    [Fact]
    public async Task A_read_finished_after_retarget_cannot_return_the_old_saved_content() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var first = await ProjectAssignmentNativeBoundaryTests.PrepareAsync(services, false);
        var next = await ProjectAssignmentNativeBoundaryTests.PrepareAsync(services, false);
        await using var session = Create(services, new WaitingAccess(),
            (model, token, receipt) => services.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(model, token, receipt));
        await session.OpenAsync(first.ExpectedProjectAdmission);
        var result = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = session.ReadAsync(() => {
            started.TrySetResult();
            return result.Task;
        }, default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        session.Retire();
        session.Drafts.Relationship.Open(next);
        await session.OpenAsync(next.ExpectedProjectAdmission);
        result.TrySetResult("Prior project saved content");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => read);
        Assert.Equal(next.ProjectId, session.Drafts.Relationship.Draft.ProjectId);
        Assert.True(session.HasAccess);
    }

    private static AssignmentWorkspaceSession Create(IServiceProvider services, IEditingUiAccess access,
        Func<ProjectPartyAssignmentUpsertRequest, CancellationToken, Action<ProjectAssignmentSaveReceipt>, Task<CanDoItAll.SharedKernel.Result<Guid>>> save) =>
        new(services.GetRequiredService<ProjectPartyIntegrationService>(), services.GetRequiredService<IStaffingRequestEditing>(),
            services.GetRequiredService<ProjectAssignmentChanges>(), services.GetRequiredService<CrmHrChanges>(),
            services.GetRequiredService<EditingChangeFeed>(), access, NullLogger<EditingObservation>.Instance,
            NullLogger<AssignmentWorkspaceSession>.Instance) {
            Dispatch = action => action(), Changed = () => { }, RecordsChanged = _ => Task.CompletedTask, SaveAssignment = save
        };

    private sealed class WaitingAccess : IEditingUiAccess {
        private readonly EditingUiTestAccess inner = new();
        public bool HoldWrite { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken token) => inner.OpenAsync(topic, token);
        public async ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken token) {
            if (write && HoldWrite) {
                HoldWrite = false;
                Entered.TrySetResult();
                await Release.Task;
                return;
            }
            await inner.ValidateAsync(grant, write, token);
        }
    }
}
