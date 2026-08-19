using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectManagerSummaryStateStoreTests
{
    [Fact]
    public void GetOrCreate_keys_state_by_profile_and_project()
    {
        var firstProfileId = Guid.NewGuid();
        var secondProfileId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var profileAccessor = new SwitchingProfileAccessor(firstProfileId);
        var store = new ProjectManagerSummaryStateStore(profileAccessor, capacity: 4);

        var firstProfileState = store.GetOrCreate(projectId);
        Assert.Same(firstProfileState, store.GetOrCreate(projectId));

        profileAccessor.SwitchTo(secondProfileId);
        var secondProfileState = store.GetOrCreate(projectId);

        Assert.NotSame(firstProfileState, secondProfileState);
        Assert.Equal(firstProfileId, firstProfileState.ProfileId);
        Assert.Equal(secondProfileId, secondProfileState.ProfileId);
        Assert.Equal(projectId, firstProfileState.ProjectId);
        Assert.Equal(projectId, secondProfileState.ProjectId);
    }

    [Fact]
    public void GetOrCreate_evicts_least_recently_used_profile_project_state()
    {
        var profileAccessor = new SwitchingProfileAccessor(Guid.NewGuid());
        var store = new ProjectManagerSummaryStateStore(profileAccessor, capacity: 2);
        var firstProjectId = Guid.NewGuid();
        var secondProjectId = Guid.NewGuid();
        var thirdProjectId = Guid.NewGuid();
        var firstState = store.GetOrCreate(firstProjectId);
        var secondState = store.GetOrCreate(secondProjectId);

        Assert.Same(firstState, store.GetOrCreate(firstProjectId));
        var thirdState = store.GetOrCreate(thirdProjectId);

        Assert.Same(firstState, store.GetOrCreate(firstProjectId));
        Assert.Same(thirdState, store.GetOrCreate(thirdProjectId));
        Assert.NotSame(secondState, store.GetOrCreate(secondProjectId));
    }

    private sealed class SwitchingProfileAccessor(
        Guid initialProfileId) : IDatabaseProfileRuntimeAccessor
    {
        private Guid currentProfileId = initialProfileId;

        public ResolvedDatabaseProfile ResolveCurrentProfile()
        {
            return CreateProfile(currentProfileId);
        }

        public ResolvedDatabaseProfile ResolveProfile(Guid profileId)
        {
            return CreateProfile(profileId);
        }

        public void SwitchTo(Guid profileId)
        {
            currentProfileId = profileId;
        }

        private static ResolvedDatabaseProfile CreateProfile(Guid profileId)
        {
            return new ResolvedDatabaseProfile(
                new DatabaseProfileRecord
                {
                    Id = profileId,
                    DisplayName = $"Profile {profileId:N}",
                    ProviderKind = DatabaseProviderKind.InMemory,
                    SourceKind = DatabaseProfileSourceKind.InMemory,
                    InMemory = new InMemoryDatabaseProfileConnection
                    {
                        DatabaseName = $"profile-{profileId:N}"
                    }
                },
                DatabaseProfileResolutionSource.ExplicitOverride,
                $"in-memory:{profileId:N}");
        }
    }
}
