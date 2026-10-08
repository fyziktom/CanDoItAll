using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Integration.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowDocumentApiIntegrationTests {
    private const string Route = "/api/workflows/attachments/documents";

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(ApiAccessScopeNames.ReadWorkflows, HttpStatusCode.Forbidden)]
    [InlineData(ApiAccessScopeNames.WriteWorkflows, HttpStatusCode.OK)]
    public async Task Document_upload_requires_workflow_write(string? scope, HttpStatusCode expected) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        if (scope is not null) {
            var issued = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new ApiTokenIssueRequest {
                Subject = "workflow-document-http-test", Scopes = [scope]
            });
            host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(issued.TokenType, issued.Token);
        }
        using var form = Form();
        using var response = await host.Client.PostAsync(Route, form);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK) {
            var result = await response.Content.ReadFromJsonAsync<WorkflowDocumentStagingResult>();
            Assert.NotNull(result);
            Assert.StartsWith("artifacts/workflow-documents/", result.RelativePath, StringComparison.Ordinal);
            Assert.Equal("application/pdf", result.ContentType);
        }
    }

    [Theory]
    [InlineData("extra-text")]
    [InlineData("extra-file")]
    [InlineData("wrong-field")]
    public async Task Ambiguous_multipart_is_rejected(string scenario) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var form = Form(scenario == "wrong-field" ? "document" : "file");
        if (scenario == "extra-text") {
            form.Add(new StringContent("untrusted"), "path");
        } else if (scenario == "extra-file") {
            form.Add(new ByteArrayContent([1]), "file", "second.pdf");
        }
        using var response = await host.Client.PostAsync(Route, form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MultipartFormDataContent Form(string field = "file") {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.7\n%%EOF"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, field, "menu.pdf");
        return form;
    }
}
