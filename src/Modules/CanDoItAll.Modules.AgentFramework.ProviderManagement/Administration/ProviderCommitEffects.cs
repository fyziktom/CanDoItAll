namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal static class ProviderCommitEffects {
    public static async Task RunAsync(IEnumerable<Func<Task>> effects) {
        List<Exception> failures = [];
        foreach (var effect in effects) {
            try {
                await effect();
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }
        if (failures.Count > 0) {
            throw new AggregateException("The provider change is committed; secondary effects require reconciliation.", failures);
        }
    }
}
