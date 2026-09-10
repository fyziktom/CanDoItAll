using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class CollaborationDbContextTests {
    [Fact]
    public void Runtime_model_maps_only_owned_records_and_rejects_foreign_queries() {
        using var context = new CollaborationDbContext(new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql("Host=localhost;Database=collaboration_model")
            .Options);
        Type[] ownedTypes = [typeof(CollaborationThreadRecord), typeof(CollaborationParticipantRecord),
            typeof(CollaborationMessageRecord), typeof(CollaborationInboxItemRecord)];

        Assert.Equal(ownedTypes.OrderBy(type => type.Name), context.Model.GetEntityTypes()
            .Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.Throws<InvalidOperationException>(() => context.Set<Project>().ToQueryString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Save_preserves_inserted_tokens_and_restamps_only_modified_records(bool asynchronous, bool acceptAllChanges) {
        await using var context = new CollaborationDbContext(new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var suppliedToken = Guid.NewGuid();
        var thread = new CollaborationThreadRecord { Subject = "Original", ConcurrencyToken = suppliedToken };
        var inbox = new CollaborationInboxItemRecord { ThreadId = thread.Id };
        context.AddRange(thread, inbox);
        await SaveAsync();

        Assert.Equal(suppliedToken, thread.ConcurrencyToken);
        Assert.NotEqual(Guid.Empty, inbox.ConcurrencyToken);
        var inboxToken = inbox.ConcurrencyToken;
        await SaveAsync();
        Assert.Equal(suppliedToken, thread.ConcurrencyToken);
        Assert.Equal(inboxToken, inbox.ConcurrencyToken);

        thread.Subject = "Edited";
        await SaveAsync();
        Assert.NotEqual(suppliedToken, thread.ConcurrencyToken);
        Assert.Equal(inboxToken, inbox.ConcurrencyToken);

        var editedToken = thread.ConcurrencyToken;
        context.Remove(thread);
        await SaveAsync();
        Assert.Equal(editedToken, thread.ConcurrencyToken);

        async Task SaveAsync() {
            if (asynchronous) {
                await context.SaveChangesAsync(acceptAllChanges);
            } else {
                context.SaveChanges(acceptAllChanges);
            }

            if (!acceptAllChanges) {
                context.ChangeTracker.AcceptAllChanges();
            }
        }
    }
}
