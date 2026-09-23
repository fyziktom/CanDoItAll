namespace CanDoItAll.SharedProviders.TestUpstream;

internal static class FixtureEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapFixtureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => TypedResults.Ok(new FixtureHealthResponse("healthy")));
        endpoints.MapOpenAiFixtureEndpoints();
        endpoints.MapOllamaFixtureEndpoints();
        endpoints.MapComfyUiFixtureEndpoints();
        endpoints.MapTestControlEndpoints();
        return endpoints;
    }
}
