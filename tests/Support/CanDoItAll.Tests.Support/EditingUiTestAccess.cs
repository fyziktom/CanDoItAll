using CanDoItAll.Editing;

namespace CanDoItAll.Tests.Support;

public sealed class EditingUiTestAccess : IEditingUiAccess {
    public bool Allowed { get; set; } = true;
    public string Principal { get; set; } = "explicit-editing-test-principal";
    public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Allowed) {
            throw new UnauthorizedAccessException("Test editing access denied.");
        }
        return Task.FromResult(new EditingUiGrant(topic, Principal, Principal));
    }

    public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Allowed || grant.PrincipalStamp != Principal || grant.Lifetime.IsCancellationRequested) {
            grant.Revoke();
            throw new UnauthorizedAccessException("Test editing access retired.");
        }
        return ValueTask.CompletedTask;
    }
}
