using Bunit;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.TestLab.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.TestLab;

public sealed class TestLabReconciliationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Successful_readback_preserves_newer_text_and_the_form_context(bool beforeBlur) {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var cut = await RenderReadyAsync(harness);
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-title-input]").ChangeAsync(new() { Value = "  Submitted plan  " }));
        var context = cut.FindComponent<EditForm>().Instance.EditContext!;
        var editor = Assert.IsType<TestPlanEditorModel>(context.Model);
        editor.Phase = "  Accepted phase  ";
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.AfterActivity = (_, _, _) => {
            probe.AfterActivity = null;
            probe.BeforeRead = async (_, _) => {
                probe.BeforeRead = null;
                entered.TrySetResult();
                await release.Task;
            };
            return Task.CompletedTask;
        };
        var pending = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await cut.InvokeAsync(() => beforeBlur
                ? cut.Find("[data-testid=testlab-title-input]").InputAsync(new() { Value = "Newer unblurred title" })
                : cut.Find("[data-testid=testlab-title-input]").ChangeAsync(new() { Value = "Newer unblurred title" }));
        } finally {
            release.TrySetResult();
        }
        await pending;
        Assert.Same(context, cut.FindComponent<EditForm>().Instance.EditContext);
        Assert.Equal("Newer unblurred title", editor.Title);
        Assert.Equal("Accepted phase", editor.Phase);
        Assert.NotNull(editor.Id);
        var accepted = await harness.Context.Services.GetRequiredService<TestLabService>().GetAsync(editor.Id);
        Assert.Equal("Submitted plan", accepted.Title);
        Assert.Equal("Newer unblurred title", cut.Find("[data-testid=testlab-title-input]").GetAttribute("value"));
    }

    [Fact]
    public async Task Concurrent_form_submissions_admit_one_new_aggregate() {
        var probe = new OwnerPostcommitTestProbe();
        await using var harness = await ComponentTestHarness.CreateAsync(probe.ConfigureServices);
        var cut = await RenderReadyAsync(harness);
        await cut.InvokeAsync(() => cut.Find("[data-testid=testlab-title-input]").ChangeAsync(new() { Value = "One dispatched create" }));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.AfterActivity = async (_, _, _) => {
            probe.AfterActivity = null;
            entered.TrySetResult();
            await release.Task;
        };
        var first = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Task second;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            second = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
            await second.WaitAsync(TimeSpan.FromSeconds(30));
        } finally {
            release.TrySetResult();
        }
        await Task.WhenAll(first, second);
        Assert.Equal(1, probe.ActivityReturns);
        Assert.Single(await harness.Context.Services.GetRequiredService<TestLabService>().ListAsync());
    }

    private static async Task<IRenderedComponent<TestLabPage>> RenderReadyAsync(ComponentTestHarness harness) {
        var admission = await OwnerPostcommitTestProbe.CreateProjectAsync(harness.Context.Services, "Reconciliation target");
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/test-lab?projectId={admission.ProjectId:D}");
        var cut = harness.Context.Render<TestLabPage>();
        cut.WaitForAssertion(() => Assert.Equal(admission.ProjectId,
            Assert.IsType<TestPlanEditorModel>(cut.FindComponent<EditForm>().Instance.EditContext!.Model).ProjectId), TimeSpan.FromSeconds(30));
        return cut;
    }
}
