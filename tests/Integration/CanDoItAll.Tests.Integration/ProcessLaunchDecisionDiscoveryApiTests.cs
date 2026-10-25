using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Api;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessLaunchDecisionDiscoveryApiTests {
    private const string Route = "/api/processes/launch/decision-contracts";

    [Fact]
    public async Task Catalog_exposes_only_registered_planning_choices_without_requiring_workspace_configuration() {
        await using var host = await ApiTestHost.CreateAsync(false);
        var contracts = await host.Client.GetFromJsonAsync<ProcessLaunchDecisionDescriptor[]>(Route);
        var descriptor = Assert.Single(contracts!);
        Assert.Equal(ProjectTaskPlanProcessContract.Execution, descriptor.Execution);
        Assert.Equal("Reviewed Project task planning", descriptor.DisplayName);
        Assert.Equal(new[] {
            (ReadBasisMode.RunSnapshot, OnBasisChange.PauseForReview),
            (ReadBasisMode.StepSnapshot, OnBasisChange.PauseForReview),
            (ReadBasisMode.RunSnapshot, OnBasisChange.Fail),
            (ReadBasisMode.StepSnapshot, OnBasisChange.Fail)
        }, descriptor.SupportedPolicies.Select(item => (item.ReadBasisMode, item.OnBasisChange)));
        Assert.All(descriptor.SupportedPolicies, policy => {
            Assert.Equal(PublicationFreshness.RequireCurrent, policy.PublicationFreshness);
            Assert.Equal(new(0, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)), policy.Budget);
        });
    }

    [Fact]
    public async Task Discovery_requires_process_read_permission_but_does_not_grant_execution() {
        await using var host = await ApiTestHost.CreateAsync(true);
        using var unauthenticated = await host.Client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProjects]);
        using var insufficient = await host.Client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Forbidden, insufficient.StatusCode);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.Api]);
        using var legacy = await host.Client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses]);
        using var discovery = await host.Client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        using var launch = await host.Client.PostAsJsonAsync("/api/processes/launch/check", new { });
        Assert.Equal(HttpStatusCode.Forbidden, launch.StatusCode);
    }

    [Fact]
    public async Task OpenApi_exposes_exact_policy_shapes_and_the_route_contract_lists_discovery() {
        await using var host = await ApiTestHost.CreateAsync(false);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var operation = root.GetProperty("paths").GetProperty(Route).GetProperty("get");
        Assert.Equal("ListProcessDecisionContracts", operation.GetProperty("operationId").GetString());
        var array = operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        Assert.Equal("array", array.GetProperty("type").GetString());
        var descriptor = Resolve(array.GetProperty("items"));
        Assert.Equal(new[] { "description", "displayName", "execution", "supportedPolicies" },
            descriptor.GetProperty("properties").EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal));
        var execution = Resolve(descriptor.GetProperty("properties").GetProperty("execution"));
        Assert.Equal(new[] { "contractId", "executor", "version" },
            execution.GetProperty("properties").EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal));
        var policies = Resolve(descriptor.GetProperty("properties").GetProperty("supportedPolicies"));
        var policy = Resolve(policies.GetProperty("items"));
        Assert.Equal(7, policy.GetProperty("required").GetArrayLength());
        Assert.True(policy.GetProperty("properties").TryGetProperty("maximumWorkDuration", out _));
        Assert.True(policy.GetProperty("properties").TryGetProperty("maximumBasisAge", out _));
        using var contract = JsonDocument.Parse(await host.Client.GetStringAsync("/api/processes/contract"));
        Assert.Contains(contract.RootElement.GetProperty("endpoints").EnumerateArray(), item => item.GetString() == $"GET {Route}");

        JsonElement Resolve(JsonElement schema) {
            while (schema.TryGetProperty("$ref", out var reference)) {
                schema = root;
                foreach (var segment in reference.GetString()![2..].Split('/')) {
                    schema = schema.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
                }
            }
            return schema;
        }
    }
}
