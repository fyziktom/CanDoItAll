using System.Data.Common;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Components;
using CanDoItAll.Prompts.UI.Editor;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.AgentFramework;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.Prompts;

// The Prompt Gallery detail loader reads the artifact header and its collections with separate statements and no
// snapshot transaction. Under PostgreSQL's default Read Committed isolation another editor can commit between those
// statements, so a read-back can carry this editor's own header token together with the other editor's collections.
// The editor session must not let such a read-back authorize a later overwrite of the other editor's collection edit.
public sealed class PromptGalleryMixedReadBackPersistenceIntegrationTests
{
    private const string SupportedModelsTable = "Prompts_PromptSupportedProviderModels";
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private static readonly PromptGalleryEditorSubmission DraftA = new(
        "Shared prompt",
        "Summary",
        PromptGalleryItemKind.FullPrompt,
        "design",
        "Content written by editor A",
        ["architecture", "review"],
        [new PromptProviderModel("OpenAI", "gpt-5.4-mini", IsPreferred: true)],
        [PromptGalleryConsumer.Chat],
        new PromptModelRecommendations(0.2, 800, 0.9));

    private static readonly PromptProviderModel[] ModelsA =
    [
        new("OpenAI", "gpt-5.4-mini", IsPreferred: true),
        new("OpenAI", "gpt-5.4", IsPreferred: false)
    ];

    private static readonly PromptProviderModel[] ModelsB =
    [
        new("Anthropic", "claude-sonnet-5", IsPreferred: true)
    ];

    [Fact]
    public async Task PostgreSql_MixedReadBackAfterOwnReceipt_DoesNotAuthorizeOverwritingAnotherEditorsModelSet()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var database = PostgresTestDatabaseLease.Create("promptgallerymixedread");
        var canonical = new WorkflowUsagePostgresDbContextFactory(database.CreateAppDbContextOptions());
        await using (var dbContext = canonical.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        var barrier = new SupportedModelReadBarrier();
        var serviceA = CreateGallery(database.ConnectionString, barrier);
        var serviceB = CreateGallery(database.ConnectionString, interceptor: null);
        var seeded = await serviceB.SaveDraftAsync(ToDraft(DraftA, id: null, expected: null));
        Assert.True(seeded.IsSuccess, string.Join(" ", seeded.Errors.Select(error => error.Message)));
        var itemId = seeded.Value.PromptArtifactId;

        PromptDraftSaveReceipt? receiptA = null;
        var armed = new ArmingGallery(serviceA, receipt =>
        {
            receiptA = receipt;
            // The read-back that follows this receipt stops right before its supported-model statement.
            barrier.Arm();
        });
        var notices = new List<PromptGalleryNotice>();
        await using var editorA = new PromptGalleryEditorSession(armed)
        {
            NoticeRaised = notice => { notices.Add(notice); return Task.CompletedTask; }
        };
        await editorA.SetTargetAsync(itemId);
        Assert.Equal(PromptGalleryEditorPhase.Ready, editorA.Presentation.Phase);
        var sourceBefore = editorA.Presentation.Source;

        // Editor A saves model set MA. Its read-back reads the header at A's token, then waits at the barrier.
        var saveA = editorA.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(
            editorA.Presentation.Generation,
            DraftA with { SupportedModels = ModelsA }));
        await barrier.Reached.WaitAsync(Bound);
        Assert.NotNull(receiptA);

        // Editor B commits model set MB against A's revision through a separate context while A is suspended.
        var foreign = await serviceB.SaveDraftAsync(ToDraft(DraftA with { SupportedModels = ModelsB }, itemId, receiptA!.Value.UpdatedAtUtc));
        Assert.True(foreign.IsSuccess, string.Join(" ", foreign.Errors.Select(error => error.Message)));
        Assert.NotEqual(receiptA.Value.UpdatedAtUtc, foreign.Value.UpdatedAtUtc);

        barrier.Release();
        await saveA.WaitAsync(Bound);

        // The mixed read-back (A's header token, B's models) neither replaces the accepted baseline nor stays silent.
        Assert.Equal(receiptA.Value.UpdatedAtUtc, editorA.AcceptedToken);
        Assert.True(editorA.Presentation.HasExternalChange);
        Assert.Same(sourceBefore, editorA.Presentation.Source);
        Assert.Single(notices, notice => notice.Summary == "Prompt draft saved");

        // A's own archive advances the token; the consistent read of MB must not be adopted as A's baseline.
        await editorA.ApplyAsync(new PromptGalleryEditorIntent.ToggleArchive(editorA.Presentation.Generation));
        Assert.True(editorA.Presentation.IsArchived);
        Assert.Equal(receiptA.Value.UpdatedAtUtc, editorA.AcceptedToken);
        Assert.True(editorA.Presentation.HasExternalChange);

        // A's stale save of MA is rejected by the owner, so B's model set survives.
        await editorA.ApplyAsync(new PromptGalleryEditorIntent.SaveDraft(
            editorA.Presentation.Generation,
            DraftA with { SupportedModels = ModelsA }));
        var rejected = Assert.Single(notices, notice => notice.Summary == "Prompt draft was not saved");
        Assert.Contains("Reload it before saving", rejected.Detail, StringComparison.Ordinal);

        var persisted = await serviceB.GetItemAsync(itemId);
        Assert.True(persisted.IsSuccess);
        Assert.Equal(
            [("Anthropic", "claude-sonnet-5", true)],
            persisted.Value!.SupportedModels.Select(model => (model.Provider, model.Model, model.IsPreferred)));
        Assert.True(persisted.Value.IsArchived);
        Assert.NotEqual(receiptA.Value.UpdatedAtUtc, persisted.Value.UpdatedAtUtc);
        Assert.Equal(receiptA.Value.UpdatedAtUtc, editorA.AcceptedToken);
        Assert.Same(sourceBefore, editorA.Presentation.Source);
        Assert.True(editorA.Presentation.HasExternalChange);

        // The explicit reload accepts B's revision and the next save continues from it.
        await editorA.ApplyAsync(new PromptGalleryEditorIntent.Retry(editorA.Presentation.Generation));
        Assert.False(editorA.Presentation.HasExternalChange);
        Assert.Equal(persisted.Value.UpdatedAtUtc, editorA.AcceptedToken);
    }

    private static PromptsService CreateGallery(string connectionString, IInterceptor? interceptor)
    {
        var options = new DbContextOptionsBuilder<PromptsDbContext>().UseNpgsql(connectionString);
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        var factory = new PromptsPersistenceTestFactory(options.Options);
        return new PromptsService(
            factory,
            new SystemClock(),
            new NullActivityStream(),
            new EfPromptGallerySearchDriver(factory),
            new PromptGalleryProjectionCoordinator(factory, new DisabledPromptGalleryProjectionDriver()),
            new PromptGalleryCompatibilityEvaluator(),
            NullLogger<PromptsService>.Instance);
    }

    private static PromptGalleryDraft ToDraft(PromptGalleryEditorSubmission submission, Guid? id, DateTimeOffset? expected)
        => new(
            id,
            ProjectId: null,
            CollectionId: null,
            submission.Title,
            submission.Summary,
            submission.Kind,
            submission.Phase,
            submission.Content,
            submission.Tags,
            submission.SupportedModels,
            submission.SupportedConsumers,
            submission.Recommendations,
            ExpectedUpdatedAtUtc: expected);

    // Holds the first supported-model statement issued after arming until the test releases it; bounded so a test
    // that never releases fails instead of hanging.
    private sealed class SupportedModelReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile bool armed;

        public Task Reached => reached.Task;

        public void Arm() => armed = true;

        public void Release() => release.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (armed && !reached.Task.IsCompleted && command.CommandText.Contains(SupportedModelsTable, StringComparison.Ordinal))
            {
                reached.TrySetResult();
                await release.Task.WaitAsync(Bound, cancellationToken);
            }

            return result;
        }
    }

    // Forwards every call to the real service and reports each successful draft receipt before the read-back.
    private sealed class ArmingGallery(IPromptGalleryService inner, Action<PromptDraftSaveReceipt> onReceipt) : IPromptGalleryService
    {
        private bool reported;

        public async Task<Result<PromptDraftSaveReceipt>> SaveDraftAsync(PromptGalleryDraft draft, CancellationToken cancellationToken = default)
        {
            var result = await inner.SaveDraftAsync(draft, cancellationToken);
            if (result.IsSuccess && !reported)
            {
                reported = true;
                onReceipt(result.Value);
            }

            return result;
        }

        public Task<Result<PromptGalleryItemDetails>> GetItemAsync(Guid promptArtifactId, CancellationToken cancellationToken = default)
            => inner.GetItemAsync(promptArtifactId, cancellationToken);

        public Task<PromptGalleryPage<PromptGallerySearchItem>> SearchAsync(PromptGalleryQuery query, CancellationToken cancellationToken = default)
            => inner.SearchAsync(query, cancellationToken);

        public Task<Result<PromptVersionSnapshot>> CreateVersionAsync(Guid promptArtifactId, PromptVersionCreateRequest request, CancellationToken cancellationToken = default)
            => inner.CreateVersionAsync(promptArtifactId, request, cancellationToken);

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(Guid promptVersionId, CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotAsync(promptVersionId, cancellationToken);

        public Task<Result<PromptVersionSnapshot>> GetVersionSnapshotAsync(Guid promptArtifactId, int versionNumber, CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotAsync(promptArtifactId, versionNumber, cancellationToken);

        public Task<Result<IReadOnlyList<PromptVersionSnapshot>>> GetVersionSnapshotsAsync(IReadOnlyCollection<Guid> promptVersionIds, CancellationToken cancellationToken = default)
            => inner.GetVersionSnapshotsAsync(promptVersionIds, cancellationToken);

        public Task<Result<IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot>>> GetCompatibilitySnapshotsAsync(IReadOnlyCollection<Guid> promptArtifactIds, CancellationToken cancellationToken = default)
            => inner.GetCompatibilitySnapshotsAsync(promptArtifactIds, cancellationToken);

        public Task<Result> ArchiveAsync(Guid promptArtifactId, bool archived, CancellationToken cancellationToken = default)
            => inner.ArchiveAsync(promptArtifactId, archived, cancellationToken);

        public Task<Result> SetFavoriteAsync(Guid promptArtifactId, bool favorite, CancellationToken cancellationToken = default)
            => inner.SetFavoriteAsync(promptArtifactId, favorite, cancellationToken);

        public Task<Result<PromptCompatibilityResult>> EvaluateCompatibilityAsync(Guid promptArtifactId, PromptGalleryConsumerContext context, CancellationToken cancellationToken = default)
            => inner.EvaluateCompatibilityAsync(promptArtifactId, context, cancellationToken);

        public Task<Result> SetWarningSuppressionAsync(Guid promptArtifactId, PromptGalleryConsumer consumer, PromptCompatibilityIssueCode issueCode, bool suppressed, CancellationToken cancellationToken = default)
            => inner.SetWarningSuppressionAsync(promptArtifactId, consumer, issueCode, suppressed, cancellationToken);
    }
}
