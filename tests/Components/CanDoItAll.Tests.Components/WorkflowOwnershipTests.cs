using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using System.Reflection;
using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.UI;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class WorkflowOwnershipTests {
    public enum DelayedReadOutcome { Success, Failure, Cancellation }

    public enum DelayedReadLane { Catalog, Definition, RunPage, RunDetail }

    public static IEnumerable<object[]> ReadLifetimes() {
        foreach (var lane in Enum.GetValues<DelayedReadLane>()) {
            foreach (var outcome in Enum.GetValues<DelayedReadOutcome>()) {
                yield return [lane, outcome, false];
                yield return [lane, outcome, true];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ReadLifetimes))]
    public async Task Each_workflow_read_lane_retains_a_canceled_token_until_its_operation_finishes(
        DelayedReadLane lane, DelayedReadOutcome outcome, bool dispose) {
        await using var fixture = await Fixture.CreateAsync();
        var catalog = fixture.Harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
        var catalogRows = await catalog.ListDefinitionsAsync();
        var firstDetail = await catalog.GetDefinitionAsync(fixture.First.Id);
        var secondDetail = await catalog.GetDefinitionAsync(fixture.Second.Id);
        var entered = new TaskCompletionSource<CancellationToken>();
        var release = new TaskCompletionSource();
        Exception? tokenFailure = null;
        var callbacks = 0;
        var signaled = false;
        var calls = 0;
        async Task<T> Read<T>(T result, CancellationToken token) {
            if (++calls != 1) {
                return result;
            }
            entered.SetResult(token);
            await release.Task;
            tokenFailure = Record.Exception(() => {
                using var registration = token.Register(() => callbacks++);
                signaled = token.WaitHandle.WaitOne(0);
            });
            return outcome switch {
                DelayedReadOutcome.Success => result,
                DelayedReadOutcome.Failure => throw new InvalidOperationException("PRIVATE_DELAYED_LANE_FAILURE"),
                _ => throw new OperationCanceledException(token)
            };
        }
        Task old;
        switch (lane) {
            case DelayedReadLane.Catalog:
                fixture.Probe.Catalog = token => Read(catalogRows, token);
                old = fixture.Emit(WorkflowAction.Refresh);
                break;
            case DelayedReadLane.Definition:
                fixture.Probe.Definition = (id, token) => Read(id == fixture.First.Id ? firstDetail : secondDetail, token);
                old = fixture.Select(fixture.Second);
                break;
            case DelayedReadLane.RunPage:
                fixture.Probe.RunPage = (request, token) => Read(new WorkflowListPage<WorkflowRunSnapshot>(
                    [request.WorkflowId == fixture.First.Id ? fixture.FirstRun : fixture.SecondRun], 0, 8, 1), token);
                old = fixture.Tab(WorkflowTab.History);
                break;
            default:
                await fixture.Tab(WorkflowTab.History);
                fixture.Probe.Events = (_, token) => Read<IReadOnlyList<WorkflowEventRecord>>([], token);
                old = fixture.Emit(WorkflowAction.RunDetails, fixture.FirstRun.RunId.Value);
                break;
        }
        var captured = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (dispose) {
            await fixture.Cut.InvokeAsync(fixture.Cut.Instance.Dispose);
        } else if (lane == DelayedReadLane.Catalog) {
            await fixture.Emit(WorkflowAction.Refresh);
        } else {
            await fixture.Select(lane == DelayedReadLane.Definition ? fixture.First : fixture.Second);
        }
        Assert.True(captured.IsCancellationRequested);
        release.SetResult();
        await old;
        Assert.Null(tokenFailure);
        Assert.Equal(1, callbacks);
        Assert.True(signaled);
        Assert.Throws<ObjectDisposedException>(() => {
            _ = captured.WaitHandle;
        });
        Assert.DoesNotContain("PRIVATE_DELAYED_LANE_FAILURE", fixture.Cut.Markup, StringComparison.Ordinal);
        if (!dispose) {
            Assert.False(fixture.Surface.Presentation.IsLoading);
            Assert.Equal((lane is DelayedReadLane.Definition or DelayedReadLane.Catalog ? fixture.First : fixture.Second).Id.Value,
                fixture.Surface.Catalog.Definition!.Id);
        }
    }

    [Fact]
    public async Task Run_and_event_details_hide_internal_data_and_preserve_output_artifacts_and_editable_human_response() {
        await using var fixture = await Fixture.CreateAsync();
        var run = fixture.FirstRun;
        var failedEvent = Event(run.RunId, "InvalidOperationException: PRIVATE_SECRET_482\n   at PRIVATE_STACK_482() in C:\\PRIVATE_PATH_482\\file.cs:line 7") with {
            Kind = WorkflowEventKind.ExecutorFailed,
            PayloadJson = "{\"exception\":\"PRIVATE_ENVELOPE_482\"}"
        };
        var output = Event(run.RunId, "Safe output emitted") with {
            Kind = WorkflowEventKind.Output,
            PayloadJson = "{\"result\":\"Useful <script>encoded output</script>\"}"
        };
        var internalEvent = Event(run.RunId, "Safe runtime summary") with {
            PayloadJson = JsonSerializer.Serialize(new WorkflowEventPayloadEnvelope(WorkflowEventPayloadSource.Runtime, "UnknownInternal",
                null, null, null, null, "PRIVATE_INTERNAL_482", 20, false, "C:\\PRIVATE_REFERENCE_482"), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        await fixture.Store.SaveEventAsync(failedEvent);
        await fixture.Store.SaveEventAsync(output);
        await fixture.Store.SaveEventAsync(internalEvent);
        await fixture.Store.SaveArtifactAsync(new(WorkflowArtifactId.New(), run.RunId, WorkflowArtifactKind.Text, null,
            "Useful artifact", "text/plain", "  artifacts\\safe.txt  ", "Useful artifact summary", run.CreatedAtUtc));
        await fixture.Store.SaveArtifactAsync(new(WorkflowArtifactId.New(), run.RunId, WorkflowArtifactKind.Text, null,
            "Internal artifact", "text/plain", "  C:\\PRIVATE_STORAGE_482\\file.txt  ", "Retained artifact summary", run.CreatedAtUtc));
        var request = Request(run.RunId) with { RequestJson = "{\"question\":\"<b>Continue?</b>\"}" };
        fixture.Probe.Pending = _ => Task.FromResult<IReadOnlyList<WorkflowExternalRequestRecord>>([request]);
        await fixture.Tab(WorkflowTab.History);
        await fixture.Emit(WorkflowAction.SelectRun, run.RunId.Value);
        Assert.Contains("Continue?", fixture.Cut.Markup, StringComparison.Ordinal);
        await fixture.Emit(WorkflowAction.ResponseChanged, request.Id.Value, "{\"approved\":false}");
        Assert.Equal("{\"approved\":false}", Assert.Single(fixture.Surface.History.Requests).ResponseJson);
        await fixture.Emit(WorkflowAction.ResponseChanged, request.Id.Value, new string('a', WorkflowRequestView.MaximumJsonLength + 1));
        Assert.Equal("{\"approved\":false}", Assert.Single(fixture.Surface.History.Requests).ResponseJson);
        await fixture.Emit(WorkflowAction.RunDetails, run.RunId.Value);
        var detail = fixture.Cut.Find("[data-testid='workflows-run-detail-dialog']");
        Assert.Contains("Useful <script>encoded output</script>", detail.TextContent, StringComparison.Ordinal);
        Assert.Contains("artifacts/safe.txt", detail.TextContent, StringComparison.Ordinal);
        Assert.Contains("Internal artifact", detail.TextContent, StringComparison.Ordinal);
        Assert.Contains("Retained artifact summary", detail.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_", fixture.Cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Technical details", detail.TextContent, StringComparison.Ordinal);
        Assert.Empty(detail.QuerySelectorAll("script"));
        Assert.Contains("UTC", detail.TextContent, StringComparison.Ordinal);
        detail.QuerySelector("[aria-label='Close']")!.Click();
        await fixture.Emit(WorkflowAction.EventDetails, failedEvent.Id);
        Assert.DoesNotContain("PRIVATE_", fixture.Cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Technical details", fixture.Cut.Find("[data-testid='workflows-event-detail-dialog']").TextContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DelayedReadOutcome.Success, false)]
    [InlineData(DelayedReadOutcome.Failure, false)]
    [InlineData(DelayedReadOutcome.Cancellation, false)]
    [InlineData(DelayedReadOutcome.Success, true)]
    [InlineData(DelayedReadOutcome.Failure, true)]
    [InlineData(DelayedReadOutcome.Cancellation, true)]
    public async Task Superseded_event_read_owns_its_token_until_delayed_completion(DelayedReadOutcome outcome, bool dispose) {
        await using var fixture = await Fixture.CreateAsync();
        var other = fixture.FirstRun with { RunId = new(Guid.NewGuid()), Summary = "current run" };
        await fixture.Store.SaveRunAsync(other);
        await fixture.Tab(WorkflowTab.History);
        var entered = new TaskCompletionSource<CancellationToken>();
        var release = new TaskCompletionSource();
        Exception? tokenFailure = null;
        var callbacks = 0;
        var signaled = false;
        fixture.Probe.EventPage = async (request, token) => {
            if (request.RunId != fixture.FirstRun.RunId) {
                return new([Event(other.RunId, "current event")], 0, 8, 1);
            }
            entered.SetResult(token);
            await release.Task;
            tokenFailure = Record.Exception(() => {
                using var registration = token.Register(() => callbacks++);
                signaled = token.WaitHandle.WaitOne(0);
            });
            return outcome switch {
                DelayedReadOutcome.Success => new([Event(fixture.FirstRun.RunId, "obsolete event")], 4, 8, 99),
                DelayedReadOutcome.Failure => throw new InvalidOperationException("PRIVATE_DELAYED_READ_FAILURE"),
                _ => throw new OperationCanceledException(token)
            };
        };
        var old = fixture.Emit(WorkflowAction.SelectRun, fixture.FirstRun.RunId.Value);
        var captured = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (dispose) {
            await fixture.Cut.InvokeAsync(fixture.Cut.Instance.Dispose);
        } else {
            await fixture.Emit(WorkflowAction.SelectRun, other.RunId.Value);
        }
        Assert.True(captured.IsCancellationRequested);
        release.SetResult();
        await old;
        Assert.Null(tokenFailure);
        Assert.Equal(1, callbacks);
        Assert.True(signaled);
        Assert.Throws<ObjectDisposedException>(() => {
            _ = captured.WaitHandle;
        });
        Assert.DoesNotContain("obsolete event", fixture.Cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_DELAYED_READ_FAILURE", fixture.Cut.Markup, StringComparison.Ordinal);
        if (!dispose) {
            Assert.Equal(other.RunId.Value, fixture.Surface.History.SelectedRun!.Id);
            Assert.Equal("current event", Assert.Single(fixture.Surface.History.Events).Summary);
            Assert.Equal(0, fixture.Surface.History.EventPage.Index);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Detached_test_keeps_its_definition_and_input_and_cannot_replace_a_newer_test(bool failOld) {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Tab(WorkflowTab.History);
        var first = new TaskCompletionSource<WorkflowTestRunResult>();
        var second = new TaskCompletionSource<WorkflowTestRunResult>();
        var requests = new List<WorkflowTestRunRequest>();
        fixture.Probe.Test = request => {
            requests.Add(request);
            return requests.Count == 1 ? first.Task : second.Task;
        };
        await fixture.Emit(WorkflowAction.TestInputChanged, text: "{\"marker\":\"first\"}");
        var old = fixture.Emit(WorkflowAction.RunTest);
        fixture.Cut.WaitForAssertion(() => Assert.Single(requests));
        await fixture.Emit(WorkflowAction.RunTest);
        Assert.Single(requests);
        await fixture.Select(fixture.Second);
        await fixture.Emit(WorkflowAction.TestInputChanged, text: "{\"marker\":\"second\"}");
        var current = fixture.Emit(WorkflowAction.RunTest);
        fixture.Cut.WaitForAssertion(() => Assert.Equal(2, requests.Count));
        Assert.Equal(fixture.First.Id, requests[0].WorkflowId);
        Assert.Equal(fixture.Second.Id, requests[1].WorkflowId);
        Assert.Contains("first", requests[0].InputJson, StringComparison.Ordinal);
        Assert.Contains("second", requests[1].InputJson, StringComparison.Ordinal);
        if (failOld) {
            first.SetException(new InvalidOperationException("PRIVATE_OLD_FAILURE"));
        } else {
            first.SetResult(Result());
        }
        await old;
        Assert.True(fixture.Surface.History.IsRunningTest);
        Assert.Null(fixture.Surface.History.TestSucceeded);
        Assert.DoesNotContain("PRIVATE_OLD_FAILURE", fixture.Cut.Markup, StringComparison.Ordinal);
        second.SetResult(Result());
        await current;
        Assert.False(fixture.Surface.History.IsRunningTest);
        Assert.True(fixture.Surface.History.TestSucceeded);
    }

    [Fact]
    public async Task Event_page_is_accepted_only_after_success_and_failed_next_page_keeps_the_previous_label() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Tab(WorkflowTab.History);
        fixture.Probe.EventPage = (request, _) => Task.FromResult(new WorkflowListPage<WorkflowEventRecord>(
            [Event(fixture.FirstRun.RunId, "accepted-page")], request.PageIndex, request.PageSize, 17));
        await fixture.Emit(WorkflowAction.SelectRun, fixture.FirstRun.RunId.Value);
        var pending = new TaskCompletionSource<WorkflowListPage<WorkflowEventRecord>>();
        fixture.Probe.EventPage = (_, _) => pending.Task;
        var load = fixture.Emit(WorkflowAction.EventPage, delta: 1);
        Assert.Equal(0, fixture.Surface.History.EventPage.Index);
        Assert.Contains(fixture.Surface.History.Events, item => item.Summary == "accepted-page");
        pending.SetException(new InvalidOperationException("PRIVATE_PAGE_FAILURE"));
        await load;
        Assert.Equal(0, fixture.Surface.History.EventPage.Index);
        Assert.Equal(17, fixture.Surface.History.EventPage.TotalCount);
        Assert.Equal("accepted-page", Assert.Single(fixture.Surface.History.Events).Summary);
        Assert.DoesNotContain("PRIVATE_PAGE_FAILURE", fixture.Cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_event_page_and_failure_cannot_replace_another_run(bool failOld) {
        await using var fixture = await Fixture.CreateAsync();
        var other = fixture.FirstRun with { RunId = new(Guid.NewGuid()), Summary = "second run" };
        await fixture.Store.SaveRunAsync(other);
        await fixture.Tab(WorkflowTab.History);
        var pending = new TaskCompletionSource<WorkflowListPage<WorkflowEventRecord>>();
        CancellationToken firstToken = default;
        fixture.Probe.EventPage = (request, token) => {
            if (request.RunId == fixture.FirstRun.RunId) {
                firstToken = token;
                return pending.Task;
            }
            return Task.FromResult(new WorkflowListPage<WorkflowEventRecord>([Event(other.RunId, "second event")], 0, 8, 1));
        };
        var old = fixture.Emit(WorkflowAction.SelectRun, fixture.FirstRun.RunId.Value);
        fixture.Cut.WaitForAssertion(() => Assert.True(firstToken.CanBeCanceled));
        await fixture.Emit(WorkflowAction.SelectRun, other.RunId.Value);
        Assert.True(firstToken.IsCancellationRequested);
        if (failOld) {
            pending.SetException(new InvalidOperationException("PRIVATE_EVENTS"));
        } else {
            pending.SetResult(new([Event(fixture.FirstRun.RunId, "old event")], 2, 8, 22));
        }
        await old;
        Assert.Equal(other.RunId.Value, fixture.Surface.History.SelectedRun!.Id);
        Assert.Equal("second event", Assert.Single(fixture.Surface.History.Events).Summary);
        Assert.Equal(0, fixture.Surface.History.EventPage.Index);
    }

    [Fact]
    public async Task Run_detail_close_and_disposal_cancel_owned_reads_without_closing_an_unrelated_dialog() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Tab(WorkflowTab.History);
        var pending = new TaskCompletionSource<IReadOnlyList<WorkflowEventRecord>>();
        CancellationToken token = default;
        fixture.Probe.Events = (_, cancellation) => {
            token = cancellation;
            return pending.Task;
        };
        var load = fixture.Emit(WorkflowAction.RunDetails, fixture.FirstRun.RunId.Value);
        fixture.Cut.WaitForAssertion(() => Assert.True(token.CanBeCanceled));
        var dialogs = fixture.Harness.Context.Services.GetRequiredService<DialogService>();
        var unrelated = dialogs.OpenAsync("Independent", _ => builder => builder.AddContent(0, "Independent dialog"));
        var count = dialogs.Dialogs.Count;
        await fixture.Cut.InvokeAsync(fixture.Cut.Instance.Dispose);
        Assert.True(token.IsCancellationRequested);
        pending.SetResult([Event(fixture.FirstRun.RunId, "late detail")]);
        await load;
        Assert.Equal(count, dialogs.Dialogs.Count);
        dialogs.Close();
        await unrelated;
    }

    [Fact]
    public async Task Human_response_captures_request_version_and_draft_and_cannot_refresh_a_different_run() {
        await using var fixture = await Fixture.CreateAsync();
        var requestA = Request(fixture.FirstRun.RunId);
        var requestB = Request(fixture.FirstRun.RunId);
        fixture.Probe.Pending = _ => Task.FromResult<IReadOnlyList<WorkflowExternalRequestRecord>>([requestA, requestB]);
        await fixture.Tab(WorkflowTab.History);
        await fixture.Emit(WorkflowAction.ResponseChanged, requestA.Id.Value, text: "{\"answer\":\"A\"}");
        await fixture.Emit(WorkflowAction.ResponseChanged, requestB.Id.Value, text: "{\"answer\":\"B\"}");
        Assert.Contains("A", fixture.Surface.History.Requests[0].ResponseJson, StringComparison.Ordinal);
        Assert.Contains("B", fixture.Surface.History.Requests[1].ResponseJson, StringComparison.Ordinal);
        var pending = new TaskCompletionSource<WorkflowExternalResponseServiceResult>();
        var submitted = new List<WorkflowExternalResponseCommand>();
        fixture.Probe.Submit = command => {
            submitted.Add(command);
            return pending.Task;
        };
        var old = fixture.Emit(WorkflowAction.Respond, requestA.Id.Value);
        fixture.Cut.WaitForAssertion(() => Assert.Single(submitted));
        await fixture.Emit(WorkflowAction.Respond, requestA.Id.Value);
        Assert.Single(submitted);
        Assert.Equal(requestA.Id, submitted[0].RequestId);
        Assert.Equal(requestA.Version, submitted[0].ExpectedRequestVersion);
        Assert.Equal("A", submitted[0].Response.GetProperty("answer").GetString());
        await fixture.Select(fixture.Second);
        pending.SetResult(new(WorkflowExternalResponseServiceOutcome.Completed, null, fixture.FirstRun, requestA, null, false, "OLD_RESPONSE"));
        await old;
        Assert.Equal(fixture.Second.Id.Value, fixture.Surface.Catalog.Definition!.Id);
        Assert.Equal(fixture.SecondRun.RunId.Value, fixture.Surface.History.SelectedRun!.Id);
        Assert.DoesNotContain("OLD_RESPONSE", fixture.Cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Definition_target_changes_discard_obsolete_intents_and_missing_route_fails_closed() {
        await using var fixture = await Fixture.CreateAsync();
        var obsolete = new WorkflowIntent(fixture.Surface.Presentation.Revision, WorkflowAction.TestInputChanged, Text: "obsolete");
        await fixture.Select(fixture.Second);
        await fixture.Cut.InvokeAsync(() => fixture.Surface.Intent.InvokeAsync(obsolete));
        Assert.DoesNotContain("obsolete", fixture.Surface.History.TestInputJson, StringComparison.Ordinal);
        var navigation = fixture.Harness.Context.Services.GetRequiredService<NavigationManager>();
        await fixture.Cut.InvokeAsync(() => navigation.NavigateTo($"/agents/workflows?workflowId={Guid.NewGuid():D}"));
        fixture.Cut.WaitForAssertion(() => {
            Assert.Null(fixture.Surface.Catalog.Definition);
            Assert.Null(fixture.Surface.History.SelectedRun);
            Assert.NotEmpty(fixture.Surface.Presentation.ErrorMessage);
        });
    }

    [Fact]
    public async Task Accepted_history_owns_its_collections_and_same_route_echo_does_not_reload() {
        await using var fixture = await Fixture.CreateAsync();
        var rows = new List<WorkflowRunSnapshot> { fixture.FirstRun };
        var calls = 0;
        fixture.Probe.RunPage = (request, _) => {
            calls++;
            return Task.FromResult(new WorkflowListPage<WorkflowRunSnapshot>(rows, 0, 8, 1));
        };
        await fixture.Tab(WorkflowTab.History);
        rows.Clear();
        await fixture.Emit(WorkflowAction.TestInputChanged, text: "{} ");
        Assert.Single(fixture.Surface.History.Runs);
        var navigation = fixture.Harness.Context.Services.GetRequiredService<NavigationManager>();
        await fixture.Cut.InvokeAsync(() => navigation.NavigateTo(navigation.Uri));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Template_selection_supersedes_old_preview_intents_without_backend_writes() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Tab(WorkflowTab.Catalog);
        var definitionCount = fixture.Surface.Catalog.DefinitionCount;
        await fixture.Emit(WorkflowAction.OpenTemplates);
        var surface = fixture.Cut.FindComponent<WorkflowTemplateCatalogSurface>().Instance;
        var first = surface.Presentation.Templates[0];
        var second = surface.Presentation.Templates[1];
        var obsoletePreview = new WorkflowIntent(surface.Presentation.Revision, WorkflowAction.PreviewTemplate, TemplateKey: first.Key);
        await fixture.Cut.InvokeAsync(() => surface.Intent.InvokeAsync(new(surface.Presentation.Revision, WorkflowAction.SelectTemplate, TemplateKey: second.Key)));
        await fixture.Cut.InvokeAsync(() => surface.Intent.InvokeAsync(obsoletePreview));
        Assert.Empty(fixture.Cut.FindAll("[data-testid='workflows-template-preview-dialog']"));
        Assert.Equal(second.Key, surface.Presentation.Selected!.Key);
        await fixture.Cut.InvokeAsync(() => surface.Intent.InvokeAsync(new(surface.Presentation.Revision, WorkflowAction.PreviewTemplate, TemplateKey: second.Key)));
        Assert.Contains(second.Name, fixture.Cut.Find("[data-testid='workflows-template-preview-dialog']").TextContent, StringComparison.Ordinal);
        Assert.Equal(definitionCount, fixture.Surface.Catalog.DefinitionCount);
    }

    [Fact]
    public async Task Curator_launch_keeps_exact_identity_and_one_flight_across_workflow_changes() {
        await using var fixture = await Fixture.CreateAsync();
        var pending = new TaskCompletionSource<ActiveAgentChat>();
        var ids = new List<Guid>();
        fixture.Probe.Launch = id => {
            ids.Add(id);
            return pending.Task;
        };
        fixture.Cut.WaitForAssertion(() => Assert.False(fixture.Cut.Find("[data-testid='workflows-curator-open']").HasAttribute("disabled")));
        var launch = fixture.Cut.Find("[data-testid='workflows-curator-open']").ClickAsync(new());
        fixture.Cut.WaitForAssertion(() => Assert.Single(ids));
        await fixture.Select(fixture.Second);
        Assert.True(fixture.Cut.Find("[data-testid='workflows-curator-open']").HasAttribute("disabled"));
        Assert.Equal(WorkflowCuratorAgentIdentity.AgentId, ids[0]);
        var now = DateTimeOffset.UtcNow;
        pending.SetResult(new(AgentChatHandleId.Create(), new(ids[0], "Curator", "Workflow specialist", null), null,
            ActiveAgentChatVisibility.Visible, ActiveAgentChatRunState.Idle, now, now, null));
        await launch;
        Assert.Single(ids);
        Assert.Equal(fixture.Second.Id.Value, fixture.Surface.Catalog.Definition!.Id);
    }

    private static WorkflowTestRunResult Result() => new(true, new([]), null, [], [], [], "");
    private static WorkflowEventRecord Event(WorkflowRunId runId, string message)
        => new(Guid.NewGuid(), runId, WorkflowEventKind.SuperStep, null, message, "{}", DateTimeOffset.UtcNow);
    private static WorkflowExternalRequestRecord Request(WorkflowRunId runId)
        => new(new(Guid.NewGuid()), runId, WorkflowExternalRequestKind.HumanInput, new("review"), "review", "{}", "", DateTimeOffset.UtcNow, null);

    private sealed class Fixture : IAsyncDisposable {
        public required CanDoItAllTestEnvironment Environment { get; init; }
        public required ComponentTestHarness Harness { get; init; }
        public required IRenderedComponent<WorkflowsPage> Cut { get; init; }
        public required Probe Probe { get; init; }
        public required IWorkflowRunStore Store { get; init; }
        public required WorkflowDefinition First { get; init; }
        public required WorkflowDefinition Second { get; init; }
        public required WorkflowRunSnapshot FirstRun { get; init; }
        public required WorkflowRunSnapshot SecondRun { get; init; }
        public WorkflowShellSurface Surface => Cut.FindComponent<WorkflowShellSurface>().Instance;

        public static async Task<Fixture> CreateAsync() {
            var environment = CanDoItAllTestEnvironment.Create("workflow-final-owner");
            var probe = new Probe();
            var harness = await WorkflowsPageTests.CreateInMemoryWorkflowHarnessAsync(environment, services => {
                Decorate<IWorkflowCatalogService>(services, probe);
                Decorate<IWorkflowRunStore>(services, probe);
                Decorate<IWorkflowRuntimeManager>(services, probe);
                Decorate<IWorkflowTestRunner>(services, probe);
                Decorate<IWorkflowExternalResponseService>(services, probe);
                Decorate<IAgentChatLauncher>(services, probe);
            });
            var catalog = harness.Context.Services.GetRequiredService<IWorkflowCatalogService>();
            var first = await WorkflowsPageTests.CreateHistoryDefinitionAsync(catalog);
            var second = await WorkflowsPageTests.CreateHistoryDefinitionAsync(catalog);
            var store = harness.Context.Services.GetRequiredService<IWorkflowRunStore>();
            var firstRun = Run(first);
            var secondRun = Run(second);
            await store.SaveRunAsync(firstRun);
            await store.SaveRunAsync(secondRun);
            harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/agents/workflows?workflowId={first.Id.Value:D}");
            var cut = harness.Context.Render<WorkflowsPage>();
            cut.WaitForAssertion(() => Assert.False(cut.FindComponent<WorkflowShellSurface>().Instance.Presentation.IsLoading));
            return new() { Environment = environment, Harness = harness, Cut = cut, Probe = probe, Store = store,
                First = first, Second = second, FirstRun = firstRun, SecondRun = secondRun };
        }

        public Task Emit(WorkflowAction action, Guid? id = null, string? text = null, int delta = 0)
            => Cut.InvokeAsync(() => Surface.Intent.InvokeAsync(new(Surface.Presentation.Revision, action, id, text, delta)));
        public Task Select(WorkflowDefinition definition) => Cut.InvokeAsync(() => Surface.Intent.InvokeAsync(new(
            Surface.Presentation.Revision, WorkflowAction.SelectDefinition,
            TreeKey: new(WorkflowDefinitionTreeNodeBuilder.BuildDefinitionNodeId(definition.Id)))));
        public Task Tab(WorkflowTab tab) => Cut.InvokeAsync(() => Surface.Intent.InvokeAsync(new(
            Surface.Presentation.Revision, WorkflowAction.ChangeTab, Tab: tab)));
        public async ValueTask DisposeAsync() {
            await Harness.DisposeAsync();
            await Environment.DisposeAsync();
        }
        private static WorkflowRunSnapshot Run(WorkflowDefinition definition) => new(new(Guid.NewGuid()), definition.Id, definition.VersionId,
            WorkflowRunState.Completed, WorkflowRuntimeBackendKind.InProcess, "sample-run", "Completed sample", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private static void Decorate<T>(IServiceCollection services, Probe probe) where T : class {
        var descriptor = services.Last(item => item.ServiceType == typeof(T));
        services.RemoveAll<T>();
        services.AddScoped<T>(provider => {
            var inner = descriptor.ImplementationInstance ?? descriptor.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
            var service = DispatchProxy.Create<T, ReadProxy>();
            var proxy = (ReadProxy)(object)service;
            proxy.Inner = inner;
            proxy.Probe = probe;
            return service;
        });
    }

    public sealed class Probe {
        public Func<CancellationToken, Task<IReadOnlyList<WorkflowCatalogItem>>>? Catalog { get; set; }
        public Func<WorkflowId, CancellationToken, Task<WorkflowDefinitionDetail?>>? Definition { get; set; }
        public Func<Guid, Task<ActiveAgentChat>>? Launch { get; set; }
        public Func<WorkflowTestRunRequest, Task<WorkflowTestRunResult>>? Test { get; set; }
        public Func<WorkflowEventPageRequest, CancellationToken, Task<WorkflowListPage<WorkflowEventRecord>>>? EventPage { get; set; }
        public Func<WorkflowRunPageRequest, CancellationToken, Task<WorkflowListPage<WorkflowRunSnapshot>>>? RunPage { get; set; }
        public Func<WorkflowRunId, CancellationToken, Task<IReadOnlyList<WorkflowEventRecord>>>? Events { get; set; }
        public Func<WorkflowRunId, Task<IReadOnlyList<WorkflowExternalRequestRecord>>>? Pending { get; set; }
        public Func<WorkflowExternalResponseCommand, Task<WorkflowExternalResponseServiceResult>>? Submit { get; set; }
    }

    public class ReadProxy : DispatchProxy {
        public object Inner { get; set; } = default!;
        public Probe Probe { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments) {
            var method = targetMethod ?? throw new InvalidOperationException("Missing workflow method.");
            var args = arguments ?? [];
            return method.Name switch {
                nameof(IWorkflowCatalogService.ListDefinitionsAsync) when Probe.Catalog is { } catalog => catalog(args.OfType<CancellationToken>().Single()),
                nameof(IWorkflowCatalogService.GetDefinitionAsync) when Probe.Definition is { } definition => definition((WorkflowId)args[0]!, args.OfType<CancellationToken>().Single()),
                nameof(IAgentChatLauncher.StartNewChatAsync) when Probe.Launch is { } launch => launch((Guid)args[0]!),
                nameof(IWorkflowTestRunner.RunAsync) when Probe.Test is { } run => run((WorkflowTestRunRequest)args[0]!),
                nameof(IWorkflowRunStore.ListEventPageAsync) when Probe.EventPage is { } events => events((WorkflowEventPageRequest)args[0]!, (CancellationToken)args[1]!),
                nameof(IWorkflowRunStore.ListRunPageAsync) when Probe.RunPage is { } page => page((WorkflowRunPageRequest)args[0]!, (CancellationToken)args[1]!),
                nameof(IWorkflowRuntimeManager.ListEventsAsync) when Probe.Events is { } details => details((WorkflowRunId)args[0]!, (CancellationToken)args[1]!),
                nameof(IWorkflowRunStore.ListPendingExternalRequestsAsync) when Probe.Pending is { } pending => pending((WorkflowRunId)args[0]!),
                nameof(IWorkflowExternalResponseService.SubmitAsync) when Probe.Submit is { } submit => submit((WorkflowExternalResponseCommand)args[0]!),
                _ => method.Invoke(Inner, args)
            };
        }
    }
}
