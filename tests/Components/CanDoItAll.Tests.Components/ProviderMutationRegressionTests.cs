using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using RuntimeAdministration = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using ProjectionException = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderCatalogProjectionException;
using ProjectionKind = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderCatalogProjectionOperationKind;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class ProviderMutationRegressionTests {
    [Fact]
    public async Task New_commit_projection_failure_retains_identity_without_another_write() {
        var reads = new Reads();
        var runtime = DispatchProxy.Create<RuntimeAdministration, RuntimeProxy>();
        var proxy = (RuntimeProxy)(object)runtime;
        proxy.Save = _ => Task.FromException<Guid>(new ProjectionException(reads.Id, ProjectionKind.Upsert,
            "Read the committed provider.", new IOException("Projection unavailable")));
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProviderProfilesReads>(reads);
            services.AddSingleton(runtime);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        await cut.Find("[data-testid='providers-new']").ClickAsync();
        cut.Find("[data-testid='providers-model-input']").Change("model");
        await cut.FindComponent<ProviderProfileEditorForm>().Find("form").SubmitAsync();
        var draft = Assert.IsType<ProviderProfileEditorModel>(cut.FindComponent<ProviderProfileEditorForm>().Instance.Context.Model);
        Assert.Equal(reads.Id, draft.Id);
        Assert.Equal(1, proxy.WriteCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_save_owns_submission_and_preserves_later_edits(bool firstSave) {
        var reads = new Reads();
        var pending = new TaskCompletionSource<Guid>();
        ProviderProfileEditorModel? submitted = null;
        var runtime = DispatchProxy.Create<RuntimeAdministration, RuntimeProxy>();
        ((RuntimeProxy)(object)runtime).Save = model => {
            submitted = model;
            return pending.Task;
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProviderProfilesReads>(reads);
            services.AddSingleton(runtime);
        });
        var cut = harness.Context.Render<AgentProviderProfilesPanel>();
        cut.WaitForElement("[data-testid='providers-name-input']");
        if (firstSave) {
            await cut.Find("[data-testid='providers-new']").ClickAsync();
            cut.Find("[data-testid='providers-model-input']").Change("model");
        }
        cut.Find("[data-testid='providers-name-input']").Change("Submitted");
        var context = cut.FindComponent<ProviderProfileEditorForm>().Instance.Context;
        var save = cut.FindComponent<ProviderProfileEditorForm>().Find("form").SubmitAsync();
        cut.WaitForAssertion(() => Assert.NotNull(submitted));
        cut.Find("[data-testid='providers-name-input']").Change("Later edit");
        var capturedName = submitted!.Name;
        var independent = !ReferenceEquals(context.Model, submitted);
        await cut.InvokeAsync(() => pending.SetResult(reads.Id));
        await save;
        Assert.True(independent);
        Assert.Equal("Submitted", capturedName);
        Assert.Same(context, cut.FindComponent<ProviderProfileEditorForm>().Instance.Context);
        var draft = (ProviderProfileEditorModel)context.Model;
        Assert.Equal(reads.Id, draft.Id);
        Assert.Equal("Later edit", draft.Name);
    }

    public class RuntimeProxy : DispatchProxy {
        public Func<ProviderProfileEditorModel, Task<Guid>> Save { get; set; } = _ => throw new InvalidOperationException();
        public int WriteCount { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(RuntimeAdministration.SaveProviderAsync)) {
                WriteCount++;
                return Save((ProviderProfileEditorModel)args![0]!);
            }
            throw new InvalidOperationException("Unexpected runtime operation.");
        }
    }

    private sealed class Reads : IProviderProfilesReads {
        public Guid Id { get; } = Guid.NewGuid();
        public Task<ProviderProfilesCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderProfilesCatalog([new ProviderProfile(Id, "Saved", ProviderKind.OpenAi,
                string.Empty, string.Empty, "model", ProviderTransportKind.Responses, true, true, true, false, true,
                "{}", string.Empty, string.Empty, null, ["model"])], new([])));
        public Task<ProviderProfileEditorModel> LoadEditorAsync(Guid providerId, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProviderProfileEditorModel { Id = Id, Name = "Saved", DefaultModel = "model", SuggestedModels = ["model"] });
    }
}

