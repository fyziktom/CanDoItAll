using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectDefinitionHttpTests {
    [Fact]
    public async Task ScalarUpdate_ReturnsExactAcceptedDefinition_WhenMutationLeaseReleaseFails() {
        var failure = new RejectLeaseRelease();
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync("project-definition-release-failure",
            environment => environment.CreatePostgreSqlProfile("definition-release-failure"), services => {
                services.RemoveAll<ProjectStructureLeaseService>();
                services.AddScoped(provider => {
                    var options = new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>())
                        .AddInterceptors(failure).Options;
                    return ActivatorUtilities.CreateInstance<ProjectStructureLeaseService>(provider, new LeaseFactory(options));
                });
            });
        var id = await CreateProjectAsync(host);
        var original = await ReadDefinitionAsync(host.Client, id);
        using var response = await host.Client.PutAsJsonAsync($"/api/project-structure/projects/{id:D}", Request(original, "  Saved despite lease cleanup  "));
        response.EnsureSuccessStatusCode();
        var accepted = (await response.Content.ReadFromJsonAsync<ProjectDefinitionScalarsAcknowledgement>())!;
        Assert.Equal(1, failure.Failures);
        Assert.Equal("Saved despite lease cleanup", accepted.Scalars.Name);
        Assert.NotEqual(original.Expectation.Revision, accepted.Definition.Revision);
        var current = await ReadDefinitionAsync(host.Client, id);
        Assert.Equal(accepted.Definition, current.Expectation);
        Assert.Equal(accepted.Scalars, current.Scalars);
        Assert.Equal(original.Phases.ToArray(), current.Phases.ToArray());
        Assert.Equal(original.Options.ToArray(), current.Options.ToArray());
    }

    [Fact]
    public async Task ScalarUpdate_PreservesChildren_AndRejectsOriginalStaleExpectation() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var id = await CreateProjectAsync(host);
        var original = await ReadDefinitionAsync(host.Client, id);
        using var ownerRead = await host.Client.GetAsync($"/api/projects/{id:D}/definition");
        ownerRead.EnsureSuccessStatusCode();
        var ownerDefinition = (await ownerRead.Content.ReadFromJsonAsync<ProjectDefinitionSnapshot>())!;
        Assert.Equal(original.Expectation, ownerDefinition.Expectation);

        var request = Request(original, "  Accepted name  ");
        using var acceptedResponse = await host.Client.PutAsJsonAsync($"/api/project-structure/projects/{id:D}", request);
        acceptedResponse.EnsureSuccessStatusCode();
        var accepted = (await acceptedResponse.Content.ReadFromJsonAsync<ProjectDefinitionScalarsAcknowledgement>())!;
        Assert.Equal("Accepted name", accepted.Scalars.Name);
        Assert.NotEqual(original.Expectation.Revision, accepted.Definition.Revision);

        using var stale = await host.Client.PutAsJsonAsync($"/api/project-structure/projects/{id:D}", request with { Name = "Stale name" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await AssertStructureErrorAsync(stale, ProjectErrorCodes.DefinitionConflict);
        var current = await ReadDefinitionAsync(host.Client, id);
        Assert.Equal(accepted.Definition, current.Expectation);
        Assert.Equal("Accepted name", current.Scalars.Name);
        Assert.Equal(original.Phases.ToArray(), current.Phases.ToArray());
        Assert.Equal(original.Options.ToArray(), current.Options.ToArray());
    }

    [Fact]
    public async Task ScalarUpdate_MissingOrMismatchedExpectation_MakesNoDefinitionChange() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var id = await CreateProjectAsync(host);
        var original = await ReadDefinitionAsync(host.Client, id);
        using var missing = await host.Client.PutAsJsonAsync($"/api/project-structure/projects/{id:D}",
            Request(original, "Missing expectation") with { ExpectedDefinition = null });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await AssertStructureErrorAsync(missing, ProjectErrorCodes.DefinitionExpectationRequired);

        using var mismatch = await host.Client.PutAsJsonAsync($"/api/project-structure/projects/{Guid.NewGuid():D}", Request(original, "Wrong target"));
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        await AssertStructureErrorAsync(mismatch, ProjectErrorCodes.DefinitionExpectationInvalid);
        Assert.Equal(original.Expectation, (await ReadDefinitionAsync(host.Client, id)).Expectation);
    }

    [Fact]
    public async Task LegacyFullSave_RequiresOriginalExpectation_AndRejectsStaleRevision() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var id = await CreateProjectAsync(host);
        var editor = (await host.Client.GetFromJsonAsync<ProjectEditorModel>($"/api/projects/{id:D}"))!;
        Assert.NotNull(editor.ExpectedDefinition);
        var original = editor.ExpectedDefinition;
        editor.ExpectedDefinition = null;
        using var missing = await host.Client.PostAsJsonAsync("/api/projects", editor);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        editor.ExpectedDefinition = original;
        editor.Name = "Full accepted name";
        using var accepted = await host.Client.PostAsJsonAsync("/api/projects", editor);
        accepted.EnsureSuccessStatusCode();
        editor.Name = "Stale full name";
        using var stale = await host.Client.PostAsJsonAsync("/api/projects", editor);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var failure = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        Assert.Equal(ProjectErrorCodes.DefinitionConflict, failure.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal("Full accepted name", (await ReadDefinitionAsync(host.Client, id)).Scalars.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScalarUpdate_MalformedOriginalExpectation_IsBadRequest(bool emptyRevision) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var id = await CreateProjectAsync(host);
        var original = await ReadDefinitionAsync(host.Client, id);
        var body = JsonSerializer.SerializeToNode(Request(original, "Invalid stamp"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var expected = body["expectedDefinition"]!;
        if (emptyRevision) {
            expected["revision"] = Guid.Empty;
        } else {
            expected["project"]!["lifetimeId"] = Guid.Empty;
        }
        using var response = await host.Client.PutAsJsonAsync($"/api/project-structure/projects/{id:D}", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(original.Expectation, (await ReadDefinitionAsync(host.Client, id)).Expectation);
    }

    [Theory]
    [InlineData("phases", false)]
    [InlineData("phases", true)]
    [InlineData("options", false)]
    [InlineData("options", true)]
    public async Task LegacyFullSave_MissingOrNullReplacementCollection_IsRejected(string collection, bool explicitNull) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var id = await CreateProjectAsync(host);
        var original = await ReadDefinitionAsync(host.Client, id);
        var editor = (await host.Client.GetFromJsonAsync<ProjectEditorModel>($"/api/projects/{id:D}"))!;
        var body = JsonSerializer.SerializeToNode(editor, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        if (explicitNull) {
            body[collection] = null;
        } else {
            Assert.True(body.Remove(collection));
        }
        using var response = await host.Client.PostAsJsonAsync("/api/projects", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var failure = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(ProjectErrorCodes.DefinitionChildInvalid, failure.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        var current = await ReadDefinitionAsync(host.Client, id);
        Assert.Equal(original.Expectation, current.Expectation);
        Assert.Equal(original.Phases.ToArray(), current.Phases.ToArray());
        Assert.Equal(original.Options.ToArray(), current.Options.ToArray());
    }

    [Fact]
    public async Task LegacyFullSave_ExplicitEmptyCollections_DeleteChildren_WhileCreationMayOmitThem() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        using var created = await host.Client.PostAsJsonAsync("/api/projects", new { Name = "Minimal creation" });
        created.EnsureSuccessStatusCode();
        var createdId = await created.Content.ReadFromJsonAsync<Guid>();
        var empty = await ReadDefinitionAsync(host.Client, createdId);
        Assert.Empty(empty.Phases);
        Assert.Empty(empty.Options);

        var id = await CreateProjectAsync(host);
        var editor = (await host.Client.GetFromJsonAsync<ProjectEditorModel>($"/api/projects/{id:D}"))!;
        editor.Phases.Clear();
        editor.Options.Clear();
        using var replaced = await host.Client.PostAsJsonAsync("/api/projects", editor);
        replaced.EnsureSuccessStatusCode();
        var current = await ReadDefinitionAsync(host.Client, id);
        Assert.Empty(current.Phases);
        Assert.Empty(current.Options);
        Assert.NotEqual(editor.ExpectedDefinition, current.Expectation);
    }

    [Fact]
    public async Task Create_RejectsExistingDefinitionExpectation() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var original = await ReadDefinitionAsync(host.Client, await CreateProjectAsync(host));
        using var response = await host.Client.PostAsJsonAsync("/api/project-structure/projects", Request(original, "Invalid create"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStructureErrorAsync(response, ProjectErrorCodes.DefinitionExpectationInvalid);
        var projects = (await host.Client.GetFromJsonAsync<ProjectSummary[]>("/api/project-structure/projects"))!;
        Assert.Single(projects);
    }

    [Theory]
    [InlineData("/api/projects")]
    [InlineData("/api/project-structure/projects")]
    public async Task DefinitionRead_MissingProject_ReturnsNotFound(string route) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        using var response = await host.Client.GetAsync($"{route}/{Guid.NewGuid():D}/definition");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectStructureAgentApiTestHost host) {
        await using var scope = host.App.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ProjectsService>().SaveAsync(new() {
            Name = "Definition test",
            Description = "Original description",
            Objective = "Preserve unrelated child values",
            Phases = [new() { Name = "Design", Goal = "Original phase" }],
            Options = [new() { Category = ProjectOptionCategory.Language, OptionName = "C#", Notes = "Original option" }]
        });
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<ProjectDefinitionSnapshot> ReadDefinitionAsync(HttpClient client, Guid projectId) {
        using var response = await client.GetAsync($"/api/project-structure/projects/{projectId:D}/definition");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDefinitionSnapshot>())!;
    }

    private static ProjectStructureProjectSaveRequest Request(ProjectDefinitionSnapshot definition, string name)
        => new(name, definition.Scalars.Description, definition.Scalars.Objective, definition.Scalars.CurrentPhase,
            definition.Scalars.Status, definition.Scalars.TargetDateUtc) { ExpectedDefinition = definition.Expectation };

    private static async Task AssertStructureErrorAsync(HttpResponseMessage response, string errorCode) {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(errorCode, json.RootElement.GetProperty("error").GetProperty("errorCode").GetString());
    }

    private sealed class LeaseFactory(DbContextOptions<WorkbenchDbContext> options) : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => new(options);
    }

    private sealed class RejectLeaseRelease : SaveChangesInterceptor {
        public int Failures { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<ProjectStructureLeaseRecord>()
                .Any(entry => entry.State == EntityState.Modified && entry.Entity.ReleasedAtUtc.HasValue)) {
                Failures++;
                throw new InvalidOperationException("Injected mutation lease release failure after owner completion.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
