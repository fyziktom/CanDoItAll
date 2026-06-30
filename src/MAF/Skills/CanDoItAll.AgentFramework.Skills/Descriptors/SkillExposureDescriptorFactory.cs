using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Skills.Abstractions;

namespace CanDoItAll.AgentFramework.Skills;

public static class SkillExposureDescriptorFactory
{
    public static CapabilityExposureDescriptor Create(SkillDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CapabilityExposureDescriptor(
            descriptor.Identity,
            descriptor.DisplayName,
            descriptor.Description,
            descriptor is RegisteredSkillDescriptor registered ? registered.RegisteredSkillKey : null,
            null,
            null,
            null,
            descriptor.Tags,
            descriptor.OperationClassifications,
            descriptor.SideEffectProfile,
            descriptor.AvailabilityState,
            TemplatePath.Create($"Templates/Capabilities/skills/{descriptor.DescriptorKind.ToString().ToLowerInvariant()}/{descriptor.Identity.Key}.json"));
    }
}
