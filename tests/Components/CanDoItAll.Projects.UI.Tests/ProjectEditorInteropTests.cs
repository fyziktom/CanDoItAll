using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectEditorInteropTests {
    private const string ModulePath = "./_content/CanDoItAll.Projects.UI/project-editor.js";

    [Theory]
    [InlineData(ImportCompletion.Success)]
    [InlineData(ImportCompletion.Failure)]
    [InlineData(ImportCompletion.Cancellation)]
    public async Task Pending_import_retires_without_waiting_and_never_inspects_or_dispatches_into_a_successor(ImportCompletion completion) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var runtime = new ControlledRuntime(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(runtime);
        context.Services.AddCanDoItAllBaseLib();
        var acquisition = new TaskCompletionSource<IJSObjectReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.Imports.Enqueue(acquisition.Task);
        var lateModule = new RecordingModule();
        var oldDraft = ProjectEditorSurfaceTests.NewDraft();
        int oldSaves = 0;
        var cut = ProjectEditorSurfaceTests.Render(context, oldDraft, () => oldSaves++);
        var pending = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        cut.WaitForAssertion(() => Assert.Equal(1, runtime.ImportCount));
        try {
            await context.DisposeRenderedComponentsAsync().WaitAsync(TimeSpan.FromSeconds(5));
            var currentModule = new RecordingModule();
            runtime.Imports.Enqueue(Task.FromResult<IJSObjectReference>(currentModule));
            int currentSaves = 0;
            var currentDraft = ProjectEditorSurfaceTests.NewDraft();
            var current = ProjectEditorSurfaceTests.Render(context, currentDraft, () => currentSaves++);
            await current.InvokeAsync(() => current.Find("form").SubmitAsync());
            Complete();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, oldSaves);
            Assert.Equal(0, lateModule.Inspections);
            Assert.Equal(0, lateModule.FocusCalls);
            Assert.Equal(completion == ImportCompletion.Success ? 1 : 0, lateModule.Disposals);
            Assert.Null(oldDraft.Message);
            Assert.Null(currentDraft.Message);
            Assert.Equal(1, currentSaves);
            var currentInstance = current.Instance;
            await context.DisposeRenderedComponentsAsync();
            await currentInstance.DisposeAsync();
            Assert.Equal(1, currentModule.Disposals);
            Assert.False(context.Renderer.UnhandledException.IsCompleted);
        } finally {
            Complete();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }

        void Complete() {
            switch (completion) {
                case ImportCompletion.Success:
                    acquisition.TrySetResult(lateModule);
                    break;
                case ImportCompletion.Failure:
                    acquisition.TrySetException(new JSException("Retired feature import failed."));
                    break;
                case ImportCompletion.Cancellation:
                    acquisition.TrySetCanceled();
                    break;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_inspection_or_focus_cannot_modify_or_save_a_replacement_draft(bool focus) {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var runtime = new ControlledRuntime(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(runtime);
        context.Services.AddCanDoItAllBaseLib();
        var heldInspection = new TaskCompletionSource<ProjectFormValidity>(TaskCreationOptions.RunContinuationsAsynchronously);
        var heldFocus = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new RecordingModule {
            Inspection = focus ? Task.FromResult(new ProjectFormValidity(false, 0, "Original invalid date")) : heldInspection.Task,
            Focus = heldFocus.Task
        };
        runtime.Imports.Enqueue(Task.FromResult<IJSObjectReference>(module));
        var oldDraft = ProjectEditorSurfaceTests.NewDraft();
        int saves = 0;
        var cut = ProjectEditorSurfaceTests.Render(context, oldDraft, () => saves++);
        var pending = cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        try {
            cut.WaitForAssertion(() => Assert.Equal(1, focus ? module.FocusCalls : module.Inspections));
            await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
            await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
            var successor = ProjectEditorSurfaceTests.NewDraft();
            await cut.InvokeAsync(() => cut.Render(p => p.Add(x => x.Draft, successor)));
            heldInspection.TrySetResult(new(false, 2, "Late old validation"));
            heldFocus.TrySetException(new JSException("Retired focus failed."));
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(successor.Message);
            Assert.Equal(0, saves);
            Assert.Equal(1, module.Disposals);
            Assert.False(context.Renderer.UnhandledException.IsCompleted);
        } finally {
            heldInspection.TrySetResult(new(true, null, null));
            heldFocus.TrySetResult();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task Active_cancellation_and_unexpected_module_cleanup_are_diagnosed_without_dispatch() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var runtime = new ControlledRuntime(context.JSInterop.JSRuntime);
        var logger = new RecordingLogger();
        context.Services.AddSingleton<IJSRuntime>(runtime);
        context.Services.AddSingleton<ILogger<ProjectModalHost>>(logger);
        context.Services.AddCanDoItAllBaseLib();
        var module = new RecordingModule {
            Inspection = Task.FromCanceled<ProjectFormValidity>(new CancellationToken(true)),
            DisposeFailure = new JSException("Injected release failure")
        };
        runtime.Imports.Enqueue(Task.FromResult<IJSObjectReference>(module));
        int saves = 0;
        var draft = ProjectEditorSurfaceTests.NewDraft();
        var cut = ProjectEditorSurfaceTests.Render(context, draft, () => saves++);
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Contains("Close and reopen", draft.Message);
        Assert.Equal(0, saves);
        var instance = cut.Instance;
        await context.DisposeRenderedComponentsAsync();
        await instance.DisposeAsync();
        Assert.Equal(1, module.Disposals);
        Assert.Equal(2, logger.Failures.Count);
        Assert.IsAssignableFrom<OperationCanceledException>(logger.Failures[0]);
        Assert.IsType<JSException>(logger.Failures[1]);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task Failed_import_is_reported_once_and_actual_teardown_allows_a_fresh_editor() {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var runtime = new ControlledRuntime(context.JSInterop.JSRuntime);
        context.Services.AddSingleton<IJSRuntime>(runtime);
        context.Services.AddCanDoItAllBaseLib();
        runtime.Imports.Enqueue(Task.FromException<IJSObjectReference>(new JSException("Injected feature-module import failure.")));
        int saves = 0;
        var draft = ProjectEditorSurfaceTests.NewDraft();
        var cut = ProjectEditorSurfaceTests.Render(context, draft, () => saves++);
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Equal(0, saves);
        Assert.Contains("Close and reopen", draft.Message);
        Assert.Equal("Project", draft.Model.Name);
        await context.DisposeRenderedComponentsAsync();
        Assert.False(context.Renderer.UnhandledException.IsCompleted, "A handled import error escaped during actual modal retirement.");

        var module = new RecordingModule();
        runtime.Imports.Enqueue(Task.FromResult<IJSObjectReference>(module));
        var fresh = ProjectEditorSurfaceTests.Render(context, ProjectEditorSurfaceTests.NewDraft(), () => saves++);
        await fresh.InvokeAsync(() => fresh.Find("form").SubmitAsync());
        Assert.Equal(1, saves);
        Assert.Equal(1, module.Inspections);
        await context.DisposeRenderedComponentsAsync();
        Assert.Equal(1, module.Disposals);
        Assert.False(context.Renderer.UnhandledException.IsCompleted);
    }

    private sealed class ControlledRuntime(IJSRuntime fallback) : IJSRuntime {
        public Queue<Task<IJSObjectReference>> Imports { get; } = new();
        public int ImportCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            if (identifier == "import" && args is [ModulePath]) {
                ImportCount++;
                return (TValue)(object)await Imports.Dequeue();
            }
            return await fallback.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }
    }

    private sealed class RecordingModule : IJSObjectReference {
        public int Inspections { get; private set; }
        public int Disposals { get; private set; }
        public int FocusCalls { get; private set; }
        public Task<ProjectFormValidity> Inspection { get; init; } = Task.FromResult(new ProjectFormValidity(true, null, null));
        public Task Focus { get; init; } = Task.CompletedTask;
        public Exception? DisposeFailure { get; init; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            if (identifier == "inspect") {
                Inspections++;
                return (TValue)(object)await Inspection;
            }
            Assert.Equal("focusInvalid", identifier);
            FocusCalls++;
            await Focus;
            return default!;
        }

        public ValueTask DisposeAsync() {
            Disposals++;
            return DisposeFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(DisposeFailure);
        }
    }

    public enum ImportCompletion { Success, Failure, Cancellation }

    private sealed class RecordingLogger : ILogger<ProjectModalHost> {
        public List<Exception> Failures { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (exception is not null) {
                Failures.Add(exception);
            }
        }
    }
}
