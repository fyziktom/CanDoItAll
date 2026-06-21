using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessStepRecoveryOptionContractTests
{
    [Fact]
    public void None_remains_zero_and_runtime_health_models_default_to_none()
    {
        Assert.Equal(0, (int)ProcessStepRecoveryOption.None);
        Assert.Equal(ProcessStepRecoveryOption.None, ProcessStepRunHealthViewModel.Empty.NextRecoveryAction);
        Assert.Equal(ProcessStepRecoveryOption.None, ProcessRunHealthSummaryViewModel.Empty.RecommendedAction);
    }
}
