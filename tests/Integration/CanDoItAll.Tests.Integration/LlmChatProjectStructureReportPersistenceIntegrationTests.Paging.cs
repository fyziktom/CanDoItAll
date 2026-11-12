using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.ReadModels;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed partial class LlmChatProjectStructureReportPersistenceIntegrationTests {
    [Fact]
    public async Task PostgreSql_pages_canonical_operations_without_reinterpreting_an_accepted_as_of_window() {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("insights-chat-paging");
        var project = Guid.NewGuid();
        var conversation = Guid.NewGuid();
        var operations = Enumerable.Range(1, 25).Select(index => CreateOperation(conversation, project,
            LlmChatOperationStatus.Succeeded, UtcMidnight.AddMinutes(-index - 2), UtcMidnight.AddMinutes(-index))).ToArray();
        await using (var context = database.CreateSimpleChatsDbContext()) {
            SeedConversationRoot(context, Guid.NewGuid(), conversation);
            context.Set<LlmChatOperationRow>().AddRange(operations);
            context.Set<LlmChatInvocationRecordRow>().AddRange(operations.Select(operation =>
                CreateInvocation(operation, 1, LlmChatInvocationPricingEvidenceStatus.ProviderReported, providerCostUsd: 0.125m)));
            await context.SaveChangesAsync();
        }
        var store = new EfLlmChatProjectStructureReportStore(new TestDbContextFactory(database));
        LlmChatProjectStructureReportQuery Query(int page) => new([project], UtcMidnight.AddHours(-1), UtcMidnight,
            UtcMidnight.AddHours(-1), [LlmChatOperationStatus.Succeeded], pageIndex: page, pageSize: 20);
        var first = await store.QueryProjectStructureReportAsync(Query(0));
        Assert.Equal(25, first.TotalCount);
        Assert.Equal(3.125m, first.KnownCostUsd);
        Assert.Equal(20, first.Runs.Count);
        var late = CreateOperation(conversation, project, LlmChatOperationStatus.Succeeded, UtcMidnight.AddMinutes(1), UtcMidnight.AddMinutes(3));
        await using (var context = database.CreateSimpleChatsDbContext()) {
            context.Set<LlmChatOperationRow>().Add(late);
            context.Set<LlmChatInvocationRecordRow>().Add(CreateInvocation(late, 1, LlmChatInvocationPricingEvidenceStatus.ProviderReported, providerCostUsd: 99m));
            await context.SaveChangesAsync();
        }
        var second = await store.QueryProjectStructureReportAsync(Query(1));
        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.Equal(first.KnownCostUsd, second.KnownCostUsd);
        Assert.Equal(5, second.Runs.Count);
        Assert.Equal(operations.Select(operation => operation.Id).Order(), first.Runs.Concat(second.Runs).Select(run => run.OperationId.Value).Order());
        var backward = await store.QueryProjectStructureReportAsync(Query(0));
        Assert.Equal(first.Runs.Select(run => run.OperationId), backward.Runs.Select(run => run.OperationId));
        var current = await store.QueryProjectStructureReportAsync(new([project], UtcMidnight.AddHours(-1), UtcMidnight.AddMinutes(4),
            UtcMidnight.AddHours(-1), [LlmChatOperationStatus.Succeeded]));
        Assert.Equal(26, current.TotalCount);
        Assert.Equal(102.125m, current.KnownCostUsd);
    }
}
