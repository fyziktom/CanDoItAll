using System.Globalization;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components.History;

public static class ProviderHistoryPresentation {
    public static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
    public static string Model(HistoryEntry entry) => entry.Provider.ResolvedModel?.Value ?? entry.Provider.RequestedModel?.Value ?? "Unknown model";

    public static string Price(HistoryPrice price) {
        var state = PriceState(price.State);
        return price.Amount is { } amount && !string.IsNullOrEmpty(price.Currency)
            ? $"{amount.ToString("0.######", CultureInfo.CurrentCulture)} {price.Currency} · {state}" : state;
    }

    public static string PriceState(HistoryPriceState state) => state switch {
        HistoryPriceState.ProviderReported => "Provider reported",
        HistoryPriceState.CalculatedAtExecution => "Calculated at execution",
        HistoryPriceState.ExplicitFree => "Explicitly free",
        HistoryPriceState.PartialEstimate => "Partial estimate",
        HistoryPriceState.MissingTariff => "Missing tariff",
        HistoryPriceState.MissingUsage => "Missing usage",
        HistoryPriceState.UnsupportedUnit => "Unsupported unit",
        HistoryPriceState.InvalidEvidence => "Invalid evidence",
        HistoryPriceState.Unpriced => "Unpriced",
        _ => "Unknown price state"
    };

    public static string Caller(HistoryCaller caller) => caller.Kind switch {
        HistoryAuthenticationKind.ManagedCredential => $"Key {caller.CredentialId?.Value.ToString("N")[..8] ?? "unavailable"} · {caller.Subject}",
        HistoryAuthenticationKind.TrustedLocalOperator => "Local operator",
        HistoryAuthenticationKind.LegacyAuthenticated => $"Legacy identity · {caller.Subject}",
        HistoryAuthenticationKind.AuthenticationDisabled => "Authentication disabled",
        _ => "Caller unavailable"
    };

    public static string Count(long? value) => value?.ToString("N0", CultureInfo.CurrentCulture) ?? "Unavailable";

    public static string Usage(HistoryUsage usage) => usage.State == HistoryUsageState.Unavailable
        ? "Usage unavailable"
        : $"{usage.InputTokens?.ToString("N0") ?? "?"} input / {usage.OutputTokens?.ToString("N0") ?? "?"} output · {usage.State}";

    public static string Applied(ProviderRequestHistoryQuery query) {
        List<string> parts = [Time(query.FromUtc) + " — " + Time(query.ToUtc),
            query.Scope is HistoryProviderScope.SingleProvider single ? $"Provider {single.Provider.Value:D}" : "All authorized providers",
            $"{query.PageSize} rows per page"];
        Add(parts, "Model", query.Model?.Value);
        Add(parts, "Workload", query.Workload);
        Add(parts, "Operation", query.Operation);
        Add(parts, "Outcome", query.Outcome);
        Add(parts, "Price", query.PriceState);
        Add(parts, "Key", query.CredentialId?.Value);
        Add(parts, "Subject", query.Subject);
        Add(parts, "Issuer", query.Issuer);
        Add(parts, "Request", query.RequestId?.Value);
        Add(parts, "Attempt", query.AttemptId?.Value);
        Add(parts, "Correlation", query.CorrelationId);
        return string.Join(" · ", parts);
    }

    private static void Add(List<string> parts, string label, object? value) {
        if (value is not null) {
            parts.Add($"{label}: {value}");
        }
    }
}
