using Bunit;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Processes;

public sealed partial class ProcessWorkspaceShellTests {
    [Fact]
    public async Task Launch_missing_browser_preparation_remains_explicit_without_creating_a_replacement() {
        using var context = CreateLaunchContext();
        var intent = Guid.NewGuid();
        var authority = new LaunchOperatorSource(ProcessPreparedLaunchFixture.Local(Guid.NewGuid()));
        var store = new RetainedLaunchStore(null);
        context.Services.AddSingleton<IProcessLaunchOperatorAuthoritySource>(authority);
        context.Services.AddSingleton<IProcessPreparedLaunchStore>(store);
        context.Services.AddSingleton(ObservationOnlyLaunchService(store, new LaunchStateStore(null)));
        context.JSInterop.Setup<string?>("sessionStorage.getItem", _ => true).SetResult(intent.ToString("D"));
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-shell']");
        await InvokeLaunchAsync(cut);
        cut.WaitForAssertion(() => Assert.Contains(intent.ToString("D"), cut.Markup, StringComparison.Ordinal));
        Assert.NotNull(cut.Find("[data-testid='processes-launch-new-intent']"));
        Assert.Equal(0, store.ContinuationCalls);
        Assert.Single(authority.ProjectCaptures);
        Assert.Null(authority.ProjectCaptures[0]);
        Assert.DoesNotContain(context.JSInterop.Invocations, invocation => invocation.Identifier == "sessionStorage.setItem");
    }

    [Fact]
    public async Task Launch_restores_the_accepted_browser_intent_without_recapturing_a_new_project_lifetime() {
        using var context = CreateLaunchContext();
        var profile = Guid.NewGuid();
        var project = Guid.NewGuid();
        var originalAuthority = ProcessPreparedLaunchFixture.Local(profile, new(profile, project, Guid.NewGuid()));
        var preparation = ProcessPreparedLaunchFixture.Create(originalAuthority, new(Guid.NewGuid()));
        var request = preparation.Request with { DefinitionKey = "blazor-app-delivery", Execute = true };
        preparation = preparation with { Request = request, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(request) };
        var retained = new ProcessPreparedLaunchSnapshot(preparation, "retained-component-evidence", 9,
            ProcessLaunchContinuationState.Started, ProcessProjectAdmissionFixture.Now, true,
            ProcessLaunchLinkDeliveryState.NotRequested, null, null);
        var store = new RetainedLaunchStore(retained);
        var authorities = new LaunchOperatorSource(originalAuthority with { ProjectAdmission = null });
        context.Services.AddSingleton<IProcessLaunchOperatorAuthoritySource>(authorities);
        context.Services.AddSingleton<IProcessPreparedLaunchStore>(store);
        context.Services.AddSingleton(ObservationOnlyLaunchService(store, new LaunchStateStore(preparation.InitialCommit.Mutation.State)));
        context.JSInterop.Setup<string?>("sessionStorage.getItem", _ => true).SetResult(preparation.CallerIntentId!.Value.Value.ToString("D"));
        var cut = context.Render<ProcessWorkspaceShell>(parameters => parameters.Add(component => component.ProjectId, project));
        cut.WaitForElement("[data-testid='processes-shell']");
        await InvokeLaunchAsync(cut);
        Assert.Contains(preparation.InitialCommit.Mutation.State.RunId.Value.ToString("D"),
            context.Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.Single(authorities.ProjectCaptures);
        Assert.Null(authorities.ProjectCaptures[0]);
        Assert.Equal(1, store.ContinuationCalls);
        Assert.Contains(context.JSInterop.Invocations, invocation => invocation.Identifier == "sessionStorage.setItem");
        Assert.DoesNotContain(context.JSInterop.Invocations, invocation => invocation.Identifier == "sessionStorage.removeItem");
    }

    private static BunitContext CreateLaunchContext() {
        var context = CreateContext(out var client);
        client.ShellResultTransform = (_, projection) => projection with {
            Authorization = projection.Authorization with { CanLaunchRuns = true },
            Commands = projection.Commands.Select(command => command.Kind == ProcessWorkspaceCommandKind.LaunchRun
                ? command with { IsEnabled = true, DisabledReason = null } : command).ToArray()
        };
        return context;
    }

    private static Task InvokeLaunchAsync(IRenderedComponent<ProcessWorkspaceShell> cut)
        => cut.Find("[data-testid='processes-command-launchrun']").ClickAsync(new MouseEventArgs());

    private static ProcessLaunchApplicationService ObservationOnlyLaunchService(IProcessPreparedLaunchStore store, IProcessRuntimeStateStore states)
        => new(null!, new FixedProcessProjectionClock(Now), null!, null!, null!, null!, states, null!, null!, null!, null!, null!, null!, null!, store);

    private sealed class LaunchOperatorSource(ProcessLaunchAuthority authority) : IProcessLaunchOperatorAuthoritySource {
        public List<Guid?> ProjectCaptures { get; } = [];
        public Task<ProcessLaunchAuthority> CaptureLocalAsync(Guid? projectId, ProcessLaunchOperatorSurface surface,
            CancellationToken cancellationToken = default) {
            ProjectCaptures.Add(projectId);
            if (projectId is not null) {
                throw new InvalidOperationException("The restored launch must use its original project lifetime.");
            }
            return Task.FromResult(authority);
        }
        public Task<ProcessLaunchAuthority> CaptureAuthenticatedAsync(Guid? projectId, string subjectId, DateTimeOffset expiresAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class LaunchStateStore(ProcessRuntimeStateSnapshot? state) : IProcessRuntimeStateStore {
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(ProcessRunId runId, CancellationToken cancellationToken = default)
            => Task.FromResult(state?.RunId == runId ? state : null);
    }

    private sealed class RetainedLaunchStore(ProcessPreparedLaunchSnapshot? saved) : IProcessPreparedLaunchStore {
        public int ContinuationCalls { get; private set; }
        public Task<ProcessPreparedLaunchSnapshot?> FindByIntentAsync(ProcessLaunchIntentId intentId, CancellationToken cancellationToken = default)
            => Task.FromResult(saved?.Preparation.CallerIntentId == intentId ? saved : null);
        public Task<ProcessPreparedLaunchSnapshot?> GetAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default)
            => Task.FromResult(saved?.Preparation.AdmissionId == admissionId ? saved : null);
        public Task<ProcessLaunchContinuationClaim?> ClaimContinuationAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default) {
            Assert.Equal(saved!.Preparation.AdmissionId, admissionId);
            ContinuationCalls++;
            return Task.FromResult<ProcessLaunchContinuationClaim?>(null);
        }
        public Task<ProcessPreparedLaunchSnapshot?> FindByRunAsync(ProcessRunId runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProcessPreparedLaunchSnapshot> PrepareAsync(ProcessPreparedLaunch preparation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RenewContinuationAsync(ProcessLaunchContinuationClaim claim, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CompleteContinuationAsync(ProcessLaunchContinuationClaim claim, ProcessLaunchContinuationState state, string? publicFailure,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessLaunchAdmissionId>> ListPendingContinuationsAsync(int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
