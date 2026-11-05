using CanDoItAll.Editing;

namespace CanDoItAll.AgentFramework.SharedProviders.UI;

public sealed record SharedProviderSourceBaseline(Guid Token, SharedProviderSourceValues Values);
public sealed record SharedProviderSourceConnection(string BaseUri, Guid Credential, bool Enabled, bool PrivateNetwork) {
    public static SharedProviderSourceConnection From(SharedProviderSourceValues values)
        => new(values.BaseUri, values.CredentialReference, values.IsEnabled, values.AllowPrivateNetwork);
}

public sealed class SharedProviderSourceComparison {
    private readonly SharedProviderSourceValues local;
    public SharedProviderSourceComparison(SharedProviderSourceBaseline? original, SharedProviderSourceValues local,
        SharedProviderSourceBaseline current, bool incomplete) {
        if (current.Token == Guid.Empty) {
            throw new ArgumentException("Source comparison requires a current saved version.", nameof(current));
        }
        this.local = local;
        Current = current;
        Incomplete = incomplete;
        if (original is not null) {
            Name = new(original.Values.Name, local.Name, current.Values.Name, StringComparer.Ordinal);
            Connection = new(SharedProviderSourceConnection.From(original.Values), SharedProviderSourceConnection.From(local),
                SharedProviderSourceConnection.From(current.Values));
            if (Connection.CurrentChanged) {
                Connection.RequireChoice();
            }
        }
    }

    public SharedProviderSourceBaseline Current { get; }
    public MergeField<string>? Name { get; }
    public MergeField<SharedProviderSourceConnection>? Connection { get; }
    public bool Incomplete { get; }
    public bool KeepIncomplete { get; set; }
    public bool KeepWholeDraft { get; set; }
    public bool IsStale { get; private set; }
    public bool CanApply => !IsStale && (Name is null ? KeepWholeDraft
        : Name.IsResolved && (Incomplete ? KeepIncomplete : Connection!.IsResolved));
    public void Invalidate() => IsStale = true;
    public string LocalDescription => $"{local.Name}; {Describe(SharedProviderSourceConnection.From(local))}";
    public string CurrentDescription => $"{Current.Values.Name}; {Describe(SharedProviderSourceConnection.From(Current.Values))}";
    public static string Describe(SharedProviderSourceConnection value) {
        var address = Uri.TryCreate(value.BaseUri, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
            string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
                ? uri.AbsoluteUri : "Unfinished instance URL retained privately";
        return $"{address}; credential reference {value.Credential:D}; enabled {value.Enabled}; private network {value.PrivateNetwork}";
    }

    public SharedProviderSourceValues Result() {
        if (!CanApply) {
            throw new InvalidOperationException("Resolve the current source comparison before applying it locally.");
        }
        if (Name is null) {
            return local;
        }
        var connection = Incomplete ? SharedProviderSourceConnection.From(local) : Connection!.Result;
        return new(Name.Result, connection.BaseUri, connection.Credential, connection.Enabled, connection.PrivateNetwork);
    }
}
