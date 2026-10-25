using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class ProcessPlanningAccountingApiTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, AgentPlanningReviewChoice.Approve, true)]
    [InlineData(ReadBasisMode.StepSnapshot, AgentPlanningReviewChoice.Reject, false)]
    public async Task Closed_child_accounting_survives_HTTP_acknowledgement_loss_and_host_restart_without_an_execution_workspace(
        ReadBasisMode mode, AgentPlanningReviewChoice choice, bool loseAcknowledgement) {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, environment, profile);
        await fixture.CloseWithoutAccountingAsync(choice);
        var before = JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id));
        var route = Route(fixture);
        var fault = new CompletionFault();
        string retained;
        Guid action;
        await using (var host = await HostAsync(environment, profile, fixture.Clock, route, loseAcknowledgement ? fault : null)) {
            ProcessPlanningReviewHttpAccessTests.UseToken(host);
            using var read = await host.Client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var view = JsonSerializer.Deserialize<ProcessPlanningReviewApiView>(await read.Content.ReadAsStringAsync(), Options(host))!;
            Assert.Equal(ProcessPlanningReviewState.RecoveryRequired, view.State);
            Assert.Null(view.Process);
            Assert.Empty(view.AvailableChoices);
            action = Guid.NewGuid();
            var request = new ProcessPlanningAccountingApiSubmission(action, view.Expected,
                Assert.IsType<ProcessPlanningReviewApiExpectation>(view.Accounting));
            retained = JsonSerializer.Serialize(request, Options(host));

            using var applied = await host.Client.PostAsync(route + "/accounting", Content(retained));

            Assert.Equal(loseAcknowledgement ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK, applied.StatusCode);
            Assert.Equal("no-store", applied.Headers.CacheControl?.ToString());
            if (!loseAcknowledgement) {
                var result = JsonSerializer.Deserialize<ProcessPlanningAccountingApiResult>(await applied.Content.ReadAsStringAsync(), Options(host))!;
                Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, result.Completion);
                Assert.Equal(action, result.ActionId);
                Assert.True(result.RequiresRuntimeReload);
            }
        }
        await using (var restarted = await HostAsync(environment, profile, fixture.Clock, route)) {
            ProcessPlanningReviewHttpAccessTests.UseToken(restarted);
            using var recovered = await restarted.Client.PostAsync(route + "/accounting", Content(retained));
            Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
            var text = await recovered.Content.ReadAsStringAsync();
            Assert.DoesNotContain("\"toolAdmission\"", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"metadataJson\"", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"currentState\"", text, StringComparison.OrdinalIgnoreCase);
            var result = JsonSerializer.Deserialize<ProcessPlanningAccountingApiResult>(text, Options(restarted))!;
            Assert.Equal(action, result.ActionId);
            Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, result.Completion);
            Assert.True(result.RequiresRuntimeReload);
            Assert.Empty(result.RefusalCodes);
            using var read = await restarted.Client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var view = JsonSerializer.Deserialize<ProcessPlanningReviewApiView>(await read.Content.ReadAsStringAsync(), Options(restarted))!;
            Assert.Equal(ProcessPlanningReviewState.Settled, view.State);
            Assert.Null(view.Accounting);
            Assert.Empty(view.AvailableChoices);
        }
        Assert.Equal(loseAcknowledgement ? 1 : 0, fault.Commits);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id)));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(choice == AgentPlanningReviewChoice.Approve ? 1 : 0, choice == AgentPlanningReviewChoice.Approve ? 1 : 0);
    }

    [Fact]
    public async Task Accounting_route_refuses_pending_children_missing_grants_and_invalid_transport_without_mutation() {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(environment: environment, profile: profile);
        var route = Route(fixture);
        await using var host = await HostAsync(environment, profile, fixture.Clock, route);
        ProcessPlanningReviewHttpAccessTests.UseToken(host);
        var before = JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id));
        var request = new ProcessPlanningAccountingApiSubmission(Guid.NewGuid(), AgentPlanningDecisionExpectation.From(fixture.Waiting),
            ProcessPlanningReviewApiExpectation.From(fixture.Request));
        var body = JsonSerializer.Serialize(request, Options(host));
        await RefusedAsync(Content(body), HttpStatusCode.Conflict);
        await RefusedAsync(new StringContent(body, Encoding.UTF8, "text/plain"), HttpStatusCode.UnsupportedMediaType);
        await RefusedAsync(Content(new string(' ', ProcessPlanningReviewRequestReader.MaximumUtf8Bytes + 1)), HttpStatusCode.RequestEntityTooLarge);
        var unknown = JsonNode.Parse(body)!;
        unknown["choice"] = "Approve";
        await RefusedAsync(Content(unknown.ToJsonString()), HttpStatusCode.BadRequest);
        var changed = JsonNode.Parse(body)!;
        changed["process"]!["runId"] = Guid.NewGuid();
        await RefusedAsync(Content(changed.ToJsonString()), HttpStatusCode.BadRequest);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.Api]);
        await RefusedAsync(Content(body), HttpStatusCode.Forbidden);
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id)));
        Assert.Single(fixture.Entry.Wire.Requests);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);

        async Task RefusedAsync(HttpContent content, HttpStatusCode expected) {
            using (content) {
                using var response = await host.Client.PostAsync(route + "/accounting", content);
                Assert.Equal(expected, response.StatusCode);
            }
        }
    }

    private static Task<ApiTestHost> HostAsync(CanDoItAllTestEnvironment environment, TestDatabaseProfile profile, TimeProvider clock,
        string route, CompletionFault? fault = null)
        => ApiTestHost.CreateAsync(true, services => {
            services.AddSingleton(clock);
            var factory = services.Last(item => item.ServiceType == typeof(ICanDoItAllAgentWorkspaceFactory));
            Assert.NotNull(factory.ImplementationType);
            services.AddScoped<ICanDoItAllAgentWorkspaceFactory>(provider => {
                var request = provider.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request;
                Assert.False(request is not null && (request.Path == route || request.Path == route + "/accounting"),
                    "Reading or reconciling completed Process work must not construct the execution workspace.");
                return (ICanDoItAllAgentWorkspaceFactory)ActivatorUtilities.CreateInstance(provider, factory.ImplementationType);
            });
            if (fault is not null) {
                services.AddScoped(provider => new ProcessDecisionReviewCoordinator(
                    provider.GetRequiredService<IProcessAcceptedExecutionInputReader>(), provider.GetRequiredService<IProcessDecisionResultQuery>(),
                    provider.GetServices<IProcessDecisionChildOutcomeReader>(), provider.GetRequiredService<IProcessIdempotencyStore>(),
                    new LostAcknowledgement(provider.GetRequiredService<IProcessRuntimeUnitOfWork>(), fault),
                    NullLogger<ProcessDecisionReviewCoordinator>.Instance));
            }
        }, sharedTestEnvironment: environment, sharedActiveProfile: profile);

    private static JsonSerializerOptions Options(ApiTestHost host)
        => host.App.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

    private static string Route(ProjectTaskPlanReviewFixture fixture)
        => $"/api/processes/runs/{fixture.State.RunId.Value:D}/steps/{fixture.Request.Child.StepInstanceId.Value:D}/planning/{fixture.Waiting.Id:D}/review";

    private static StringContent Content(string body) => new(body, Encoding.UTF8, "application/json");

    private sealed class CompletionFault {
        internal int Commits;
    }

    private sealed class LostAcknowledgement(IProcessRuntimeUnitOfWork inner, CompletionFault fault) : IProcessRuntimeUnitOfWork {
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken) {
            var result = await inner.CommitAsync(request, cancellationToken);
            Assert.True(result.CommittedNow);
            Assert.Equal(1, Interlocked.Increment(ref fault.Commits));
            throw new IOException("The original Process accounting transaction committed without an acknowledged response.");
        }
    }
}
