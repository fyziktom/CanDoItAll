using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    public enum LegacyLifecycleEntry { Delete, Add, Remove, Reconnect }

    [Theory]
    [InlineData(LegacyLifecycleEntry.Delete)]
    [InlineData(LegacyLifecycleEntry.Add)]
    [InlineData(LegacyLifecycleEntry.Remove)]
    [InlineData(LegacyLifecycleEntry.Reconnect)]
    public Task Identity_only_service_entry_refuses_before_any_owner_or_participant_effect(LegacyLifecycleEntry entry)
        => WithFixtureAsync(async fixture => {
            var parent = await fixture.SeedAsync("Original parent");
            var child = await fixture.SeedAsync("Original child");
            var next = await fixture.SeedAsync("Other parent");
            var ensure = await fixture.SubmitIntentAsync(new ProjectLifecycleCommand.EnsureLink(parent.Project, child.Project));
            Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(ensure, fixture.Invocation));
            var before = await fixture.StateAsync();
            var projects = fixture.Services.GetRequiredService<ProjectsService>();
            await Assert.ThrowsAsync<ProjectLifecycleOriginalIntentRequiredException>(async () => {
                switch (entry) {
                    case LegacyLifecycleEntry.Delete:
                        await projects.DeleteAsync(child.Project.ProjectId, expectedProjectAdmission: child.Project);
                        break;
                    case LegacyLifecycleEntry.Add:
                        await projects.AddSubprojectAsync(next.Project.ProjectId, child.Project.ProjectId,
                            expectedProjectAdmissions: [next.Project, child.Project]);
                        break;
                    case LegacyLifecycleEntry.Remove:
                        await projects.RemoveSubprojectAsync(parent.Project.ProjectId, child.Project.ProjectId);
                        break;
                    case LegacyLifecycleEntry.Reconnect:
                        await projects.ReconnectSubprojectAsync(child.Project.ProjectId, parent.Project.ProjectId,
                            next.Project.ProjectId, expectedProjectAdmissions: [child.Project, parent.Project, next.Project]);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(entry));
                }
            });
            before.RequireEqual(await fixture.StateAsync());
            Assert.Equal(0, fixture.Participant.Prepared);
            Assert.Equal(0, fixture.Participant.Completed);
        });
}
