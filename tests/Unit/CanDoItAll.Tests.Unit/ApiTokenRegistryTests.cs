using System.Reflection;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class ApiTokenRegistryTests {
    [Fact]
    public async Task TOKEN_LIFECYCLE_records_survive_reopen_and_deletion_never_restores_revocation() {
        await using var environment = CanDoItAllTestEnvironment.Create("token-registry-tests");
        var registry = CreateRegistry(environment);
        var record = CreateRecord("desktop");

        registry.Register(record);
        var reopened = CreateRegistry(environment);
        Assert.Equal(record.Subject, (await reopened.FindAsync(record.Id))!.Subject);
        await reopened.RevokeAsync(record.Id, record.IssuedAtUtc.AddMinutes(1));
        Assert.Equal(ApiTokenStatus.Revoked, (await registry.FindAsync(record.Id))!.GetStatus(record.IssuedAtUtc));
        await registry.DeleteAsync(record.Id);
        Assert.Null(await reopened.FindAsync(record.Id));
    }

    [Fact]
    public async Task TOKEN_PRIVACY_search_is_paged_searchable_and_contains_only_metadata() {
        await using var environment = CanDoItAllTestEnvironment.Create("token-registry-tests");
        var registry = CreateRegistry(environment);
        for (var index = 0; index < 31; index++) {
            registry.Register(CreateRecord($"Desktop {index:D2}"));
        }
        var first = await registry.SearchAsync(new ApiTokenQuery("desktop", PageSize: 25));
        var second = await registry.SearchAsync(new ApiTokenQuery("desktop", Offset: 25, PageSize: 25));
        Assert.Equal(31, first.TotalCount);
        Assert.Equal(25, first.Items.Count);
        Assert.Equal(6, second.Items.Count);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        Assert.Single((await registry.SearchAsync(new ApiTokenQuery("Desktop 07"))).Items);
        Assert.Single((await registry.SearchAsync(new ApiTokenQuery(first.Items[0].Id.ToString("N")[..12]))).Items);
        Assert.Single((await registry.SearchAsync(new ApiTokenQuery(first.Items[0].Id.ToString()))).Items);
        Assert.Equal(31, (await registry.SearchAsync(new ApiTokenQuery(ApiAccessScopeNames.ReadSharedProviderCatalog))).TotalCount);
        var json = await File.ReadAllTextAsync(Path.Combine(environment.ControlPlaneRootPath, "api-tokens", $"{first.Items[0].Id:N}.json"));
        Assert.DoesNotContain("signingKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eyJ", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TOKEN_LIFECYCLE_corrupt_records_fail_explicitly_instead_of_returning_active_state() {
        await using var environment = CanDoItAllTestEnvironment.Create("token-registry-tests");
        var registry = CreateRegistry(environment);
        var record = CreateRecord("corrupt");
        registry.Register(record);
        await File.WriteAllTextAsync(Path.Combine(environment.ControlPlaneRootPath, "api-tokens", $"{record.Id:N}.json"), "{}");

        await Assert.ThrowsAsync<InvalidDataException>(() => registry.FindAsync(record.Id));
        await Assert.ThrowsAsync<InvalidDataException>(() => registry.SearchAsync(new ApiTokenQuery()));
    }

    [Fact]
    public async Task TOKEN_LIFECYCLE_concurrent_writes_preserve_independent_records_and_revoke_delete_order() {
        await using var environment = CanDoItAllTestEnvironment.Create("token-registry-tests");
        var registry = CreateRegistry(environment);
        var records = Enumerable.Range(0, 12).Select(index => CreateRecord($"parallel-{index}")).ToArray();
        await Task.WhenAll(records.Select(record => Task.Run(() => registry.Register(record))));
        Assert.Equal(12, (await registry.SearchAsync(new ApiTokenQuery())).TotalCount);

        var target = records[0];
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => registry.RevokeAsync(target.Id, target.IssuedAtUtc)));
        Assert.Equal(ApiTokenStatus.Revoked, (await registry.FindAsync(target.Id))!.GetStatus(target.IssuedAtUtc));
        await registry.DeleteAsync(target.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.RevokeAsync(target.Id, target.IssuedAtUtc));
        Assert.Null(await registry.FindAsync(target.Id));
    }

    [Fact]
    public async Task TOKEN_LIFECYCLE_duplicate_registration_and_unbounded_pages_are_rejected() {
        await using var environment = CanDoItAllTestEnvironment.Create("token-registry-tests");
        var registry = CreateRegistry(environment);
        var record = CreateRecord("duplicate");
        registry.Register(record);
        Assert.ThrowsAny<IOException>(() => registry.Register(record));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.SearchAsync(new ApiTokenQuery(PageSize: 101)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.SearchAsync(new ApiTokenQuery(Offset: -1)));
    }

    [Fact]
    public void TOKEN_SCOPES_catalog_covers_all_declared_scopes_and_empty_text_stays_empty() {
        var declared = typeof(ApiAccessScopeNames).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!).Order().ToArray();
        Assert.Equal(declared, ApiScopeCatalog.All.Select(scope => scope.Name).Order().ToArray());
        Assert.Empty(ApiScopeCatalog.Parse(" , ; "));
        Assert.Equal(new[] { ApiAccessScopeNames.InvokeSharedProviders, ApiAccessScopeNames.ReadSharedProviderCatalog }.Order(),
            ApiScopeCatalog.Parse($"{ApiAccessScopeNames.InvokeSharedProviders}, {ApiAccessScopeNames.ReadSharedProviderCatalog}; {ApiAccessScopeNames.InvokeSharedProviders}").Order());
    }

    private static FileApiTokenRegistry CreateRegistry(CanDoItAllTestEnvironment environment) {
        var writer = new DurableFileWriter(new PhysicalFileSystemPathPolicyFactory());
        var resolver = new ControlPlanePathResolver(
            Options.Create(new ControlPlaneOptions { RootPath = environment.ControlPlaneRootPath }),
            environment.CreateHostEnvironment("ApiTokenRegistryTests"), writer);
        return new FileApiTokenRegistry(resolver, writer);
    }

    private static ApiTokenRecord CreateRecord(string subject) => new(
        Guid.NewGuid(), subject, subject, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
        [ApiAccessScopeNames.ReadSharedProviderCatalog]);
}
