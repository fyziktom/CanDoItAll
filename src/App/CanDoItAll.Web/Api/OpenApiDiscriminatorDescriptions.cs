using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

// The generator adds the discriminator property of a polymorphic type (for example "$origin") to each variant schema
// itself, so no XML comment can describe it. This transformer states the value that identifies each variant, taken
// from the base schema's discriminator mapping, when the property has no text of its own.
internal static class OpenApiDiscriminatorDescriptions
{
    public static Task TransformDocumentAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (document.Components?.Schemas is not { } schemas)
        {
            return Task.CompletedTask;
        }

        foreach (var baseSchema in schemas.Values)
        {
            if (baseSchema.Discriminator is not { PropertyName: { Length: > 0 } propertyName, Mapping: { } mapping })
            {
                continue;
            }

            foreach (var (value, variant) in mapping)
            {
                if (variant.Reference.Id is not { } variantName ||
                    !schemas.TryGetValue(variantName, out var variantSchema) ||
                    variantSchema.Properties?.TryGetValue(propertyName, out var property) != true ||
                    property is not OpenApiSchema discriminator ||
                    !string.IsNullOrWhiteSpace(discriminator.Description))
                {
                    continue;
                }

                discriminator.Description =
                    $"Discriminator of this variant: always `{value}`. Read it to tell the variants apart.";
            }
        }

        return Task.CompletedTask;
    }
}
