using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

// A form handler parameter, such as a bare IFormFile, becomes a property of the generated form request-body schema.
// Neither its XML <param> (which describes the request body as a whole) nor its [Description] attribute reaches that
// property, so this transformer copies the parameter's [Description] to the property when it has no text of its own.
internal static class OpenApiFormParameterDescriptions
{
    public static Task TransformOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.RequestBody?.Content is not { } content)
        {
            return Task.CompletedTask;
        }

        foreach (var parameter in context.Description.ParameterDescriptions)
        {
            if (parameter.Source != BindingSource.Form && parameter.Source != BindingSource.FormFile)
            {
                continue;
            }

            var description = (parameter.ParameterDescriptor as IParameterInfoParameterDescriptor)?
                .ParameterInfo
                .GetCustomAttribute<DescriptionAttribute>()?
                .Description;
            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            foreach (var mediaType in content.Values)
            {
                if (mediaType.Schema?.Properties is not { } properties ||
                    !properties.TryGetValue(parameter.Name, out var property))
                {
                    continue;
                }

                if (property is OpenApiSchemaReference reference)
                {
                    if (string.IsNullOrWhiteSpace(reference.Reference.Description))
                    {
                        reference.Reference.Description = description;
                    }
                }
                else if (property is OpenApiSchema schema && string.IsNullOrWhiteSpace(schema.Description))
                {
                    schema.Description = description;
                }
            }
        }

        return Task.CompletedTask;
    }
}
