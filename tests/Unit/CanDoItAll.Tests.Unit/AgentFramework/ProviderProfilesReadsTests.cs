using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Security;
using IProviderAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderAdministrationService;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProviderProfilesReadsTests {
    [Fact]
    public async Task Secret_failure_is_explicit_partial_result_without_hiding_core_failure() {
        var runtime = Port<IProviderRuntimeAdministrationService>((method, _) => method.Name switch {
            nameof(IProviderRuntimeAdministrationService.ListProvidersAsync) => Task.FromResult<IReadOnlyList<ProviderProfile>>([]),
            _ => throw new NotSupportedException(method.Name)
        });
        var administration = Port<IProviderAdministrationService>((method, _) => method.Name switch {
            nameof(IProviderAdministrationService.ListSecretsAsync) => Task.FromException<IReadOnlyList<SecretListItem>>(new InvalidOperationException("fixture-private-secret-catalog-detail")),
            _ => throw new NotSupportedException(method.Name)
        });
        var result = await new ProviderProfilesReads(runtime, administration).LoadCatalogAsync();
        Assert.False(string.IsNullOrWhiteSpace(result.Secrets.Error));
        Assert.DoesNotContain("fixture-private-secret-catalog-detail", result.Secrets.Error, StringComparison.Ordinal);
        Assert.Empty(result.Secrets.Items);
    }

    [Fact]
    public async Task Catalog_failure_propagates_as_core_failure() {
        var runtime = Port<IProviderRuntimeAdministrationService>((_, _) => Task.FromException<IReadOnlyList<ProviderProfile>>(new InvalidOperationException("Core unavailable")));
        var administration = Port<IProviderAdministrationService>((_, _) => Task.FromResult<IReadOnlyList<SecretListItem>>([]));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new ProviderProfilesReads(runtime, administration).LoadCatalogAsync());
        Assert.Equal("Core unavailable", exception.Message);
    }

    [Fact]
    public async Task Owner_cancellation_is_not_a_secret_partial_failure() {
        using var owner = new CancellationTokenSource();
        owner.Cancel();
        var runtime = Port<IProviderRuntimeAdministrationService>((_, args) => {
            Assert.Equal(owner.Token, args![0]);
            return Task.FromResult<IReadOnlyList<ProviderProfile>>([]);
        });
        var administration = Port<IProviderAdministrationService>((_, args) => {
            Assert.Equal(owner.Token, args![0]);
            return Task.FromCanceled<IReadOnlyList<SecretListItem>>(owner.Token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ProviderProfilesReads(runtime, administration).LoadCatalogAsync(owner.Token));
    }

    [Fact]
    public async Task Editor_read_forwards_the_exact_target_and_owner_token() {
        var id = Guid.NewGuid();
        using var owner = new CancellationTokenSource();
        var draft = new ProviderProfileEditorModel { Id = id, Name = "Exact editor" };
        var runtime = Port<IProviderRuntimeAdministrationService>((method, args) => {
            Assert.Equal(nameof(IProviderRuntimeAdministrationService.GetProviderEditorAsync), method.Name);
            Assert.Equal(id, args![0]);
            Assert.Equal(owner.Token, args[1]);
            return Task.FromResult(draft);
        });
        var administration = Port<IProviderAdministrationService>((method, _) => throw new NotSupportedException(method.Name));
        Assert.Same(draft, await new ProviderProfilesReads(runtime, administration).LoadEditorAsync(id, owner.Token));
    }

    private static T Port<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class {
        var proxy = DispatchProxy.Create<T, ReadPortProxy>();
        ((ReadPortProxy)(object)proxy).Read = invoke;
        return proxy;
    }

    public class ReadPortProxy : DispatchProxy {
        public Func<MethodInfo, object?[]?, object?> Read { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Read(targetMethod!, args);
    }
}
