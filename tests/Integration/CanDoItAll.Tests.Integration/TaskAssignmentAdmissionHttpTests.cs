using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class TaskAssignmentAdmissionHttpTests {
    [Fact]
    public async Task Http_task_create_requires_displayed_lifetime_and_rejects_stale_editor_after_recreation() {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        ProjectWriteAdmission original;
        await using (var scope = host.App.Services.CreateAsyncScope()) {
            var result = await scope.ServiceProvider.GetRequiredService<ProjectsService>()
                .CreateWithAdmissionAsync(new ProjectEditorModel { Name = "Task HTTP admission" });
            Assert.True(result.IsSuccess);
            original = Assert.IsType<ProjectWriteAdmission>(result.Value);
        }
        var path = $"/api/project-structure/projects/{original.ProjectId:D}";
        using var read = await host.Client.PostAsJsonAsync(path + "/structure/read", new ProjectStructureReadRequest(), ProjectStructureHttpContractTestJson.SerializerOptions);
        read.EnsureSuccessStatusCode();
        var displayed = await read.Content.ReadFromJsonAsync<ProjectStructureReadResponse>(ProjectStructureHttpContractTestJson.SerializerOptions);
        Assert.Equal(original, displayed!.ExpectedProjectAdmission);
        var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var request = new ProjectStructureTaskCreateRequest("Captured HTTP task", now, now.AddHours(1));
        using var missing = await host.Client.PostAsJsonAsync(path + "/tasks", request, ProjectStructureHttpContractTestJson.SerializerOptions);
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.Contains("ProjectLifetimeRefreshRequired", await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using (var complete = await host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            complete.Remove(await complete.Set<Project>().SingleAsync(item => item.Id == original.ProjectId));
            complete.Add(new ProjectRetirementRecord { ProjectId = original.ProjectId, LifetimeId = original.LifetimeId, RetiredAtUtc = now });
            await complete.SaveChangesAsync();
            complete.Add(new Project { Id = original.ProjectId, Name = "Recreated HTTP project", Slug = Guid.NewGuid().ToString("N") });
            await complete.SaveChangesAsync();
        }
        using var stale = await host.Client.PostAsJsonAsync(path + "/tasks", request with { ExpectedProjectAdmission = displayed.ExpectedProjectAdmission }, ProjectStructureHttpContractTestJson.SerializerOptions);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using (var complete = await host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            Assert.False(await complete.Set<ProjectObjectRecord>().AnyAsync(item => item.ProjectId == original.ProjectId));
        }
        using var freshRead = await host.Client.PostAsJsonAsync(path + "/structure/read", new ProjectStructureReadRequest(), ProjectStructureHttpContractTestJson.SerializerOptions);
        freshRead.EnsureSuccessStatusCode();
        var fresh = await freshRead.Content.ReadFromJsonAsync<ProjectStructureReadResponse>(ProjectStructureHttpContractTestJson.SerializerOptions);
        Assert.NotEqual(original.LifetimeId, fresh!.ExpectedProjectAdmission!.LifetimeId);
        using var accepted = await host.Client.PostAsJsonAsync(path + "/tasks", request with { ExpectedProjectAdmission = fresh.ExpectedProjectAdmission }, ProjectStructureHttpContractTestJson.SerializerOptions);
        accepted.EnsureSuccessStatusCode();
    }
}
