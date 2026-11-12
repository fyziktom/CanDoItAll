using CanDoItAll.Modules.Workspace.DataSources.Contracts;

namespace CanDoItAll.Workspace.DataSources.UI;

public sealed record DataSourceReceipt(Guid OperationId, DataSourcesContext Context, DataSourceAction Action,
    Guid TargetProfileId, Guid? SourceProfileId, DataSourceResult? Result);

public sealed class DataSourceOperationLedger {
    private readonly List<DataSourceReceipt> receipts = [];
    public event Action? Changed;
    public bool IsBusy { get; private set; }
    public bool HasUnknown => receipts.Any(item => item.Result?.Outcome is DataSourceOutcome.Unknown or DataSourceOutcome.Partial);
    public bool CanDispatch => !IsBusy && !HasUnknown;
    public IReadOnlyList<DataSourceReceipt> Receipts => receipts.AsReadOnly();

    public async Task<DataSourceResult?> RunAsync(DataSourcesContext context, DataSourceAction action,
        Guid targetId, Guid? sourceId, Func<Task<DataSourceResult>> dispatch) {
        if (!CanDispatch) {
            return null;
        }
        var entry = new DataSourceReceipt(Guid.NewGuid(), context, action, targetId, sourceId, null);
        receipts.Add(entry);
        IsBusy = true;
        Changed?.Invoke();
        DataSourceResult result;
        try {
            result = await dispatch();
        } catch (DataSourcesException exception) {
            result = new(targetId, DataSourceOutcome.Refused, Describe(exception.Failure));
        } catch {
            result = new(targetId, DataSourceOutcome.Unknown,
                "The outcome was not acknowledged. Inspect the original profile and group results before any further operation. Reopening this view does not retry or clear this uncertainty.");
        } finally {
            IsBusy = false;
        }
        receipts[receipts.IndexOf(entry)] = entry with { TargetProfileId = result.ProfileId, Result = result };
        while (receipts.Count > 16 && receipts[0].Result?.Outcome is DataSourceOutcome.Confirmed or DataSourceOutcome.Refused) {
            receipts.RemoveAt(0);
        }
        Changed?.Invoke();
        return result;
    }

    internal static string Describe(DataSourceFailure failure) => failure switch {
        DataSourceFailure.Missing => "The exact saved profile no longer exists. Refresh the list before selecting another profile.",
        DataSourceFailure.Locked => "This profile is owned by startup configuration or is the current/pending runtime selection.",
        DataSourceFailure.InvalidRequest => "The request is incomplete or its current prerequisites are not satisfied.",
        DataSourceFailure.StaleContext => "The original runtime context is no longer current. Reopen Data Sources.",
        _ => "The data source could not be read. Existing draft and acknowledged progress are retained."
    };
}
