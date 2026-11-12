using Bunit;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class ProjectPickerAdmissionTests {
    [Fact]
    public async Task Clearing_a_project_keeps_submission_blocked_until_both_identity_and_admission_callbacks_finish() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var selected = (await harness.Context.Services.GetRequiredService<ProjectsService>().CreateWithAdmissionAsync(new() { Name = "Project clear admission" })).Value!;
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new List<bool>();
        var admissions = new List<ProjectWriteAdmission?>();
        var cut = harness.Context.Render<ProjectPicker>(parameters => parameters
            .Add(component => component.DatabaseProfileId, selected.DatabaseProfileId)
            .Add(component => component.SelectedProjectId, selected.ProjectId)
            .Add(component => component.SelectedAdmission, selected)
            .Add(component => component.PendingChanged, value => pending.Add(value))
            .Add(component => component.SelectedProjectIdChanged, async _ => {
                reached.TrySetResult();
                await released.Task;
            })
            .Add(component => component.SelectedAdmissionChanged, value => admissions.Add(value)));

        var clearing = cut.Find("[data-testid='crmhr-project-picker-clear']").ClickAsync(new());
        try {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(Assert.Single(pending));
            Assert.Empty(admissions);
        } finally {
            released.TrySetResult();
        }
        await clearing;

        Assert.Equal([true, false], pending);
        Assert.Null(Assert.Single(admissions));
    }
}
