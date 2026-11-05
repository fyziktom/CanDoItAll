using CanDoItAll.Editing;
using CanDoItAll.Modules.Prompts;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Support;

public sealed class PromptGalleryUiTestAccess : IPromptGalleryUiAccess {
    public bool Allowed { get; set; } = true;
    public string Principal { get; set; } = "explicit-test-principal";
    public Task<PromptGalleryUiGrant> OpenAsync(EditingScope scope, CancellationToken cancellationToken) {
        if (!Allowed) {
            throw new UnauthorizedAccessException("Test access denied.");
        }
        return Task.FromResult(new PromptGalleryUiGrant(scope, Principal));
    }
    public ValueTask ValidateAsync(PromptGalleryUiGrant grant, bool write, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Allowed || grant.PrincipalStamp != Principal || grant.Lifetime.IsCancellationRequested) {
            grant.Revoke();
            throw new UnauthorizedAccessException("Test access retired.");
        }
        return ValueTask.CompletedTask;
    }

    public static void Register(IServiceCollection services, PromptGalleryChanges? changes = null, PromptGalleryUiTestAccess? access = null) {
        services.AddLogging();
        services.AddSingleton(changes ?? PromptGalleryObservationTestSupport.Create());
        services.AddSingleton<IPromptGalleryUiAccess>(access ?? new PromptGalleryUiTestAccess());
    }
}
