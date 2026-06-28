using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.AgentFramework.Tools;

public sealed class ToolSetupTestService(
    IExternalProcessToolInvoker processInvoker,
    IExternalHttpToolInvoker httpInvoker) : IToolSetupTestService
{
    public async Task<CapabilitySetupTestResult> TestProcessToolAsync(
        ExternalProcessToolDescriptor descriptor,
        string jsonInput,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var result = await processInvoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, jsonInput, correlationId),
            cancellationToken);
        return ToSetupResult(descriptor.Identity, result);
    }

    public async Task<CapabilitySetupTestResult> TestHttpToolAsync(
        ExternalHttpToolDescriptor descriptor,
        string jsonInput,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var result = await httpInvoker.InvokeAsync(
            descriptor,
            ToolInvocationRequest.Create(descriptor.Identity, descriptor.ImplementationKey, jsonInput, correlationId),
            cancellationToken);
        return ToSetupResult(descriptor.Identity, result);
    }

    private static CapabilitySetupTestResult ToSetupResult(
        CapabilityIdentity identity,
        ToolInvocationResult result)
    {
        return new CapabilitySetupTestResult(
            result.IsSuccess,
            identity,
            result.CorrelationId,
            result.Diagnostics);
    }
}
