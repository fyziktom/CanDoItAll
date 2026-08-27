namespace CanDoItAll.SharedProviders.TestUpstream;

internal static class OllamaFixtureEndpointRouteBuilderExtensions
{
    private const string Model = "e2e-ollama";

    public static IEndpointRouteBuilder MapOllamaFixtureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/tags", () => TypedResults.Ok(new
        {
            models = new[]
            {
                new
                {
                    name = Model,
                    model = Model,
                    details = new
                    {
                        family = "fixture"
                    },
                    capabilities = new[]
                    {
                        "completion"
                    }
                }
            }
        }));
        endpoints.MapPost("/api/show", (OllamaShowRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                return Results.BadRequest(new
                {
                    error = "A model is required."
                });
            }

            return Results.Ok(new
            {
                details = new
                {
                    family = "fixture"
                },
                capabilities = new[]
                {
                    "completion"
                }
            });
        });
        endpoints.MapPost("/api/chat", (OllamaChatRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Model) || request.Messages.Count == 0)
            {
                return Results.BadRequest(new
                {
                    error = "A model and at least one message are required."
                });
            }

            return Results.Ok(new
            {
                model = request.Model,
                created_at = "2026-08-26T00:00:00Z",
                message = new
                {
                    role = "assistant",
                    content = "deterministic Ollama fixture response"
                },
                done = true,
                done_reason = "stop",
                prompt_eval_count = 5,
                eval_count = 3
            });
        });
        return endpoints;
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaChatMessage> Messages,
        bool Stream = false);

    private sealed record OllamaShowRequest(string Model, bool Verbose = false);

    private sealed record OllamaChatMessage(string Role, string Content);
}
