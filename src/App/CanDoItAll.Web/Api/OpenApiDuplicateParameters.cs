using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

// A handler parameter and a property of an [AsParameters] object can bind the same route value, for example agentId
// in GET /api/agents/{agentId}/execution-runs. The generator then lists the parameter twice, which OpenAPI forbids (a
// parameter is identified by its name and location). This transformer keeps one of them, preferring a described one.
internal static class OpenApiDuplicateParameters
{
    public static Task TransformOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is not { Count: > 1 } parameters)
        {
            return Task.CompletedTask;
        }

        var kept = new Dictionary<(string? Name, ParameterLocation? In), int>();
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (!kept.TryGetValue((parameter.Name, parameter.In), out var keptIndex))
            {
                kept.Add((parameter.Name, parameter.In), index);
                continue;
            }

            if (string.IsNullOrWhiteSpace(parameters[keptIndex].Description) &&
                !string.IsNullOrWhiteSpace(parameter.Description))
            {
                parameters[keptIndex] = parameter;
            }

            parameters.RemoveAt(index);
            index--;
        }

        return Task.CompletedTask;
    }
}
