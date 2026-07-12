using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowCommandExecutorSafetyTests
{
    [Fact]
    public void CommandProcessRemainsPlannedWithActionableSafetyBlockers()
    {
        var descriptor = Assert.Single(
            BuiltInWorkflowExecutorDescriptors.Planned,
            item => item.Id == WorkflowExecutorIds.CommandProcess);

        Assert.False(descriptor.IsImplemented);
        Assert.False(descriptor.Availability.IsRunnable);
        Assert.Contains("cancellation", descriptor.Availability.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("credential", descriptor.Availability.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("masked", descriptor.Availability.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approval", descriptor.Availability.Message, StringComparison.OrdinalIgnoreCase);
    }
}
