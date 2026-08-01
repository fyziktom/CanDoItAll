using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ApiDocumentationIntegrationTests
{
    [Fact]
    public async Task Swagger_ui_is_available_when_enabled()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);

        using var response = await host.Client.GetAsync(
            "/swagger/index.html",
            CancellationToken.None);
        using var documentResponse = await host.Client.GetAsync(
            "/swagger/v1/swagger.json",
            CancellationToken.None);
        var content = await response.Content.ReadAsStringAsync(
            CancellationToken.None);

        response.EnsureSuccessStatusCode();
        documentResponse.EnsureSuccessStatusCode();
        Assert.Contains("CanDoItAll API", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swagger_ui_is_not_mapped_when_disabled()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            services => services.PostConfigure<ApiAccessOptions>(
                options => options.SwaggerUiEnabled = false),
            useInMemoryDatabase: true);

        using var uiResponse = await host.Client.GetAsync(
            "/swagger/index.html",
            CancellationToken.None);
        using var documentResponse = await host.Client.GetAsync(
            "/swagger/v1/swagger.json",
            CancellationToken.None);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, uiResponse.StatusCode);
        documentResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Swagger_ui_is_not_mapped_when_open_api_is_disabled()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            services => services.PostConfigure<ApiAccessOptions>(options =>
            {
                options.OpenApiEnabled = false;
                options.SwaggerUiEnabled = true;
            }),
            useInMemoryDatabase: true);

        using var uiResponse = await host.Client.GetAsync(
            "/swagger/index.html",
            CancellationToken.None);
        using var documentResponse = await host.Client.GetAsync(
            "/swagger/v1/swagger.json",
            CancellationToken.None);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, uiResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, documentResponse.StatusCode);
    }
}
