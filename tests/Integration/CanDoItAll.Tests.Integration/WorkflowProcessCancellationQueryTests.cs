using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.AgentFramework.WorkflowProcessCancellationFixture;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowProcessCancellationQueryTests(ITestOutputHelper output) {
    [Fact]
    public async Task Deep_keyset_and_pending_request_selections_use_bounded_indexes_with_unrelated_history() {
        await using var app = await TestApplication.CreateAsync();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        WorkflowProcessRunId process = new(Guid.NewGuid());
        var selected = await RunAsync(services, process);
        await RequestAsync(services, selected);
        var unrelated = await RunAsync(services, new(Guid.NewGuid()));
        var unrelatedRequest = await RequestAsync(services, unrelated);
        await using var database = Context(services);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AgentFramework_WorkflowRuns"
            SELECT (jsonb_populate_record(NULL::"AgentFramework_WorkflowRuns", to_jsonb(seed) ||
                jsonb_build_object('RunId', md5('workflow-recovery-' || ordinal)::uuid))).*
            FROM "AgentFramework_WorkflowRuns" seed CROSS JOIN generate_series(1, 10000) ordinal WHERE seed."RunId" = {unrelated.RunId.Value};
            INSERT INTO "AgentFramework_WorkflowExternalRequests"
            SELECT (jsonb_populate_record(NULL::"AgentFramework_WorkflowExternalRequests", to_jsonb(seed) ||
                jsonb_build_object('Id', md5('request-recovery-' || ordinal)::uuid))).*
            FROM "AgentFramework_WorkflowExternalRequests" seed CROSS JOIN generate_series(1, 10000) ordinal WHERE seed."Id" = {unrelatedRequest.Id.Value}
            """);
        for (var index = 1; index <= 32; index++) {
            await RunAsync(services, process, new(new Guid($"00000000-0000-0000-0000-{index:D12}")));
        }
        await database.Database.ExecuteSqlRawAsync("ANALYZE \"AgentFramework_WorkflowRuns\"; ANALYZE \"AgentFramework_WorkflowExternalRequests\"");
        var after = new WorkflowRunId(new Guid("00000000-0000-0000-0000-000000000030"));
        var next = new WorkflowRunId(new Guid("00000000-0000-0000-0000-000000000031"));
        Assert.Equal(next, await Owner(services).FindNextAsync(process, after, default));
        var runPlan = (await database.Database.SqlQuery<string>($"""
            EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) SELECT "RunId" FROM "AgentFramework_WorkflowRuns"
            WHERE "OriginProcessRunId" = {process.Value} AND "RunId" > {after.Value} ORDER BY "RunId" LIMIT 1
            """).ToArrayAsync()).Single();
        var requestPlan = (await database.Database.SqlQuery<string>($"""
            EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) SELECT "Id" FROM "AgentFramework_WorkflowExternalRequests"
            WHERE "RunId" = {selected.RunId.Value} AND "RespondedAtUtc" IS NULL ORDER BY "Id" LIMIT 2
            """).ToArrayAsync()).Single();
        RequireIndex(runPlan, "IX_WorkflowRuns_ProcessRecovery");
        RequireIndex(requestPlan, "IX_WorkflowRequests_ProcessRecovery");
        Assert.Equal(10002, await database.Set<WorkflowExternalRequestRecordEntity>().CountAsync());
    }

    private void RequireIndex(string json, string index) {
        using var document = JsonDocument.Parse(json);
        var plan = document.RootElement[0].GetProperty("Plan");
        Assert.Equal(1d, plan.GetProperty("Actual Rows").GetDouble());
        Assert.Contains(index, json, StringComparison.Ordinal);
        Assert.DoesNotContain("Seq Scan", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Sort Key", json, StringComparison.Ordinal);
        output.WriteLine(json);
    }
}
