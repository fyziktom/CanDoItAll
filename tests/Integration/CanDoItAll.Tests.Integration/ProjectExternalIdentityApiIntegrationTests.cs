using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectExternalIdentityApiIntegrationTests {
    private const string ResolvePath = "/api/projects/by-external-key/partner.integration/workspace";

    [Fact]
    public async Task Identity_survives_rename_legacy_save_and_archiving_and_rejects_rebinding_or_duplicates() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        using var created = await host.Client.PostAsJsonAsync("/api/projects/", NewProject());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        var resolution = await host.Client.GetFromJsonAsync<ProjectExternalIdentityResolution>(ResolvePath);
        Assert.Equal(id, resolution!.ProjectId);
        Assert.Equal("partner.integration", resolution.ExternalNamespace);
        Assert.Equal("workspace", resolution.ExternalKey);

        var editor = (await host.Client.GetFromJsonAsync<ProjectEditorModel>($"/api/projects/{id}"))!;
        Assert.Equal(resolution.LifetimeId, editor.ExpectedLifetimeId);
        Assert.Equal("partner.integration", editor.ExternalNamespace);
        Assert.Equal("workspace", editor.ExternalKey);
        editor.Name = "Renamed synthetic project";
        editor.Status = ProjectStatus.Archived;
        using var renamed = await host.Client.PostAsJsonAsync("/api/projects/", editor);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        using var legacy = await host.Client.PostAsJsonAsync("/api/projects/", new { id, name = "Legacy client edit", status = editor.Status });
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        Assert.Equal(resolution, await host.Client.GetFromJsonAsync<ProjectExternalIdentityResolution>(ResolvePath));

        editor.ExternalKey = "another-key";
        using var rebound = await host.Client.PostAsJsonAsync("/api/projects/", editor);
        await AssertErrorAsync(rebound, HttpStatusCode.Conflict, ProjectErrorCodes.ExternalIdentityImmutable);
        using var duplicate = await host.Client.PostAsJsonAsync("/api/projects/", NewProject());
        await AssertErrorAsync(duplicate, HttpStatusCode.Conflict, ProjectErrorCodes.ExternalIdentityConflict);
        using var expected = await host.Client.GetAsync($"{ResolvePath}?expectedLifetimeId={resolution.LifetimeId}");
        Assert.Equal(HttpStatusCode.OK, expected.StatusCode);
    }

    [Fact]
    public async Task Binding_an_existing_project_requires_current_lifetime_and_unknown_ids_do_not_create_projects() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        using var created = await host.Client.PostAsJsonAsync("/api/projects/", new ProjectEditorModel { Name = "Unbound synthetic project" });
        created.EnsureSuccessStatusCode();
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        var editor = (await host.Client.GetFromJsonAsync<ProjectEditorModel>($"/api/projects/{id}"))!;
        var lifetime = editor.ExpectedLifetimeId;
        editor.ExternalNamespace = "partner.integration";
        editor.ExternalKey = "workspace";
        editor.ExpectedLifetimeId = null;
        using var missingLifetime = await host.Client.PostAsJsonAsync("/api/projects/", editor);
        await AssertErrorAsync(missingLifetime, HttpStatusCode.BadRequest, ProjectErrorCodes.ExternalIdentityLifetimeRequired);
        editor.ExpectedLifetimeId = Guid.NewGuid();
        using var stale = await host.Client.PostAsJsonAsync("/api/projects/", editor);
        await AssertErrorAsync(stale, HttpStatusCode.BadRequest, ProjectErrorCodes.LifetimeChanged);
        editor.ExpectedLifetimeId = lifetime;
        using var bound = await host.Client.PostAsJsonAsync("/api/projects/", editor);
        Assert.Equal(HttpStatusCode.OK, bound.StatusCode);
        editor.Id = Guid.NewGuid();
        using var missing = await host.Client.PostAsJsonAsync("/api/projects/", editor);
        await AssertErrorAsync(missing, HttpStatusCode.NotFound, ProjectErrorCodes.NotFound);
    }

    [Fact]
    public async Task Deleted_and_recreated_identity_rejects_previous_lifetime_without_name_fallback() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        using var created = await host.Client.PostAsJsonAsync("/api/projects/", NewProject());
        created.EnsureSuccessStatusCode();
        var original = (await host.Client.GetFromJsonAsync<ProjectExternalIdentityResolution>(ResolvePath))!;
        using var deleted = await host.Client.DeleteAsync($"/api/projects/{original.ProjectId}");
        deleted.EnsureSuccessStatusCode();
        using var missing = await host.Client.GetAsync(ResolvePath);
        await AssertErrorAsync(missing, HttpStatusCode.NotFound, ProjectErrorCodes.NotFound);
        using var sameName = await host.Client.PostAsJsonAsync("/api/projects/", new ProjectEditorModel { Name = NewProject().Name });
        sameName.EnsureSuccessStatusCode();
        using var stillMissing = await host.Client.GetAsync(ResolvePath);
        Assert.Equal(HttpStatusCode.NotFound, stillMissing.StatusCode);
        using var recreated = await host.Client.PostAsJsonAsync("/api/projects/", NewProject());
        recreated.EnsureSuccessStatusCode();
        using var stale = await host.Client.GetAsync($"{ResolvePath}?expectedLifetimeId={original.LifetimeId}");
        await AssertErrorAsync(stale, HttpStatusCode.Conflict, ProjectErrorCodes.LifetimeChanged);
        var current = (await host.Client.GetFromJsonAsync<ProjectExternalIdentityResolution>(ResolvePath))!;
        Assert.NotEqual(original.ProjectId, current.ProjectId);
        Assert.NotEqual(original.LifetimeId, current.LifetimeId);
    }

    [Fact]
    public async Task Concurrent_creates_have_one_winner_and_an_explicit_conflict() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var responses = await Task.WhenAll(
            host.Client.PostAsJsonAsync("/api/projects/", NewProject()),
            host.Client.PostAsJsonAsync("/api/projects/", NewProject()));
        using var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        using var loser = Assert.Single(responses, response => response.StatusCode != HttpStatusCode.OK);
        await AssertErrorAsync(loser, HttpStatusCode.Conflict, ProjectErrorCodes.ExternalIdentityConflict);
        Assert.Equal(await winner.Content.ReadFromJsonAsync<Guid>(),
            (await host.Client.GetFromJsonAsync<ProjectExternalIdentityResolution>(ResolvePath))!.ProjectId);
    }

    [Fact]
    public async Task Read_and_write_scopes_are_enforced_independently() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: true, useInMemoryDatabase: true);
        using var anonymous = await host.Client.GetAsync(ResolvePath);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        SetScope(host, ApiAccessScopeNames.WriteProjects);
        using var forbiddenRead = await host.Client.GetAsync(ResolvePath);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);
        using var created = await host.Client.PostAsJsonAsync("/api/projects/", NewProject());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        SetScope(host, ApiAccessScopeNames.ReadProjects);
        using var allowedRead = await host.Client.GetAsync(ResolvePath);
        Assert.Equal(HttpStatusCode.OK, allowedRead.StatusCode);
        using var forbiddenWrite = await host.Client.PostAsJsonAsync("/api/projects/", NewProject());
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
    }

    [Theory]
    [InlineData(null, "workspace")]
    [InlineData("partner.integration", null)]
    [InlineData("", "")]
    [InlineData("partner.integration", " ")]
    [InlineData("partner.integration", "bad:key")]
    [InlineData("partner.integration", "ends-")]
    [InlineData("partner.integration", "-starts")]
    [InlineData("partner.integration", "wórkspace")]
    public async Task Invalid_identity_is_rejected_before_creation(string? externalNamespace, string? externalKey) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var response = await host.Client.PostAsJsonAsync("/api/projects/", new ProjectEditorModel {
            Name = "Rejected synthetic project", ExternalNamespace = externalNamespace, ExternalKey = externalKey
        });
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ProjectErrorCodes.ExternalIdentityInvalid);
        Assert.Empty((await host.Client.GetFromJsonAsync<ProjectSummary[]>("/api/projects/"))!);
    }

    [Fact]
    public async Task Length_boundaries_and_invalid_lookup_preconditions_are_explicit() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        var project = NewProject();
        project.ExternalKey = new string('a', 101);
        using var oversized = await host.Client.PostAsJsonAsync("/api/projects/", project);
        await AssertErrorAsync(oversized, HttpStatusCode.BadRequest, ProjectErrorCodes.ExternalIdentityInvalid);
        project.ExternalKey = new string('a', 100);
        using var maximum = await host.Client.PostAsJsonAsync("/api/projects/", project);
        Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
        using var emptyLifetime = await host.Client.GetAsync($"{ResolvePath}?expectedLifetimeId={Guid.Empty}");
        await AssertErrorAsync(emptyLifetime, HttpStatusCode.BadRequest, ProjectErrorCodes.ExternalIdentityInvalid);
        using var invalidKey = await host.Client.GetAsync("/api/projects/by-external-key/partner.integration/bad:key");
        await AssertErrorAsync(invalidKey, HttpStatusCode.BadRequest, ProjectErrorCodes.ExternalIdentityInvalid);
    }

    [Fact]
    public async Task OpenApi_publishes_identity_fields_resolution_and_failure_statuses() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty("/api/projects/by-external-key/{externalNamespace}/{externalKey}").GetProperty("get");
        foreach (var status in new[] { "200", "400", "404", "409" }) {
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        }
        Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), parameter => parameter.GetProperty("name").GetString() == "expectedLifetimeId");
        var editor = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("ProjectEditorModel").GetProperty("properties");
        Assert.True(editor.TryGetProperty("externalNamespace", out _));
        Assert.True(editor.TryGetProperty("externalKey", out _));
        Assert.True(paths.GetProperty("/api/projects").GetProperty("post").GetProperty("responses").TryGetProperty("409", out _));
    }

    private static ProjectEditorModel NewProject() => new() {
        Name = "Synthetic workspace project", ExternalNamespace = " Partner.Integration ", ExternalKey = " WORKSPACE "
    };

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code) {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, received {response.StatusCode}: {text}");
        using var document = JsonDocument.Parse(text);
        Assert.Equal(code, Assert.Single(document.RootElement.GetProperty("errors").EnumerateArray()).GetProperty("code").GetString());
    }

    private static void SetScope(ApiTestHost host, string scope) {
        var issued = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new ApiTokenIssueRequest {
            Subject = "synthetic-project-identity-test", Scopes = [scope]
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(issued.TokenType, issued.Token);
    }
}
