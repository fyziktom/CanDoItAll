using System.Runtime.ExceptionServices;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Playwright;

internal sealed class AgentChatSubmissionBrowserHost : IAsyncDisposable {
    private readonly CanDoItAllTestEnvironment environment = CanDoItAllTestEnvironment.Create("agent-chat-submission-browser");
    private NativeApplication? app;
    private HttpClient? client;
    private Task? disposal;
    internal SubmissionControl Control { get; } = new();
    internal Guid AgentId { get; private set; }
    internal ChatSessionRecord OriginalThread { get; private set; } = null!;
    internal ChatSessionRecord OtherThread { get; private set; } = null!;
    internal string BaseUrl => app!.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
    internal string Evidence { get; } = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright",
        "agent-chat-submission-retention", Guid.NewGuid().ToString("N"));

    internal static async Task<AgentChatSubmissionBrowserHost> StartAsync() {
        var host = new AgentChatSubmissionBrowserHost();
        try {
            Directory.CreateDirectory(host.Evidence);
            var profile = host.environment.CreatePostgreSqlProfile("native");
            await using (var seed = await TestApplicationBootstrap.BuildServiceProviderAsync(
                    profile, "AgentChatSubmissionBrowserSeed", TestSchemaBootstrapModules.Full)) {
                await using var scope = seed.CreateAsyncScope();
                var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
                host.AgentId = await workspace.SaveAgentAsync(new AgentEditorModel {
                    Name = "Retained submission browser Agent", Status = AgentLifecycleStatus.Active,
                    Instructions = "This owned fixture never enters a model or a tool.", SelectedCapabilityIds = []
                });
                var original = await workspace.GetOrCreateChatSessionAsync(host.AgentId);
                host.OriginalThread = await workspace.RenameChatSessionAsync(host.AgentId, original.Id, "Original retained thread", original.Title);
                var other = await workspace.GetOrCreateChatSessionAsync(host.AgentId);
                host.OtherThread = await workspace.RenameChatSessionAsync(host.AgentId, other.Id, "Other retained thread", other.Title);
            }
            host.app = new(profile, host.Control);
            host.app.UseKestrel(0);
            host.client = host.app.CreateClient(new() { AllowAutoRedirect = false });
            return host;
        } catch (Exception primary) {
            try {
                await host.DisposeAsync();
            } catch (Exception cleanup) {
                throw new AggregateException(primary, cleanup);
            }
            ExceptionDispatchInfo.Capture(primary).Throw();
            throw;
        }
    }

    internal async Task AssertNoRunOrMessageAsync() {
        await using var scope = app!.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        Assert.Empty(await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: AgentId)));
        Assert.Empty((await workspace.GetOrCreateChatSessionAsync(AgentId, OriginalThread.Id)).Messages);
        Assert.Empty((await workspace.GetOrCreateChatSessionAsync(AgentId, OtherThread.Id)).Messages);
    }

    public ValueTask DisposeAsync() => new(disposal ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync() {
        var failures = new List<Exception>();
        foreach (var release in new Func<Task>[] {
            Control.ReleaseAndDrainAsync,
            () => {
                client?.Dispose();
                return Task.CompletedTask;
            },
            () => app?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            () => environment.DisposeAsync().AsTask()
        }) {
            try {
                await release();
            } catch (Exception failure) {
                failures.Add(failure);
            }
        }
        if (failures.Count != 0) {
            throw new AggregateException("Owned chat browser fixture cleanup failed.", failures);
        }
    }

    private sealed class NativeApplication(TestDatabaseProfile profile, SubmissionControl control)
        : WebApplicationFactory<CanDoItAll.Web.Components.App> {
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "src", "App", "CanDoItAll.Web"));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(profile.CreateConfigurationValues(
                new Dictionary<string, string?> {
                    ["DevelopmentManager:TuningModeEnabled"] = "false",
                    ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false",
                    [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
                })));
            builder.ConfigureServices(services => {
                services.Replace(ServiceDescriptor.Scoped<IComponentActivator>(provider => new CapturingComponentActivator(provider, control)));
                services.Replace(ServiceDescriptor.Scoped<IAgentChatExecutionOrchestrator>(provider =>
                    new ControlledOrchestrator(control, provider.GetRequiredService<AgentChatSubmissionRetention>(),
                        provider.GetRequiredService<IAgentExecutionActivityCoordinator>(),
                        provider.GetRequiredService<IDatabaseProfileRuntimeAccessor>(),
                        provider.GetRequiredService<IAgentExecutionProfileGenerationSource>())));
            });
        }
    }

    private sealed class CapturingComponentActivator(IServiceProvider services, SubmissionControl control) : IComponentActivator {
        public IComponent CreateInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type componentType) {
            var component = (IComponent)ActivatorUtilities.CreateFactory(componentType, Type.EmptyTypes)(services, []);
            if (component is AgentChatPanel panel) {
                control.CapturePanel(panel);
            }
            return component;
        }
    }

    internal sealed class SubmissionControl {
        private readonly object gate = new();
        private readonly List<Attempt> attempts = [];
        private readonly List<AgentChatPanel> panels = [];
        private Task? draining;
        private int sendCalls;
        private TaskCompletionSource<Attempt> next = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private AgentChatSubmissionRetention? retention;
        private SynchronizationContext? nativeRenderer;
        internal int SendCount {
            get {
                lock (gate) {
                    return sendCalls;
                }
            }
        }
        internal AgentChatSubmissionRetention Retention {
            get {
                lock (gate) {
                    return retention ?? throw new InvalidOperationException("No native submission has entered.");
                }
            }
        }
        internal Task<Attempt> NextAsync() {
            lock (gate) {
                return next.Task;
            }
        }

        internal void CapturePanel(AgentChatPanel panel) {
            lock (gate) {
                panels.Add(panel);
            }
        }

        internal async Task WaitForDraftAsync(Guid agentId, Guid sessionId, string expected) {
            SynchronizationContext renderer;
            lock (gate) {
                renderer = nativeRenderer ?? throw new InvalidOperationException("No native renderer has submitted a prompt.");
            }
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var observations = new PeriodicTimer(TimeSpan.FromMilliseconds(25));
            do {
                var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                renderer.Post(_ => {
                    try {
                        AgentChatPanel[] captured;
                        lock (gate) {
                            captured = panels.ToArray();
                        }
                        var panel = captured.SingleOrDefault(value => !ReadField<bool>(value, "isDisposed") &&
                            ReadProperty<Guid?>(value, "selectedAgentId") == agentId &&
                            ReadProperty<Guid?>(value, "selectedSessionId") == sessionId);
                        observed.SetResult(panel is not null && ReadField<string>(panel, "draftPrompt") == expected);
                    } catch (Exception failure) {
                        observed.SetException(failure);
                    }
                }, null);
                if (await observed.Task.WaitAsync(deadline.Token)) {
                    return;
                }
            } while (await observations.WaitForNextTickAsync(deadline.Token));
        }

        internal AgentChatOperationHandle Begin(AgentChatSendRequest request, AgentChatSubmissionRetention current,
            IAgentExecutionActivityOperationLease lease, IAgentChatExecutionOrchestrator orchestrator) {
            var leaseOwnedByAttempt = false;
            try {
                lock (gate) {
                    sendCalls++;
                    if (attempts.Count == 3 || draining is not null) {
                        throw new InvalidOperationException("The native browser proof attempted an unplanned resend.");
                    }
                    if (retention is not null) {
                        Assert.Same(retention, current);
                    }
                    retention = current;
                    var panel = Assert.Single(panels.Where(value => ReferenceEquals(value.ChatExecutionOrchestrator, orchestrator) &&
                        !ReadField<bool>(value, "isDisposed") && ReadProperty<Guid?>(value, "selectedAgentId") == request.AgentId &&
                        ReadProperty<Guid?>(value, "selectedSessionId") == request.ChatSessionId && value.ActiveChatHandleId == request.ConversationHandleId));
                    var previous = ReadField<Task>(panel, "trackedChatOperation");
                    var renderer = SynchronizationContext.Current ?? throw new InvalidOperationException("The owned chat send requires its actual renderer context.");
                    nativeRenderer = renderer;
                    var captured = request with { AttachmentPaths = request.AttachmentPaths?.ToArray() };
                    var attempt = new Attempt(captured, lease);
                    leaseOwnedByAttempt = true;
                    attempts.Add(attempt);
                    try {
                        renderer.Post(_ => {
                            try {
                                var consumer = ReadField<Task>(panel, "trackedChatOperation");
                                if (ReferenceEquals(consumer, previous)) {
                                    throw new InvalidOperationException("The panel did not publish its actual submitted operation task.");
                                }
                                attempt.CaptureConsumer(consumer);
                            } catch (Exception failure) {
                                attempt.FailConsumerCapture(failure);
                            }
                        }, null);
                    } catch (Exception primary) {
                        attempt.FailConsumerCapture(primary);
                        try {
                            attempt.ReleaseForCleanup();
                        } catch (Exception cleanup) {
                            throw new AggregateException(primary, cleanup);
                        }
                        throw;
                    }
                    var observed = next;
                    next = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    observed.TrySetResult(attempt);
                    return new(lease.StreamId, attempt.Completion);
                }
            } catch (Exception primary) {
                if (leaseOwnedByAttempt) {
                    throw;
                }
                try {
                    lease.Dispose();
                } catch (Exception cleanup) {
                    throw new AggregateException(primary, cleanup);
                }
                throw;
            }
        }

        internal Task ReleaseAndDrainAsync() {
            lock (gate) {
                return draining ??= DrainCoreAsync(attempts.ToArray());
            }
        }

        private static async Task DrainCoreAsync(Attempt[] owned) {
            var failures = new List<Exception>();
            foreach (var attempt in owned) {
                try {
                    await attempt.WaitForConsumerCaptureAsync();
                } catch (Exception failure) {
                    failures.Add(failure);
                }
                try {
                    attempt.ReleaseForCleanup();
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            }
            foreach (var attempt in owned) {
                try {
                    await attempt.DrainAsync();
                } catch (Exception failure) {
                    failures.Add(failure);
                }
            }
            if (failures.Count != 0) {
                throw new AggregateException("Controlled native chat operations did not drain as arranged.", failures);
            }
        }

        private static T ReadField<T>(AgentChatPanel panel, string name) => (T)(typeof(AgentChatPanel)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(panel)
            ?? throw new InvalidOperationException($"The actual panel field {name} is unavailable."));

        private static T? ReadProperty<T>(AgentChatPanel panel, string name) => (T?)
            (typeof(AgentChatPanel).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"The actual panel property {name} is unavailable.")).GetValue(panel);
    }

    internal sealed class Attempt {
        private readonly TaskCompletionSource<Exception> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<Task> consumer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Exception? expectedFailure;
        internal AgentChatSendRequest Request { get; }
        internal AgentExecutionActivityStreamId Stream { get; }
        internal Task<AgentChatRunResult> Completion { get; }
        internal Attempt(AgentChatSendRequest request, IAgentExecutionActivityOperationLease lease) {
            Request = request;
            Stream = lease.StreamId;
            Completion = CompleteAsync(lease);
        }
        internal void RefuseBeforeAdmission() => Release(new AgentChatAdmissionConflictException(
            Request.AgentId, Request.ChatSessionId, new SandboxWorkspaceConcurrencyException(2, 3)));
        internal void LoseConfirmation() => Release(new AggregateException(
            new SandboxWorkspaceConcurrencyException(2, 3), new IOException("Owned fixture publication cleanup was not confirmed.")));
        internal void CaptureConsumer(Task operation) => consumer.SetResult(operation);
        internal void FailConsumerCapture(Exception failure) => consumer.SetException(failure);
        internal Task WaitForConsumerCaptureAsync() => consumer.Task;
        private void Release(Exception failure) {
            expectedFailure = failure;
            if (!release.TrySetResult(failure)) {
                throw new InvalidOperationException("This controlled operation was already released.");
            }
        }
        internal void ReleaseForCleanup() {
            if (!release.Task.IsCompleted) {
                Release(new OperationCanceledException("Owned browser fixture stopped."));
            }
        }
        private async Task<AgentChatRunResult> CompleteAsync(IAgentExecutionActivityOperationLease lease) {
            using (lease) {
                var failure = await release.Task.WaitAsync(TimeSpan.FromSeconds(90));
                lease.Fail("Controlled native chat outcome.");
                ExceptionDispatchInfo.Capture(failure).Throw();
                throw new InvalidOperationException("Unreachable controlled completion.");
            }
        }
        internal async Task DrainAsync() {
            var failures = new List<Exception>();
            try {
                await Completion;
                throw new InvalidOperationException("The controlled failure unexpectedly succeeded.");
            } catch (Exception failure) when (ReferenceEquals(failure, expectedFailure)) {
            } catch (Exception failure) {
                failures.Add(failure);
            }
            try {
                await await consumer.Task;
            } catch (Exception failure) {
                failures.Add(failure);
            }
            if (failures.Count != 0) {
                throw new AggregateException("The actual chat consumer or controlled owner failed during terminal drain.", failures);
            }
        }
    }

    private sealed class ControlledOrchestrator(SubmissionControl control, AgentChatSubmissionRetention retention,
        IAgentExecutionActivityCoordinator activity, IDatabaseProfileRuntimeAccessor profiles,
        IAgentExecutionProfileGenerationSource generation) : IAgentChatExecutionOrchestrator {
        public AgentChatOperationHandle StartSendMessage(AgentChatSendRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = profiles.ResolveCurrentProfile().Profile.Id;
            var stream = new AgentExecutionActivityStreamId(profile, WorkspaceScopeDescriptor.Organization(profile.ToString("N")),
                generation.GetGeneration(), AgentExecutionOperationId.New());
            var admitted = Assert.IsType<AgentExecutionActivityAdmitted>(activity.AdmitOperation(stream, request.AgentId,
                request.ChatSessionId, "Controlled native submission entered."));
            return control.Begin(request, retention, admitted.Operation, this);
        }
        public AgentChatOperationHandle StartSendMessage(Guid agentId, Guid? chatSessionId, string prompt,
            IReadOnlyList<string>? attachmentPaths = null, CancellationToken cancellationToken = default) =>
            StartSendMessage(new(agentId, chatSessionId, prompt) { AttachmentPaths = attachmentPaths }, cancellationToken);
        public Task<AgentChatRunResult> SendMessageAsync(AgentChatSendRequest request, CancellationToken cancellationToken = default) =>
            StartSendMessage(request, cancellationToken).Completion;
        public Task<AgentChatRunResult> SendMessageAsync(Guid agentId, Guid? chatSessionId, string prompt,
            IReadOnlyList<string>? attachmentPaths = null, CancellationToken cancellationToken = default) =>
            StartSendMessage(agentId, chatSessionId, prompt, attachmentPaths, cancellationToken).Completion;
        public AgentChatOperationHandle StartApprovalContinuation(Guid agentId, Guid chatSessionId,
            IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException("No approval is arranged.");
        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(Guid agentId, Guid chatSessionId,
            IReadOnlyList<PendingToolApprovalDecision> decisions, bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw new NotSupportedException("No approval is arranged.");
        public (AgentExecutionActivityStreamId StreamId, Task<ExecutionRunResult> Completion) StartRunRecovery(Guid agentId,
            Guid chatSessionId, Guid executionRunId, AgentExecutionActivityStreamId rejectedStreamId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException("No execution is arranged.");
    }
}
