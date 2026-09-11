namespace CanDoItAll.Modules.TestLab;

public sealed class TestPlanCommittedSaveException(Guid testPlanId, Exception innerException)
    : Exception($"Test plan '{testPlanId:D}' was saved, but a subsequent operation failed. Reload Test Lab to view the committed state.", innerException) {
    public Guid TestPlanId { get; } = testPlanId;
}
