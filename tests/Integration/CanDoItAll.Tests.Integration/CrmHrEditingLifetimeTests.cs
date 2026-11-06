using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration;

public sealed class CrmHrEditingLifetimeTests {
    [Fact]
    public async Task New_party_acceptance_retargets_observation_before_any_read_back() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var feed = services.GetRequiredService<EditingChangeFeed>();
        var notice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var editor = new PartyDirectoryEditor(parties, services.GetRequiredService<CrmHrChanges>(), feed,
            new EditingUiTestAccess(), NullLogger<EditingObservation>.Instance, NullLogger<PartyDirectoryEditor>.Instance) {
            Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => notice.TrySetResult()
        };
        await editor.NewDraftAsync();
        editor.Draft.DisplayName = "New observed party";
        var created = await editor.SaveAsync();
        Assert.True(created.IsSuccess);
        Assert.Equal(created.Value, editor.Draft.Id);
        editor.Draft.Summary = "Local input after creation";
        var delivered = feed.DeliveredHints;

        Assert.True((await parties.SavePartyAsync(new() { DisplayName = "Unrelated new party" })).IsSuccess);

        Assert.Equal(delivered, feed.DeliveredHints);
        Assert.False(editor.State.IsOutdated);
        Assert.Equal(1, feed.ActiveSubscriptions);
        var peer = (await parties.GetPartyAsync(created.Value))!;
        peer.DisplayName = "Peer changed created party";
        Assert.True((await parties.SavePartyAsync(peer)).IsSuccess);
        await notice.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(editor.State.IsOutdated);
        Assert.Equal("Local input after creation", editor.Draft.Summary);
        await editor.DisposeAsync();
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_create_observation_attach_keeps_commit_knowledge_and_releases_the_temporary_grant(bool denied) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var feed = services.GetRequiredService<EditingChangeFeed>();
        var access = new RetargetAccess(denied);
        await using var editor = new PartyDirectoryEditor(parties, services.GetRequiredService<CrmHrChanges>(), feed,
            access, NullLogger<EditingObservation>.Instance, NullLogger<PartyDirectoryEditor>.Instance) {
            Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => { }
        };
        await editor.NewDraftAsync();
        editor.Draft.DisplayName = "Created before observation attach failure";

        var created = await editor.SaveAsync();

        Assert.True(created.IsSuccess);
        Assert.False(editor.State.IsUnknown);
        Assert.False(editor.State.IsSaving);
        Assert.Equal(0, feed.ActiveSubscriptions);
        Assert.Equal(0, feed.ActiveTopics);
        if (denied) {
            Assert.True(editor.AccessRetired);
            Assert.Null(editor.Draft.Id);
        } else {
            Assert.Equal(created.Value, editor.Draft.Id);
            Assert.Contains("live updates could not attach", editor.Message);
        }
        Assert.Equal(created.Value, Assert.Single(await parties.ListPartiesAsync()).Id);
        access.Fail = false;
        var read = await editor.ReadAsync(created.Value);
        editor.Observe(PartyEditorViewModel.FromEditorModel(read.Content!), read);
        editor.Draft.Summary = "Explicit edit after reopening";
        Assert.True((await editor.SaveAsync()).IsSuccess);
        Assert.Equal(created.Value, Assert.Single(await parties.ListPartiesAsync()).Id);
        Assert.Equal(editor.Draft.Summary, (await parties.GetPartyAsync(created.Value))!.Summary);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_comparison_reads_cannot_reopen_review_or_allow_a_save_during_the_read(bool accountProfile) {
        var gate = new ReadGate();
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new DelayedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var created = await parties.SavePartyAsync(new PartyEditorModel { DisplayName = "Cancelled comparison", PartyType = PartyType.Organization });
        Assert.True(created.IsSuccess);
        var changes = services.GetRequiredService<CrmHrChanges>();
        var feed = services.GetRequiredService<EditingChangeFeed>();
        if (accountProfile) {
            var crm = services.GetRequiredService<CrmService>();
            await using var editor = new AccountProfileEditor(crm, changes, feed, new EditingUiTestAccess(),
                NullLogger<EditingObservation>.Instance, NullLogger<AccountProfileEditor>.Instance) {
                Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => { }
            };
            editor.Observe(await editor.ReadWorkspaceAsync(created.Value, default));
            editor.Draft.CommercialNotes = "Local work retained";
            await AssertCancellationAsync(editor.ReviewAsync, editor.SaveAsync, editor.CancelReview,
                () => editor.ReadingReview, () => editor.State.Review is not null, gate);
            Assert.Equal("Local work retained", editor.Draft.CommercialNotes);
            Assert.Empty((await crm.GetAccountProfileAsync(created.Value))!.CommercialNotes);
        } else {
            await using var editor = new PartyDirectoryEditor(parties, changes, feed, new EditingUiTestAccess(),
                NullLogger<EditingObservation>.Instance, NullLogger<PartyDirectoryEditor>.Instance) {
                Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => { }
            };
            var read = await editor.ReadAsync(created.Value);
            editor.Observe(PartyEditorViewModel.FromEditorModel(read.Content!), read);
            editor.Draft.Notes = "Local work retained";
            await AssertCancellationAsync(editor.ReviewAsync, editor.SaveAsync, editor.CancelReview,
                () => editor.ReadingReview, () => editor.State.Review is not null, gate);
            Assert.Equal("Local work retained", editor.Draft.Notes);
            Assert.Empty((await parties.GetPartyAsync(created.Value))!.Notes);
        }
    }

    [Fact]
    public async Task One_hundred_party_open_review_close_cycles_release_their_observations_and_drafts() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var id = (await parties.SavePartyAsync(new PartyEditorModel { DisplayName = "Party lifetime" })).Value;
        var feed = services.GetRequiredService<EditingChangeFeed>();
        await using var editor = new PartyDirectoryEditor(parties, services.GetRequiredService<CrmHrChanges>(), feed,
            new EditingUiTestAccess(), NullLogger<EditingObservation>.Instance, NullLogger<PartyDirectoryEditor>.Instance) {
            Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => { }
        };
        var references = new List<WeakReference>();
        for (var index = 0; index < 100; index++) {
            references.Add(await OpenPartyAsync(editor, id));
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
        }

        Collect();

        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    [Fact]
    public async Task One_hundred_account_open_review_close_cycles_release_their_observations_and_drafts() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var id = (await parties.SavePartyAsync(new PartyEditorModel { DisplayName = "Account lifetime", PartyType = PartyType.Organization })).Value;
        var feed = services.GetRequiredService<EditingChangeFeed>();
        await using var editor = new AccountProfileEditor(services.GetRequiredService<CrmService>(), services.GetRequiredService<CrmHrChanges>(), feed,
            new EditingUiTestAccess(), NullLogger<EditingObservation>.Instance, NullLogger<AccountProfileEditor>.Instance) {
            Dispatch = work => work(), Changed = () => { }, Retired = () => { }, Notice = () => { }
        };
        var references = new List<WeakReference>();
        for (var index = 0; index < 100; index++) {
            references.Add(await OpenAccountAsync(editor, id));
            Assert.Equal(0, feed.ActiveSubscriptions);
            Assert.Equal(0, feed.ActiveTopics);
        }

        Collect();

        Assert.All(references, reference => Assert.False(reference.IsAlive));
    }

    private sealed class RetargetAccess(bool denied) : IEditingUiAccess {
        private readonly EditingUiTestAccess inner = new();
        public bool Fail { get; set; } = true;
        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) => Fail && topic.ResourceId.HasValue
            ? Task.FromException<EditingUiGrant>(denied ? new UnauthorizedAccessException("Retarget denied.") : new InvalidOperationException("Retarget unavailable."))
            : inner.OpenAsync(topic, cancellationToken);
        public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) => inner.ValidateAsync(grant, write, cancellationToken);
    }

    private static async Task<WeakReference> OpenPartyAsync(PartyDirectoryEditor editor, Guid id) {
        var read = await editor.ReadAsync(id);
        editor.Observe(PartyEditorViewModel.FromEditorModel(read.Content!), read);
        var reference = new WeakReference(editor.Draft);
        editor.Draft.Notes = "Uncommitted lifetime draft";
        await editor.ReviewAsync();
        editor.State.CancelReview();
        await editor.NewDraftAsync();
        return reference;
    }

    private static async Task<WeakReference> OpenAccountAsync(AccountProfileEditor editor, Guid id) {
        editor.Observe(await editor.ReadWorkspaceAsync(id, default));
        var reference = new WeakReference(editor.Draft);
        editor.Draft.CommercialNotes = "Uncommitted lifetime draft";
        await editor.ReviewAsync();
        editor.State.CancelReview();
        editor.Clear();
        await editor.AuthorizeCatalogReadAsync(default);
        return reference;
    }

    private static void Collect() {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static async Task AssertCancellationAsync(Func<Task> review, Func<Task<Result<Guid>>> save,
        Action cancel, Func<bool> reading, Func<bool> hasReview, ReadGate gate) {
        gate.Armed = true;
        var pending = review();
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(reading());
            Assert.True((await save()).IsFailure);
            cancel();
            Assert.False(reading());
        } finally {
            gate.Release.TrySetResult();
        }
        await pending;
        Assert.False(hasReview());
    }

    private sealed class DelayedFactory(DbContextOptions<CrmHrDbContext> options, ReadGate gate) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(options);
        public async Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (gate.Armed && Interlocked.CompareExchange(ref gate.Delayed, 1, 0) == 0) {
                gate.Entered.TrySetResult();
                await gate.Release.Task;
            }
            return CreateDbContext();
        }
    }

    private sealed class ReadGate {
        public bool Armed;
        public int Delayed;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
