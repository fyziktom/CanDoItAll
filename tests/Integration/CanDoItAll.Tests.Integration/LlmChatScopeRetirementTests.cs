using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.ReadModels;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.LlmChats;

[Trait("Category", "HostPlatform")]
public sealed class LlmChatScopeRetirementTests {
    [Fact]
    public async Task Admitted_catalog_read_owns_its_real_context_until_completion_after_caller_scope_retirement() {
        var barrier = new ReadBarrier();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                services.AddScoped<EfLlmChatDefinitionReadStore>();
                services.Replace(ServiceDescriptor.Scoped<ILlmChatDefinitionReadStore>(provider =>
                    new HeldReadStore(provider.GetRequiredService<EfLlmChatDefinitionReadStore>(),
                        provider.GetRequiredService<SimpleChatsDbContext>(), barrier)));
            }
        });
        var scope = application.Services.CreateAsyncScope();
        var definitions = scope.ServiceProvider.GetRequiredService<ILlmChatDefinitionApplicationService>();
        var reading = definitions.ListPageAsync(new(take: 10));
        await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            await scope.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(reading.IsCompleted);
        } finally {
            barrier.Release.TrySetResult();
        }
        var result = await reading.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(barrier.DatabaseReadCompleted);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => barrier.Context!.Set<LlmChatDefinitionRow>().AnyAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => definitions.ListPageAsync(new(take: 10)));
        await using var successor = application.Services.CreateAsyncScope();
        Assert.True((await successor.ServiceProvider.GetRequiredService<ILlmChatDefinitionApplicationService>().ListPageAsync(new(take: 10))).IsSuccess);
    }

    private sealed class ReadBarrier {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SimpleChatsDbContext? Context { get; set; }
        public bool DatabaseReadCompleted { get; set; }
    }

    private sealed class HeldReadStore(EfLlmChatDefinitionReadStore inner, SimpleChatsDbContext context, ReadBarrier barrier) : ILlmChatDefinitionReadStore {
        public Task<LlmChatDefinitionReadModel?> TryGetAsync(LlmChatDefinitionId id, CancellationToken cancellationToken = default)
            => inner.TryGetAsync(id, cancellationToken);
        public async Task<LlmChatPage<LlmChatDefinitionReadModel, LlmChatDefinitionCursor>> ListPageAsync(int take,
            LlmChatDefinitionCursor? cursor, LlmChatDefinitionStatus? status, string? searchText = null,
            IReadOnlyList<string>? tags = null, CancellationToken cancellationToken = default) {
            barrier.Context = context;
            barrier.Entered.TrySetResult();
            await barrier.Release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var result = await inner.ListPageAsync(take, cursor, status, searchText, tags, cancellationToken);
            barrier.DatabaseReadCompleted = true;
            return result;
        }
    }
}
