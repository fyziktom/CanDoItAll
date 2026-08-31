using System.Reflection;
using System.Runtime.CompilerServices;
using Bunit;
using CanDoItAll.AgentFramework.Components;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentDetailsDialogDeletionTests
{
    private const string DeleteButtonSelector = "[data-testid='agents-catalog-delete']";
    private const string ConfirmationSelector = "[data-testid='agents-catalog-delete-confirmation']";
    private const string ConfirmationContentSelector = "[data-testid='agents-catalog-delete-confirmation-content']";
    private const string CancelButtonSelector = "[data-testid='agents-catalog-delete-cancel']";
    private const string ConfirmButtonSelector = "[data-testid='agents-catalog-delete-confirm']";

    [Fact]
    public async Task Delete_opens_confirmation_without_mutating_the_agent()
    {
        using var harness = new DeletionHarness();
        var editor = CreateEditor();
        var detailsTask = harness.OpenDetails(editor);

        var clickTask = harness.ClickDeleteAsync();

        var confirmation = harness.Host.WaitForElement(ConfirmationSelector);
        Assert.Equal(0, harness.Workspace.DeleteCallCount);
        Assert.Equal(2, harness.DialogService.Dialogs.Count);
        Assert.Equal(
            typeof(AgentDeleteConfirmationDialog),
            harness.DialogService.Dialogs[^1].ComponentType);
        Assert.Contains(editor.Name, confirmation.TextContent, StringComparison.Ordinal);
        Assert.Contains(
            "cannot be undone",
            confirmation.TextContent,
            StringComparison.OrdinalIgnoreCase);

        harness.Host.Find(CancelButtonSelector).Click();
        await clickTask.WaitAsync(TimeSpan.FromSeconds(2));
        await harness.CloseDetailsAsync(detailsTask);
    }

    [Fact]
    public async Task Cancelling_delete_does_not_mutate_or_close_agent_details()
    {
        using var harness = new DeletionHarness();
        var detailsTask = harness.OpenDetails(CreateEditor());
        var clickTask = harness.ClickDeleteAsync();
        harness.Host.WaitForElement(ConfirmationSelector);

        harness.Host.Find(CancelButtonSelector).Click();
        await clickTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, harness.Workspace.DeleteCallCount);
        Assert.False(detailsTask.IsCompleted);
        Assert.Single(harness.DialogService.Dialogs);
        Assert.NotNull(harness.Host.Find(DeleteButtonSelector));

        await harness.CloseDetailsAsync(detailsTask);
    }

    [Fact]
    public async Task Confirming_delete_mutates_the_agent_exactly_once()
    {
        using var harness = new DeletionHarness();
        var editor = CreateEditor();
        var detailsTask = harness.OpenDetails(editor);
        var clickTask = harness.ClickDeleteAsync();
        harness.Host.WaitForElement(ConfirmationSelector);

        harness.Host.Find(ConfirmButtonSelector).Click();
        await clickTask.WaitAsync(TimeSpan.FromSeconds(2));
        var result = await detailsTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, harness.Workspace.DeleteCallCount);
        Assert.Equal(editor.Id, harness.Workspace.DeletedAgentId);
        var deleteResult = Assert.IsType<AgentDetailsDialogResult>(result);
        Assert.Equal(editor.Id, deleteResult.AgentId);
        Assert.True(deleteResult.Deleted);
        Assert.Empty(harness.DialogService.Dialogs);
    }

    [Fact]
    public async Task Successful_dialog_delete_emits_only_the_close_result_channel()
    {
        using var harness = new DeletionHarness();
        var editor = CreateEditor();
        var savedResults = new List<AgentDetailsDialogResult>();
        var detailsTask = harness.OpenDetails(editor, savedResults.Add);
        var clickTask = harness.ClickDeleteAsync();
        harness.Host.WaitForElement(ConfirmationSelector);

        harness.Host.Find(ConfirmButtonSelector).Click();
        await clickTask.WaitAsync(TimeSpan.FromSeconds(2));
        var result = await detailsTask.WaitAsync(TimeSpan.FromSeconds(2));

        var deleteResult = Assert.IsType<AgentDetailsDialogResult>(result);
        Assert.Equal(editor.Id, deleteResult.AgentId);
        Assert.True(deleteResult.Deleted);
        Assert.Empty(savedResults);
        Assert.Equal(1, harness.Workspace.DeleteCallCount);
    }

    [Fact]
    public async Task Failed_delete_keeps_agent_details_open_and_usable()
    {
        using var harness = new DeletionHarness();
        harness.Workspace.DeleteFailure = new InvalidOperationException("Deletion probe failure.");
        var detailsTask = harness.OpenDetails(CreateEditor());
        var clickTask = harness.ClickDeleteAsync();
        harness.Host.WaitForElement(ConfirmationSelector);

        harness.Host.Find(ConfirmButtonSelector).Click();
        await clickTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, harness.Workspace.DeleteCallCount);
        Assert.False(detailsTask.IsCompleted);
        Assert.Single(harness.DialogService.Dialogs);
        var deleteButton = harness.Host.Find(DeleteButtonSelector);
        Assert.False(deleteButton.HasAttribute("disabled"));
        Assert.NotEqual("true", deleteButton.GetAttribute("aria-busy"));

        var retryTask = harness.ClickDeleteAsync();
        var confirmation = harness.Host.WaitForElement(ConfirmationContentSelector);
        Assert.NotNull(confirmation);
        Assert.Equal(1, harness.Workspace.DeleteCallCount);
        harness.Host.Find(CancelButtonSelector).Click();
        await retryTask.WaitAsync(TimeSpan.FromSeconds(2));

        await harness.CloseDetailsAsync(detailsTask);
    }

    private static AgentEditorModel CreateEditor()
    {
        return new AgentEditorModel
        {
            Id = Guid.NewGuid(),
            Name = "Gardener deletion probe"
        };
    }

    public sealed class TestAgentDetailsDialog : AgentDetailsDialog
    {
        [Parameter]
        public AgentEditorModel TestEditor { get; set; } = new();

        protected override Task OnInitializedAsync()
        {
            SetBaseField("editorModel", TestEditor);
            SetBaseField("isLoading", false);
            return Task.CompletedTask;
        }

        private void SetBaseField(string fieldName, object value)
        {
            var field = typeof(AgentDetailsDialog).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(AgentDetailsDialog).FullName, fieldName);
            field.SetValue(this, value);
        }
    }

    public class RecordingWorkspaceServiceProxy : DispatchProxy
    {
        public int DeleteCallCount { get; private set; }

        public Guid? DeletedAgentId { get; private set; }

        public Exception? DeleteFailure { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.DeleteAgentAsync) =>
                    DeleteAgent((Guid)args![0]!),
                "add_ExecutionUpdated" or "remove_ExecutionUpdated" => null,
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in this component test.")
            };
        }

        private Task DeleteAgent(Guid agentId)
        {
            DeleteCallCount++;
            DeletedAgentId = agentId;
            return DeleteFailure is null
                ? Task.CompletedTask
                : Task.FromException(DeleteFailure);
        }
    }

    private sealed class DeletionHarness : IDisposable
    {
        private readonly BunitContext context = new();

        public DeletionHarness()
        {
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddCanDoItAllBaseLib();
        context.Services.AddStubProviderRuntimeAdministration();
            context.Services.AddSingleton<IExternalTargetPathRegistryFactory>(new ExternalTargetPathRegistryFactory());
            context.Services.AddSingleton<IStorageCatalogSelectionSource>(new EmptyStorageCatalogSelectionSource());
            var avatarGenerationService = new AgentAvatarGenerationService(
                new UnavailableAgentImageGenerationService(),
                NullLogger<AgentAvatarGenerationService>.Instance);

            var workspaceService = DispatchProxy.Create<
                IAgentFrameworkWorkspaceService,
                RecordingWorkspaceServiceProxy>();
            Workspace = (RecordingWorkspaceServiceProxy)(object)workspaceService;
            context.Services.AddSingleton(avatarGenerationService);
            context.Services.AddSingleton(workspaceService);
            context.Services.AddSingleton<IAvatarGenerationGateway>(
                new AgentAvatarGenerationGateway(workspaceService, avatarGenerationService));
            context.Services.AddSingleton(
                (ProjectsService)RuntimeHelpers.GetUninitializedObject(typeof(ProjectsService)));
            context.Services.AddSingleton(
                (SecretService)RuntimeHelpers.GetUninitializedObject(typeof(SecretService)));

            DialogService = context.Services.GetRequiredService<DialogService>();
            Host = context.Render<DialogHost>();
        }

        public RecordingWorkspaceServiceProxy Workspace { get; }

        public DialogService DialogService { get; }

        public IRenderedComponent<DialogHost> Host { get; }

        public Task<object?> OpenDetails(
            AgentEditorModel editor,
            Action<AgentDetailsDialogResult>? saved = null)
        {
            var parameters = new Dictionary<string, object?>
            {
                [nameof(TestAgentDetailsDialog.TestEditor)] = editor
            };
            if (saved is not null)
            {
                parameters[nameof(AgentDetailsDialog.Saved)] =
                    EventCallback.Factory.Create(this, saved);
            }

            var result = DialogService.OpenAsync<TestAgentDetailsDialog>(
                "Agent details",
                parameters,
                new DialogOptions
                {
                    TestId = "agents-details-dialog"
                });
            Host.WaitForElement(DeleteButtonSelector);
            return result;
        }

        public Task ClickDeleteAsync()
        {
            return Host
                .Find(DeleteButtonSelector)
                .ClickAsync(new MouseEventArgs());
        }

        public async Task CloseDetailsAsync(Task<object?> detailsTask)
        {
            await DialogService.CloseAsync();
            await detailsTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public void Dispose()
        {
            context.Dispose();
        }
    }
}
