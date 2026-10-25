using System.Net;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Web;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Processes;

internal static class ProcessCancellationNativeHost {
    internal const string Route = "/_tests/native-cancellation";
    internal static Task<ApiTestHost> CreateAsync(ReadProbe? reads = null, bool enabled = true) => ApiTestHost.CreateAsync(true, services => {
        services.AddScoped<LocalOperatorAuthenticationStateProvider>();
        services.Replace(ServiceDescriptor.Scoped<IInteractiveAccessPrincipalProvider>(
            provider => provider.GetRequiredService<LocalOperatorAuthenticationStateProvider>()));
        if (reads is not null) {
            services.Replace(ServiceDescriptor.Scoped<IProcessCancellationProgressReader>(provider => new SelectedRead(
                new(provider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>()), reads)));
        }
    }, configureApplication: app => {
        app.MapGet(Route + "/{runId:guid}", async (Guid runId, IProcessCancellationClient client, HttpContext http) =>
            await ResultAsync(async () => {
                var view = await client.ReadAsync(new(runId), http.RequestAborted);
                return new View(view.Progress?.RunId.Value, view.Progress?.Tracking, view.CanRequest, view.RootRecoveryAvailable);
            }));
        app.MapPost(Route + "/{runId:guid}", async (Guid runId, IProcessCancellationClient client, HttpContext http) =>
            await ResultAsync(() => RequestAsync(client, runId, http.RequestAborted)));
        app.MapPost(Route + "/local/{trusted:bool}/{runId:guid}", async (bool trusted, Guid runId,
            LocalOperatorAuthenticationStateProvider interactive, IProcessCancellationClient client, HttpContext http) => {
            var address = IPAddress.Parse(trusted ? "127.0.0.1" : "192.0.2.10");
            http.Connection.RemoteIpAddress = address;
            http.Items[DevelopmentEndpointAccess.OriginalRemoteIpItemKey] = address;
            interactive.SetAuthenticationState(Task.FromResult(new AuthenticationState(http.User)));
            return await ResultAsync(() => RequestAsync(client, runId, http.RequestAborted));
        });
    }, apiConfiguration: ProcessRootCancellationAdmissionApiTests.Configuration(enabled));

    private static async Task<Receipt> RequestAsync(IProcessCancellationClient client, Guid runId, CancellationToken token)
        => await client.RequestAsync(new(runId), token) switch {
            ProcessCancellationClientResult.Root root => new(root.RunId.Value, root.Admission.Status, root.Admission.BarrierCommandId?.Value, false),
            ProcessCancellationClientResult.Child child => new(child.RunId.Value, null, null, child.Commit.Succeeded),
            _ => throw new InvalidOperationException("Unknown cancellation receipt.")
        };

    private static async Task<IResult> ResultAsync<T>(Func<Task<T>> action) {
        try {
            return Results.Ok(await action());
        } catch (ProcessCancellationAccessDeniedException) {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    internal sealed record View(Guid? RunId, ProcessCancellationTracking? Tracking, bool CanRequest, bool RootRecoveryAvailable);
    internal sealed record Receipt(Guid RunId, ProcessRootCancellationAdmissionStatus? Admission, Guid? Barrier, bool ChildStopped);
    internal sealed class ReadProbe {
        internal Func<Task>? AfterRead { get; set; }
        internal int Calls { get; private set; }
        internal async Task CompleteAsync() {
            Calls++;
            var action = AfterRead;
            AfterRead = null;
            if (action is not null) {
                await action();
            }
        }
    }

    private sealed class SelectedRead(EfProcessCancellationProgressReader reader, ReadProbe probe) : IProcessCancellationProgressReader {
        public async Task<ProcessCancellationProgress?> ReadAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            var progress = await reader.ReadAsync(runId, cancellationToken);
            await probe.CompleteAsync();
            return progress;
        }
    }
}
