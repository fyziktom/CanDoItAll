using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.AgentFramework.SharedProviders.UI;

public sealed record SharedProviderSourceValues(string Name, string BaseUri, Guid CredentialReference, bool IsEnabled, bool AllowPrivateNetwork);
public sealed record SharedProviderSourceRevisions(long Name, long BaseUri, long CredentialReference, long IsEnabled, long AllowPrivateNetwork);
public sealed record SharedProviderSourceSubmission(Guid DraftId, Guid SourceId, Guid? ExpectedToken,
    SharedProviderSourceValues Values, SharedProviderSourceRevisions Revisions);

public sealed class SharedProviderSourceDraft {
    private SharedProviderSourceValues values;
    private readonly Dictionary<string, long> revisions = [];
    private readonly ValidationMessageStore validation;

    public SharedProviderSourceDraft(Guid sourceId, Guid? expectedToken, SharedProviderSourceValues initial) {
        SourceId = sourceId;
        ExpectedToken = expectedToken;
        values = initial;
        Context = new(this);
        validation = new(Context);
        Context.OnValidationRequested += (_, _) => Validate();
    }

    public Guid Id { get; } = Guid.NewGuid();
    public Guid SourceId { get; }
    public Guid? ExpectedToken { get; private set; }
    public EditContext Context { get; }
    public bool ReadbackConflict { get; set; }
    public string Name {
        get => values.Name;
        set {
            values = values with { Name = value };
            Touch();
        }
    }
    public string BaseUri {
        get => values.BaseUri;
        set {
            values = values with { BaseUri = value };
            Touch();
        }
    }
    public Guid CredentialReference {
        get => values.CredentialReference;
        set {
            values = values with { CredentialReference = value };
            Touch();
        }
    }
    public bool IsEnabled {
        get => values.IsEnabled;
        set {
            values = values with { IsEnabled = value };
            Touch();
        }
    }
    public bool AllowPrivateNetwork {
        get => values.AllowPrivateNetwork;
        set {
            values = values with { AllowPrivateNetwork = value };
            Touch();
        }
    }

    public SharedProviderSourceSubmission Capture() => new(Id, SourceId, ExpectedToken, values,
        new(Revision(nameof(Name)), Revision(nameof(BaseUri)), Revision(nameof(CredentialReference)),
            Revision(nameof(IsEnabled)), Revision(nameof(AllowPrivateNetwork))));
    public bool CanSubmit(SharedProviderSourceSubmission submission) => !ReadbackConflict && submission == Capture() && Context.Validate();

    public void AcceptIdentity(SharedProviderSourceSubmission submission, Guid sourceId, Guid token) {
        if (submission.DraftId != Id || sourceId != SourceId) {
            throw new ArgumentException("The source result belongs to another draft.", nameof(submission));
        }
        ExpectedToken = token;
    }

    public bool Accept(SharedProviderSourceSubmission submission, Guid token, SharedProviderSourceValues accepted) {
        AcceptIdentity(submission, submission.SourceId, token);
        var current = Capture().Revisions;
        values = new(current.Name == submission.Revisions.Name ? accepted.Name : values.Name,
            current.BaseUri == submission.Revisions.BaseUri ? accepted.BaseUri : values.BaseUri,
            current.CredentialReference == submission.Revisions.CredentialReference ? accepted.CredentialReference : values.CredentialReference,
            current.IsEnabled == submission.Revisions.IsEnabled ? accepted.IsEnabled : values.IsEnabled,
            current.AllowPrivateNetwork == submission.Revisions.AllowPrivateNetwork ? accepted.AllowPrivateNetwork : values.AllowPrivateNetwork);
        foreach (var field in new[] {
            (nameof(Name), current.Name == submission.Revisions.Name),
            (nameof(BaseUri), current.BaseUri == submission.Revisions.BaseUri),
            (nameof(CredentialReference), current.CredentialReference == submission.Revisions.CredentialReference),
            (nameof(IsEnabled), current.IsEnabled == submission.Revisions.IsEnabled),
            (nameof(AllowPrivateNetwork), current.AllowPrivateNetwork == submission.Revisions.AllowPrivateNetwork)
        }) {
            if (field.Item2) {
                var identifier = new FieldIdentifier(this, field.Item1);
                validation.Clear(identifier);
                Context.MarkAsUnmodified(identifier);
            }
        }
        Context.NotifyValidationStateChanged();
        ReadbackConflict = false;
        if (current == submission.Revisions) {
            Context.MarkAsUnmodified();
            return true;
        }
        return false;
    }

    private long Revision(string field) => revisions.GetValueOrDefault(field);
    private void Touch([CallerMemberName] string field = "") {
        revisions[field] = Revision(field) + 1;
        Context.NotifyFieldChanged(new(this, field));
    }

    private void Validate() {
        validation.Clear();
        if (string.IsNullOrWhiteSpace(Name)) {
            validation.Add(new FieldIdentifier(this, nameof(Name)), "Enter a source name.");
        }
        if (!Uri.TryCreate(BaseUri.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) {
            validation.Add(new FieldIdentifier(this, nameof(BaseUri)), "Enter an absolute HTTP or HTTPS instance URL.");
        }
        if (CredentialReference == Guid.Empty) {
            validation.Add(new FieldIdentifier(this, nameof(CredentialReference)), "Select a stored source credential.");
        }
        Context.NotifyValidationStateChanged();
    }
}
