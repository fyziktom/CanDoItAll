using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Skills.Abstractions;

namespace CanDoItAll.AgentFramework.Skills;

public sealed class InlineSkillLoader : IInlineSkillLoader
{
    private const int MaxDescriptionLength = 512;

    public Task<SkillLoadResult> LoadAsync(
        InlineSkillDescriptor descriptor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        if (descriptor.AvailabilityState != CapabilityAvailabilityState.Available)
        {
            return Task.FromResult(Failure(
                descriptor,
                correlationId,
                CapabilityDiagnosticCategory.CapabilityUnavailable,
                "$.availabilityState",
                $"Inline skill '{descriptor.Identity.Key}' is {descriptor.AvailabilityState}.",
                "Enable or replace the inline skill before loading it."));
        }

        if (!SkillName.TryCreate(descriptor.SkillName, out _))
        {
            return Task.FromResult(ValidationFailure(
                descriptor,
                correlationId,
                "$.inlineSkill.name",
                "Inline skill name must use only lowercase ASCII letters, numbers, and single hyphens.",
                "Use a lowercase kebab-case name without leading, trailing, or consecutive hyphens."));
        }

        if (string.IsNullOrWhiteSpace(descriptor.Description) || descriptor.Description.Length > MaxDescriptionLength)
        {
            return Task.FromResult(ValidationFailure(
                descriptor,
                correlationId,
                "$.description",
                "Inline skill description is missing or too long.",
                $"Provide a description between 1 and {MaxDescriptionLength} characters."));
        }

        if (string.IsNullOrWhiteSpace(descriptor.Instructions))
        {
            return Task.FromResult(ValidationFailure(
                descriptor,
                correlationId,
                "$.inlineSkill.instructions",
                "Inline skill instructions are required.",
                "Provide the inline skill instructions."));
        }

        for (var index = 0; index < descriptor.Resources.Count; index++)
        {
            var resource = descriptor.Resources[index];
            if (string.IsNullOrWhiteSpace(resource.Name))
            {
                return Task.FromResult(ValidationFailure(
                    descriptor,
                    correlationId,
                    $"$.inlineSkill.resources[{index}].name",
                    "Inline skill resource name is required.",
                    "Provide a non-empty resource name."));
            }

            if (string.IsNullOrWhiteSpace(resource.Content))
            {
                return Task.FromResult(ValidationFailure(
                    descriptor,
                    correlationId,
                    $"$.inlineSkill.resources[{index}].content",
                    "Inline skill resource content is required.",
                    "Provide non-empty resource content or remove the resource."));
            }
        }

        return Task.FromResult(SkillLoadResult.Success(new LoadedSkill(
            descriptor.Identity,
            SkillDescriptorKind.Inline,
            descriptor.SkillName,
            descriptor.Description,
            descriptor.Instructions,
            descriptor.Resources,
            null,
            null,
            new SkillScriptExecutionPolicy(false, SkillScriptTrustLevel.InlineSkill)), correlationId));
    }

    private static SkillLoadResult ValidationFailure(
        InlineSkillDescriptor descriptor,
        string correlationId,
        string fieldPath,
        string detail,
        string repairHint)
    {
        return Failure(
            descriptor,
            correlationId,
            CapabilityDiagnosticCategory.TemplateValidation,
            fieldPath,
            detail,
            repairHint);
    }

    private static SkillLoadResult Failure(
        InlineSkillDescriptor descriptor,
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
                CapabilityTransportKind.InlineSkill)
        ]);
    }
}
