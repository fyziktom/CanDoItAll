using System.Collections.Immutable;
using System.Globalization;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.StorageCatalog.UI;

public enum CatalogField { Name, ProviderKind, ConnectionMode, EndpointOrRoot, CredentialSecretId, IsEnabled, IsReadOnly, GatewayBaseUrl, PinOnUpload, Username, BasePath, UseSsl, UsePassiveMode, DisplayOrderText, PortText, DefaultPurposes }
public sealed record CatalogSubmission(CatalogEdit Value, ImmutableArray<long> Revisions, long Revision);

public sealed class CatalogDraft {
    private CatalogEdit value;
    private readonly long[] revisions = new long[Enum.GetValues<CatalogField>().Length];
    private long revision;
    private string displayOrderText;
    private string portText;
    private readonly ValidationMessageStore validation;
    public CatalogDraft(CatalogEdit initial) {
        value = initial;
        displayOrderText = initial.DisplayOrder.ToString(CultureInfo.InvariantCulture);
        portText = initial.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        EditContext = new(this);
        validation = new(EditContext);
    }
    public Guid Origin { get; } = Guid.NewGuid();
    public Guid? Id => value.Id;
    public bool IsSystemDefault => value.IsSystemDefault;
    public CatalogHealthFact Health => value.Health;
    public ImmutableArray<CatalogPurpose> DefaultPurposes => value.DefaultPurposes;
    public EditContext EditContext { get; }
    public int Step { get; set; }
    public bool Deleted { get; private set; }
    public string Name {
        get => value.Name;
        set => Change(CatalogField.Name, this.value with { Name = value });
    }
    public CatalogProvider ProviderKind {
        get => value.ProviderKind;
        set => Change(CatalogField.ProviderKind, this.value with { ProviderKind = value });
    }
    public CatalogConnection ConnectionMode {
        get => value.ConnectionMode;
        set => Change(CatalogField.ConnectionMode, this.value with { ConnectionMode = value });
    }
    public string EndpointOrRoot {
        get => value.EndpointOrRoot;
        set => Change(CatalogField.EndpointOrRoot, this.value with { EndpointOrRoot = value });
    }
    public Guid? CredentialSecretId {
        get => value.CredentialSecretId;
        set => Change(CatalogField.CredentialSecretId, this.value with { CredentialSecretId = value });
    }
    public bool IsEnabled {
        get => value.IsEnabled;
        set => Change(CatalogField.IsEnabled, this.value with { IsEnabled = value });
    }
    public bool IsReadOnly {
        get => value.IsReadOnly;
        set => Change(CatalogField.IsReadOnly, this.value with { IsReadOnly = value });
    }
    public string GatewayBaseUrl {
        get => value.GatewayBaseUrl;
        set => Change(CatalogField.GatewayBaseUrl, this.value with { GatewayBaseUrl = value });
    }
    public bool PinOnUpload {
        get => value.PinOnUpload;
        set => Change(CatalogField.PinOnUpload, this.value with { PinOnUpload = value });
    }
    public string Username {
        get => value.Username;
        set => Change(CatalogField.Username, this.value with { Username = value });
    }
    public string BasePath {
        get => value.BasePath;
        set => Change(CatalogField.BasePath, this.value with { BasePath = value });
    }
    public bool UseSsl {
        get => value.UseSsl;
        set => Change(CatalogField.UseSsl, this.value with { UseSsl = value });
    }
    public bool UsePassiveMode {
        get => value.UsePassiveMode;
        set => Change(CatalogField.UsePassiveMode, this.value with { UsePassiveMode = value });
    }
    public string DisplayOrderText {
        get => displayOrderText;
        set {
            displayOrderText = value;
            Changed(CatalogField.DisplayOrderText);
        }
    }
    public string PortText {
        get => portText;
        set {
            portText = value;
            Changed(CatalogField.PortText);
        }
    }
    private void Change(CatalogField field, CatalogEdit next) {
        value = next;
        Changed(field);
    }
    private void Changed(CatalogField field) {
        revisions[(int)field] = ++revision;
        value = value with { Health = new(CatalogHealth.Unknown, CatalogCapability.None, null, "Configuration edited; previous test does not certify this draft.") };
        EditContext.NotifyFieldChanged(new(this, field.ToString()));
    }
    public void TogglePurpose(CatalogPurpose purpose, bool selected) {
        Change(CatalogField.DefaultPurposes, value with {
            DefaultPurposes = selected ? [.. value.DefaultPurposes.Append(purpose).Distinct()] : [.. value.DefaultPurposes.Where(item => item != purpose)]
        });
    }
    public void ChangeProvider(CatalogProviderChoice choice) {
        ProviderKind = choice.Value;
        ConnectionMode = choice.Template.ConnectionMode;
        UseSsl = choice.Template.UseSsl;
        UsePassiveMode = choice.Template.UsePassiveMode;
        PinOnUpload = choice.Template.PinOnUpload;
        if (choice.Value == CatalogProvider.FileSystem) {
            PortText = string.Empty;
            GatewayBaseUrl = string.Empty;
            Username = string.Empty;
            BasePath = string.Empty;
        }
    }
    public CatalogSubmission? Capture(CatalogEffect effect) {
        validation.Clear();
        if (effect == CatalogEffect.Delete) {
            return new(value, [.. revisions], revision);
        }
        if (!int.TryParse(DisplayOrderText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var order)) {
            validation.Add(new FieldIdentifier(this, nameof(DisplayOrderText)), "Display order must be a complete integer.");
        }
        int? port = null;
        if (!string.IsNullOrWhiteSpace(PortText)) {
            if (int.TryParse(PortText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed is > 0 and <= 65535) {
                port = parsed;
            } else {
                validation.Add(new FieldIdentifier(this, nameof(PortText)), "Port must be a complete integer from 1 to 65535.");
            }
        }
        if (effect == CatalogEffect.Save && string.IsNullOrWhiteSpace(Name)) {
            validation.Add(new FieldIdentifier(this, nameof(Name)), "Storage name is required.");
        }
        if (effect != CatalogEffect.Delete && string.IsNullOrWhiteSpace(EndpointOrRoot)) {
            validation.Add(new FieldIdentifier(this, nameof(EndpointOrRoot)), "A root path or endpoint is required.");
        }
        if (!Enum.IsDefined(ProviderKind) || !Enum.IsDefined(ConnectionMode)) {
            validation.Add(new FieldIdentifier(this, nameof(ProviderKind)), "Choose a supported provider and connection mode explicitly.");
        }
        EditContext.NotifyValidationStateChanged();
        return EditContext.GetValidationMessages().Any() ? null : new(value with { DisplayOrder = order, Port = port }, [.. revisions], revision);
    }
    public void AcceptIdentity(Guid id) => value = value with { Id = id };
    public void AcceptHealth(CatalogHealthFact health, CatalogSubmission submitted) {
        if (revision == submitted.Revision) {
            value = value with { Health = health };
        }
    }
    public void MarkDeleted() => Deleted = true;
    public void Merge(CatalogEdit observed, CatalogSubmission submitted, CatalogRouting routing) {
        if (observed.Id != Id || Deleted) {
            return;
        }
        if (revisions[(int)CatalogField.Name] == submitted.Revisions[(int)CatalogField.Name]) {
            value = value with { Name = observed.Name };
        }
        if (revisions[(int)CatalogField.ProviderKind] == submitted.Revisions[(int)CatalogField.ProviderKind]) {
            value = value with { ProviderKind = observed.ProviderKind };
        }
        if (revisions[(int)CatalogField.ConnectionMode] == submitted.Revisions[(int)CatalogField.ConnectionMode]) {
            value = value with { ConnectionMode = observed.ConnectionMode };
        }
        if (revisions[(int)CatalogField.EndpointOrRoot] == submitted.Revisions[(int)CatalogField.EndpointOrRoot]) {
            value = value with { EndpointOrRoot = observed.EndpointOrRoot };
        }
        if (revisions[(int)CatalogField.CredentialSecretId] == submitted.Revisions[(int)CatalogField.CredentialSecretId]) {
            value = value with { CredentialSecretId = observed.CredentialSecretId };
        }
        if (revisions[(int)CatalogField.IsEnabled] == submitted.Revisions[(int)CatalogField.IsEnabled]) {
            value = value with { IsEnabled = observed.IsEnabled };
        }
        if (revisions[(int)CatalogField.IsReadOnly] == submitted.Revisions[(int)CatalogField.IsReadOnly]) {
            value = value with { IsReadOnly = observed.IsReadOnly };
        }
        if (revisions[(int)CatalogField.GatewayBaseUrl] == submitted.Revisions[(int)CatalogField.GatewayBaseUrl]) {
            value = value with { GatewayBaseUrl = observed.GatewayBaseUrl };
        }
        if (revisions[(int)CatalogField.PinOnUpload] == submitted.Revisions[(int)CatalogField.PinOnUpload]) {
            value = value with { PinOnUpload = observed.PinOnUpload };
        }
        if (revisions[(int)CatalogField.Username] == submitted.Revisions[(int)CatalogField.Username]) {
            value = value with { Username = observed.Username };
        }
        if (revisions[(int)CatalogField.BasePath] == submitted.Revisions[(int)CatalogField.BasePath]) {
            value = value with { BasePath = observed.BasePath };
        }
        if (revisions[(int)CatalogField.UseSsl] == submitted.Revisions[(int)CatalogField.UseSsl]) {
            value = value with { UseSsl = observed.UseSsl };
        }
        if (revisions[(int)CatalogField.UsePassiveMode] == submitted.Revisions[(int)CatalogField.UsePassiveMode]) {
            value = value with { UsePassiveMode = observed.UsePassiveMode };
        }
        if (revisions[(int)CatalogField.DisplayOrderText] == submitted.Revisions[(int)CatalogField.DisplayOrderText]) {
            displayOrderText = observed.DisplayOrder.ToString(CultureInfo.InvariantCulture);
        }
        if (revisions[(int)CatalogField.PortText] == submitted.Revisions[(int)CatalogField.PortText]) {
            portText = observed.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
        if (routing == CatalogRouting.Complete && revisions[(int)CatalogField.DefaultPurposes] == submitted.Revisions[(int)CatalogField.DefaultPurposes]) {
            value = value with { DefaultPurposes = observed.DefaultPurposes };
        }
        AcceptHealth(observed.Health, submitted);
    }
}
