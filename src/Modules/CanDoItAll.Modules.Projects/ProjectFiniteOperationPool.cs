using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Projects;

public static class ProjectFiniteOperationPool {
    public static readonly FiniteOperationPoolId Pool = new(new Guid("e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376"));
    public static readonly FiniteReceiptFamilyId DefinitionFamily = new(new Guid("574607ce-cb66-4783-9675-8a59d4383ab4"));
    public const int ProtocolVersion = 1;

    public static FiniteOperationPoolRegistration Create(ProjectDefinitionOperationOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        _ = options.GetLimits();
        return new(Pool, ProtocolVersion, new(options.AdmissionWindow, options.ReplayRetention, options.MutationDeadline,
            options.StatementDeadline, options.LockDeadline, options.MaintenanceInterval, options.MaintenanceWarningAfter),
            [new(DefinitionFamily, ProjectDefinitionReceiptRecord.MaximumPayloadBytes)]);
    }
}
