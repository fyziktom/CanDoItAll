using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.AgentFramework.Tools;

public static class ToolExposureDescriptorFactory
{
    public static CapabilityExposureDescriptor Create(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityExposureDescriptor(
            descriptor.Identity,
            descriptor.Identity.Key.Value,
            string.Empty,
            descriptor.ImplementationKey,
            descriptor.RuntimeToolName,
            null,
            null,
            descriptor.Tags,
            descriptor.OperationClassifications,
            descriptor.SideEffectProfile,
            CapabilityAvailabilityState.Available,
            TemplatePath.Create($"Templates/Capabilities/tools/{descriptor.Identity.Key}.json"));
    }
}
