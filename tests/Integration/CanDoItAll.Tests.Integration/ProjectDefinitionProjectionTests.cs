using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectDefinitionProjectionTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_after_search_flush_rolls_back_definition_children_and_projection(bool creating) {
        var probe = new SearchFlushProbe();
        await using var application = await CreateApplicationAsync(probe);
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var id = Guid.NewGuid();
        if (!creating) {
            Assert.True((await service.CreateAsync(id, Draft())).IsSuccess);
        }
        var original = (await service.GetDefinitionAsync(id)).Value;
        var originalProjection = await ReadProjectionAsync(application.Services, id);
        var draft = creating ? Draft() : await service.GetAsync(id);
        draft.Name = "Uncommitted proposed name";
        draft.Phases.Clear();
        draft.Options.Clear();
        probe.Arm(throwAfterFlush: true);

        await Assert.ThrowsAsync<ProjectionFlushFailure>(() => creating
            ? service.CreateAsync(id, draft)
            : service.SaveAsync(draft));

        Assert.True(probe.SawEnlistedTransaction);
        var current = await service.GetDefinitionAsync(id);
        if (creating) {
            Assert.Contains(current.Errors, error => error.Code == ProjectErrorCodes.NotFound);
        } else {
            Assert.Equal(original!.Expectation, current.Value!.Expectation);
            Assert.Equal(original.Scalars, current.Value.Scalars);
            Assert.Equal(original.Phases, current.Value.Phases);
            Assert.Equal(original.Options, current.Value.Options);
        }
        Assert.Equal(originalProjection, await ReadProjectionAsync(application.Services, id));
    }

    [Fact]
    public async Task Search_and_definition_remain_invisible_until_the_same_owner_transaction_commits() {
        var probe = new SearchFlushProbe();
        await using var application = await CreateApplicationAsync(probe);
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var created = await service.SaveAsync(Draft());
        Assert.True(created.IsSuccess);
        var id = created.Value;
        var original = (await service.GetDefinitionAsync(id)).Value!;
        var originalProjection = await ReadProjectionAsync(application.Services, id);
        var draft = await service.GetAsync(id);
        draft.Name = "Committed with search";
        draft.Phases[0].Name = "Accepted phase";
        draft.Options[0].OptionName = "Accepted option";
        probe.Arm(throwAfterFlush: false);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var saving = service.SaveEditorAsync(draft, deadline.Token);
        try {
            await probe.Flushed.Task.WaitAsync(deadline.Token);
            Assert.True(probe.SawEnlistedTransaction);
            var during = (await service.GetDefinitionAsync(id, deadline.Token)).Value!;
            Assert.Equal(original.Expectation, during.Expectation);
            Assert.Equal(original.Scalars, during.Scalars);
            Assert.Equal(originalProjection, await ReadProjectionAsync(application.Services, id));
        } finally {
            probe.Release.TrySetResult();
            await saving;
        }

        var accepted = (await saving).Value!;
        var current = (await service.GetDefinitionAsync(id)).Value!;
        var projection = await ReadProjectionAsync(application.Services, id);
        Assert.Equal(accepted.Definition, current.Expectation);
        Assert.Equal(accepted.Name, projection!.Title);
        Assert.Contains("Accepted option", projection.Body, StringComparison.Ordinal);
        Assert.Equal("Accepted phase", Assert.Single(current.Phases).Name);
    }

    private static Task<TestApplication> CreateApplicationAsync(SearchFlushProbe probe)
        => TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.ConfigureDbContext<SearchDbContext>(
                options => options.AddInterceptors(probe), ServiceLifetime.Singleton)
        });

    private static ProjectEditorModel Draft() => new() {
        Name = "Original project",
        Objective = "Original objective",
        Phases = [new() { Name = "Original phase", Goal = "Original goal" }],
        Options = [new() { Category = ProjectOptionCategory.Language, OptionName = "Original option" }]
    };

    private static async Task<ProjectionState?> ReadProjectionAsync(IServiceProvider services, Guid id) {
        await using var context = await services.GetRequiredService<IDbContextFactory<SearchDbContext>>().CreateDbContextAsync();
        var key = id.ToString();
        return await context.Set<SearchDocument>().AsNoTracking()
            .Where(document => document.SourceType == SearchDocument.ProjectSourceType && document.SourceKey == key)
            .Select(document => new ProjectionState(document.Title, document.Body, document.UpdatedAtUtc))
            .SingleOrDefaultAsync();
    }

    private sealed record ProjectionState(string Title, string Body, DateTimeOffset UpdatedAtUtc);

    private sealed class ProjectionFlushFailure : Exception;

    private sealed class SearchFlushProbe : SaveChangesInterceptor {
        private int armed;
        private bool throwAfterFlush;
        public bool SawEnlistedTransaction { get; private set; }
        public TaskCompletionSource Flushed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm(bool throwAfterFlush) {
            this.throwAfterFlush = throwAfterFlush;
            Interlocked.Exchange(ref armed, 1);
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Interlocked.CompareExchange(ref armed, 0, 1) != 1) {
                return result;
            }
            SawEnlistedTransaction = eventData.Context?.Database.CurrentTransaction is not null;
            Flushed.TrySetResult();
            if (throwAfterFlush) {
                throw new ProjectionFlushFailure();
            }
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }
}
