using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Skills.Abstractions;

namespace CanDoItAll.AgentFramework.Skills;

public sealed class RegisteredSkillResolver(IRegisteredSkillRegistry registry) : IRegisteredSkillResolver
{
    public Task<SkillLoadResult> ResolveAsync(
        RegisteredSkillDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        if (descriptor.AvailabilityState == CapabilityAvailabilityState.Retired)
        {
            return Task.FromResult(Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.CapabilityUnavailable,
                "$.availabilityState",
                $"Registered skill '{descriptor.Identity.Key}' is retired.",
                "Remove the retired registered skill or replace it with an active implementation."));
        }

        if (!registry.TryResolve(descriptor.RegisteredSkillKey, out var binding))
        {
            return Task.FromResult(Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.ImplementationMissing,
                "$.registeredSkillKey",
                $"Registered skill key '{descriptor.RegisteredSkillKey}' was not found.",
                "Register the skill binding in the application composition before loading the registered skill."));
        }

        return Task.FromResult(binding.Resolve(descriptor, correlationId));
    }

    private static SkillLoadResult Failure(
        RegisteredSkillDescriptor descriptor,
        string correlationId,
        CapabilityDiagnosticCategory category,
        string fieldPath,
        string detail,
        string repairHint)
    {
        return SkillLoadResult.Failure(correlationId,
        [
            SkillDiagnostics.Create(
                category,
                descriptor,
                fieldPath,
                detail,
                repairHint,
                correlationId,
                CapabilityTransportKind.RegisteredSkill,
                descriptor.RegisteredSkillKey)
        ]);
    }
}
