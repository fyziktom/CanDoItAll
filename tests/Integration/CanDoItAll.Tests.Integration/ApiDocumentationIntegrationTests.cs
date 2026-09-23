using System.Net;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ApiDocumentationIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Swagger_uses_the_public_https_port_when_user_authentication_is_enabled(bool userAuthenticationEnabled) {
        const int publicHttpsPort = 7443;
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            services => services.AddHttpsRedirection(options => options.HttpsPort = publicHttpsPort),
            useInMemoryDatabase: true,
            apiConfiguration: new Dictionary<string, string?> {
                ["Api:UserAuthentication:Enabled"] = userAuthenticationEnabled.ToString(),
                ["Api:BootstrapAdmin:PasswordHash"] = new ApiPasswordService().Hash(Guid.NewGuid().ToString("N"))
            });
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
            BaseAddress = host.Client.BaseAddress
        };

        foreach (var path in new[] { "/swagger/index.html?flow=login", "/swagger/v1/swagger.json" }) {
            using var response = await client.GetAsync(path, CancellationToken.None);
            Assert.Equal(userAuthenticationEnabled ? HttpStatusCode.TemporaryRedirect : HttpStatusCode.OK, response.StatusCode);
            if (userAuthenticationEnabled) {
                Assert.Equal(new UriBuilder(new Uri(client.BaseAddress!, path)) {
                    Scheme = Uri.UriSchemeHttps,
                    Port = publicHttpsPort
                }.Uri, response.Headers.Location);
            }
        }
        using var apiResponse = await client.GetAsync("/api/access/status", CancellationToken.None);
        Assert.Equal(userAuthenticationEnabled ? HttpStatusCode.BadRequest : HttpStatusCode.OK, apiResponse.StatusCode);
        Assert.Null(apiResponse.Headers.Location);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Swagger_ui_and_documents_are_available_anonymously_when_enabled(bool jwtEnabled) {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: jwtEnabled,
            useInMemoryDatabase: true);

        using var response = await host.Client.GetAsync(
            "/swagger/index.html",
            CancellationToken.None);
        using var documentResponse = await host.Client.GetAsync(
            "/swagger/v1/swagger.json",
            CancellationToken.None);
        using var openApiResponse = await host.Client.GetAsync(
            "/openapi/v1.json",
            CancellationToken.None);
        var content = await response.Content.ReadAsStringAsync(
            CancellationToken.None);

        response.EnsureSuccessStatusCode();
        documentResponse.EnsureSuccessStatusCode();
        openApiResponse.EnsureSuccessStatusCode();
        Assert.Equal(
            await openApiResponse.Content.ReadAsByteArrayAsync(CancellationToken.None),
            await documentResponse.Content.ReadAsByteArrayAsync(CancellationToken.None));
        Assert.Contains("CanDoItAll API", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Swagger_ui_is_not_mapped_when_disabled(bool jwtEnabled) {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: jwtEnabled,
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Swagger_ui_is_not_mapped_when_open_api_is_disabled(bool jwtEnabled) {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: jwtEnabled,
            services => services.PostConfigure<ApiAccessOptions>(options => {
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

        using var openApiResponse = await host.Client.GetAsync(
            "/openapi/v1.json",
            CancellationToken.None);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, openApiResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, uiResponse.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, documentResponse.StatusCode);
    }
}
