using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class SearchStorageDbContextTests {
    [Fact]
    public void Search_model_contains_only_search_documents() {
        using var context = new SearchDbContext(new DbContextOptionsBuilder<SearchDbContext>()
            .UseInMemoryDatabase($"search-model-{Guid.NewGuid():N}").Options);
        var entity = Assert.Single(context.Model.GetEntityTypes());
        Assert.Equal(typeof(SearchDocument), entity.ClrType);
        Assert.Equal("Infrastructure_SearchDocuments", entity.GetTableName());
        Assert.DoesNotContain(entity.GetProperties(), property => property.IsConcurrencyToken);
        Assert.Throws<InvalidOperationException>(() => context.Set<StorageCatalogRecord>().ToList());
    }

    [Fact]
    public void Storage_model_contains_only_catalog_and_routing_records() {
        using var context = new StorageDbContext(new DbContextOptionsBuilder<StorageDbContext>()
            .UseInMemoryDatabase($"storage-model-{Guid.NewGuid():N}").Options);
        Assert.Equal([typeof(StorageCatalogRecord), typeof(StorageRoutingRule)],
            context.Model.GetEntityTypes().Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.DoesNotContain(context.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()),
            property => property.IsConcurrencyToken);
        Assert.Throws<InvalidOperationException>(() => context.Set<SearchDocument>().ToList());
    }
}
