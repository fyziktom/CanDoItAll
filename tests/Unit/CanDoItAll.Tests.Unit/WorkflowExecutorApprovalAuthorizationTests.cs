using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExecutorApprovalAuthorizationTests
{
    [Fact]
    public void ExecutorAuthorizationCarriesTheReconstructedExternalResponseGrant()
    {
        Assert.NotNull(typeof(WorkflowExecutorApprovalAuthorization).GetProperty("ExternalResponseAuthorization"));
        Assert.NotNull(typeof(WorkflowExecutorInvocationContext).GetProperty("ExternalResponseAuthorization"));
    }

    [Fact]
    public void MafApprovalContinuationCarriesTheReconstructedExternalResponseGrant()
    {
        Assert.NotNull(typeof(MafWorkflowApprovalContinuation).GetProperty("ExternalResponseAuthorization"));
    }
}
