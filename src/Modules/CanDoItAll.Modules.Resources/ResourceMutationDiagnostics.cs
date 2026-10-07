namespace CanDoItAll.Modules.Resources;

internal static class ResourceMutationDiagnostics {
    public static void PreservePrimary(Exception primary, Action log) {
        try {
            log();
        } catch (Exception diagnosticFailure) {
            primary.Data[nameof(ResourceMutationDiagnostics)] = diagnosticFailure.GetType().FullName;
        }
    }
}
