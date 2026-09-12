using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureDeletionHttpBoundaryTests
{
    [Fact]
    public async Task Delete_routes_require_and_propagate_managed_storage_disposition()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-deletion-http-boundary",
            environment => environment.CreatePostgreSqlProfile("deletion-http-boundary"));
        Guid projectId;
        ProjectStructureNode rejectedNode;
        ProjectStructureNode singleNode;
        ProjectStructureNode firstBatchNode;
        ProjectStructureNode secondBatchNode;
        ProjectStructureNode invalidManagedNode;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            var savedProject = await projects.SaveAsync(new ProjectEditorModel
            {
                Name = "Deletion HTTP boundary",
                Objective = "Require an explicit storage outcome.",
                CurrentPhase = "Validation"
            });
            Assert.True(savedProject.IsSuccess);
            projectId = savedProject.Value;
            rejectedNode = await CreateNoteAsync(workbench, projectId, "Rejected without disposition");
            singleNode = await CreateNoteAsync(workbench, projectId, "Single node");
            firstBatchNode = await CreateNoteAsync(workbench, projectId, "Batch node one");
            secondBatchNode = await CreateNoteAsync(workbench, projectId, "Batch node two");
            invalidManagedNode = await CreateManagedAssetAsync(
                scope.ServiceProvider.GetRequiredService<ProjectAssetCreationService>(),
                workbench,
                projectId);
            await CorruptManagedFingerprintAsync(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>(),
                projectId,
                invalidManagedNode.Id);
        }

        using var unspecifiedResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{projectId:D}/nodes/{rejectedNode.Id}/delete",
            new { });
        Assert.Equal(HttpStatusCode.BadRequest, unspecifiedResponse.StatusCode);
        var unspecifiedError = await ReadAsync<ApiErrorResponse>(unspecifiedResponse);
        Assert.Equal(
            "ProjectStructureManagedStorageDispositionRequired",
            unspecifiedError.Error.ErrorCode);

        var displayed = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{projectId:D}/structure/read",
            new ProjectStructureReadRequest());
        Assert.NotNull(displayed.ExpectedProjectAdmission);

        var singleResult = await PostAndReadAsync<ProjectStructureDeletionResult>(
            host.Client,
            $"/api/project-structure/projects/{projectId:D}/nodes/{singleNode.Id}/delete",
            new ProjectStructureNodeDeleteInput(
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles) {
                ExpectedProjectAdmission = displayed.ExpectedProjectAdmission
            });
        Assert.Equal(1, singleResult.DeletedNodeCount);

        var batchResult = await PostAndReadAsync<ProjectStructureDeletionResult>(
            host.Client,
            $"/api/project-structure/projects/{projectId:D}/nodes/delete",
            new ProjectStructureNodeDeleteBatchInput(
                [firstBatchNode.Id, secondBatchNode.Id],
                ProjectStructureManagedStorageDisposition.RetainManagedFiles) {
                ExpectedProjectAdmission = displayed.ExpectedProjectAdmission
            });
        Assert.Equal(2, batchResult.DeletedNodeCount);

        using var invalidDeleteResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{projectId:D}/nodes/{invalidManagedNode.Id}/delete",
            new ProjectStructureNodeDeleteInput(
                ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles) {
                ExpectedProjectAdmission = displayed.ExpectedProjectAdmission
            });
        Assert.Equal(HttpStatusCode.Conflict, invalidDeleteResponse.StatusCode);
        var invalidDeleteError = await ReadAsync<ApiErrorResponse>(invalidDeleteResponse);
        Assert.Equal(
            "ProjectStructureDeletionBatchPartialCommit",
            invalidDeleteError.Error.ErrorCode);
        Assert.Contains(
            "RetainManagedFiles",
            invalidDeleteError.Error.Message,
            StringComparison.Ordinal);

        await using var verificationScope = host.App.Services.CreateAsyncScope();
        var verificationWorkbench = verificationScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var surface = await verificationWorkbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Id == rejectedNode.Id);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == singleNode.Id);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == firstBatchNode.Id);
        Assert.DoesNotContain(surface.Nodes, node => node.Id == secondBatchNode.Id);
        Assert.Contains(surface.Nodes, node => node.Id == invalidManagedNode.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_requires_displayed_lifetime_and_cannot_delete_a_recreated_project_node(bool batch) {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        ProjectWriteAdmission original;
        ProjectStructureNode node;
        await using (var scope = host.App.Services.CreateAsyncScope()) {
            var created = await scope.ServiceProvider.GetRequiredService<ProjectsService>()
                .CreateWithAdmissionAsync(new ProjectEditorModel { Name = "Deletion lifetime" });
            Assert.True(created.IsSuccess);
            original = Assert.IsType<ProjectWriteAdmission>(created.Value);
            node = await CreateNoteAsync(scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>(),
                original.ProjectId, "Original node");
        }
        var projectPath = $"/api/project-structure/projects/{original.ProjectId:D}";
        var displayed = await PostAndReadAsync<ProjectStructureReadResponse>(host.Client,
            projectPath + "/structure/read", new ProjectStructureReadRequest());
        Assert.Equal(original, displayed.ExpectedProjectAdmission);
        using (var missing = await DeleteAsync(null)) {
            Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
            Assert.Equal("ProjectLifetimeRefreshRequired", (await ReadAsync<ApiErrorResponse>(missing)).Error.ErrorCode);
        }
        await using (var db = await host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            Assert.True(await db.Set<ProjectObjectRecord>().AnyAsync(item => item.NodeKey == node.Id));
            db.Remove(await db.Set<Project>().SingleAsync(item => item.Id == original.ProjectId));
            db.Add(new ProjectRetirementRecord {
                ProjectId = original.ProjectId,
                LifetimeId = original.LifetimeId,
                RetiredAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            db.Add(new Project { Id = original.ProjectId, Name = "Deletion lifetime", Slug = Guid.NewGuid().ToString("N") });
            await db.SaveChangesAsync();
        }
        await using (var scope = host.App.Services.CreateAsyncScope()) {
            node = await CreateNoteAsync(scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>(),
                original.ProjectId, "Replacement node");
        }
        using (var stale = await DeleteAsync(displayed.ExpectedProjectAdmission)) {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        }
        var fresh = await PostAndReadAsync<ProjectStructureReadResponse>(host.Client,
            projectPath + "/structure/read", new ProjectStructureReadRequest());
        Assert.Contains(fresh.Nodes, item => item.Id == node.Id);
        Assert.NotEqual(original.LifetimeId, fresh.ExpectedProjectAdmission!.LifetimeId);
        using var accepted = await DeleteAsync(fresh.ExpectedProjectAdmission);
        accepted.EnsureSuccessStatusCode();
        Assert.Equal(1, (await ReadAsync<ProjectStructureDeletionResult>(accepted)).DeletedNodeCount);

        Task<HttpResponseMessage> DeleteAsync(ProjectWriteAdmission? expected) => batch
            ? host.Client.PostAsJsonAsync(projectPath + "/nodes/delete",
                new ProjectStructureNodeDeleteBatchInput([node.Id], ProjectStructureManagedStorageDisposition.RetainManagedFiles) {
                    ExpectedProjectAdmission = expected
                })
            : host.Client.PostAsJsonAsync(projectPath + $"/nodes/{node.Id}/delete",
                new ProjectStructureNodeDeleteInput(ProjectStructureManagedStorageDisposition.RetainManagedFiles) {
                    ExpectedProjectAdmission = expected
                });
    }

    private static Task<ProjectStructureNode> CreateNoteAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string title)
        => workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                title,
                string.Empty,
                string.Empty,
                $"project:{projectId:D}"));

    private static async Task<T> PostAndReadAsync<T>(
        HttpClient client,
        string path,
        object request)
    {
        using var response = await client.PostAsJsonAsync(path, request);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {await response.Content.ReadAsStringAsync()}");
        }

        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<T>(
                ProjectStructureHttpContractTestJson.SerializerOptions)
            ?? throw new InvalidOperationException(
                $"No {typeof(T).Name} payload was returned.");

    private static async Task<ProjectStructureNode> CreateManagedAssetAsync(
        ProjectAssetCreationService assetCreationService,
        ProjectWorkbenchService workbench,
        Guid projectId)
    {
        var media = await assetCreationService.CreateTextAsync(
            ProjectFileSubtype.Markdown,
            "invalid-http-delete.md",
            "# Invalid HTTP delete");
        return await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Invalid HTTP managed asset",
                string.Empty,
                string.Empty,
                $"project:{projectId:D}",
                ObjectSubtype: "markdown",
                Media: media));
    }

    private static async Task CorruptManagedFingerprintAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid projectId,
        string nodeId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var objectId = await dbContext.Set<ProjectObjectRecord>()
            .Where(record => record.ProjectId == projectId && record.NodeKey == nodeId)
            .Select(record => record.Id)
            .SingleAsync();
        var binding = await dbContext.Set<ProjectNodeBindingRecord>()
            .SingleAsync(record => record.ProjectObjectId == objectId);
        var reference = JsonNode.Parse(binding.StorageObjectReferenceJson)?.AsObject()
            ?? throw new InvalidOperationException("The test asset has no storage reference.");
        var provenance = JsonNode.Parse(reference["metadataJson"]?.GetValue<string>() ?? string.Empty)?.AsObject()
            ?? throw new InvalidOperationException("The test asset has no managed provenance.");
        var fingerprint = provenance["physicalObjectFingerprint"]?.GetValue<string>()
            ?? throw new InvalidOperationException("The test asset has no physical fingerprint.");
        provenance["physicalObjectFingerprint"] = fingerprint[0] == '0'
            ? $"1{fingerprint[1..]}"
            : $"0{fingerprint[1..]}";
        reference["metadataJson"] = provenance.ToJsonString();
        binding.StorageObjectReferenceJson = reference.ToJsonString();
        await dbContext.SaveChangesAsync();
    }

    private sealed record ApiErrorResponse(ApiError Error);

    private sealed record ApiError(string ErrorCode, string Message);
}
