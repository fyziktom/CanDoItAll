using System.Text.Json.Nodes;
using CanDoItAll.Web;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiDocumentationDocumentFixture : IAsyncLifetime
{
    private ApiTestHost? host;

    public byte[] OpenApiBytes { get; private set; } = [];

    public byte[] SwaggerBytes { get; private set; } = [];

    public JsonObject Document { get; private set; } = new();

    public async Task InitializeAsync()
    {
        // Program.cs maps the runtime routes and, in Development, the diagnostics routes outside the API groups.
        host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true,
            configureApplication: application =>
            {
                application.MapDevelopmentDiagnosticsEndpoints();
                application.MapRuntimeEndpoints();
            });
        OpenApiBytes = await host.Client.GetByteArrayAsync("/openapi/v1.json");
        SwaggerBytes = await host.Client.GetByteArrayAsync("/swagger/v1/swagger.json");
        Document = JsonNode.Parse(OpenApiBytes)?.AsObject()
            ?? throw new InvalidOperationException("The OpenAPI document is not a JSON object.");
    }

    public async Task DisposeAsync()
    {
        if (host is not null)
        {
            await host.DisposeAsync();
        }
    }

    public JsonObject Operation(string path, string method)
        => Document["paths"]?[path]?[method]?.AsObject()
            ?? throw new InvalidOperationException($"Operation {method.ToUpperInvariant()} {path} is not documented.");

    public JsonObject Schema(string name)
        => Document["components"]?["schemas"]?[name]?.AsObject()
            ?? throw new InvalidOperationException($"Component schema '{name}' is not documented.");
}
