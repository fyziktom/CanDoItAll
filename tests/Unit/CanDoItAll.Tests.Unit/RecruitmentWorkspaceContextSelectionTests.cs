using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit;

public sealed class RecruitmentWorkspaceContextSelectionTests
{
    [Fact]
    public void Explicit_missing_application_fails_closed()
    {
        var requestedApplicationId = Guid.NewGuid();

        Assert.False(RecruitmentWorkspaceContextSelection.MatchesRequestedApplication(
            requestedApplicationId,
            requestedPartyId: null,
            CreateWorkspace(selectedApplicationId: null)));
    }

    [Fact]
    public void Explicit_application_must_match_loaded_selection()
    {
        var requestedApplicationId = Guid.NewGuid();

        Assert.False(RecruitmentWorkspaceContextSelection.MatchesRequestedApplication(
            requestedApplicationId,
            requestedPartyId: null,
            CreateWorkspace(Guid.NewGuid())));
        Assert.True(RecruitmentWorkspaceContextSelection.MatchesRequestedApplication(
            requestedApplicationId,
            requestedPartyId: null,
            CreateWorkspace(requestedApplicationId)));
    }

    [Fact]
    public void Generic_recruiting_route_accepts_an_empty_workspace()
        => Assert.True(RecruitmentWorkspaceContextSelection.MatchesRequestedApplication(
            requestedApplicationId: null,
            requestedPartyId: null,
            CreateWorkspace(selectedApplicationId: null)));

    [Fact]
    public void Explicit_party_must_match_the_selected_candidate()
    {
        var requestedPartyId = Guid.NewGuid();

        Assert.False(RecruitmentWorkspaceContextSelection.MatchesRequestedApplication(
            requestedApplicationId: null,
            requestedPartyId,
            CreateWorkspace(Guid.NewGuid(), Guid.NewGuid())));
        Assert.True(RecruitmentWorkspaceContextSelection.MatchesRequestedApplication(
            requestedApplicationId: null,
            requestedPartyId,
            CreateWorkspace(Guid.NewGuid(), requestedPartyId)));
    }

    [Fact]
    public void Explicit_application_and_party_must_describe_the_same_selection()
    {
        var applicationId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        Assert.False(RecruitmentWorkspaceContextSelection.MatchesRequestedApplication(
            applicationId,
            partyId,
            CreateWorkspace(applicationId, Guid.NewGuid())));
    }

    private static RecruitmentWorkspaceModel CreateWorkspace(
        Guid? selectedApplicationId,
        Guid? selectedPartyId = null)
    {
        var application = new RecruitmentApplicationEditorModel
        {
            Id = selectedApplicationId,
            PartyId = selectedPartyId
        };

        return new RecruitmentWorkspaceModel(
            application,
            selectedApplicationId.HasValue,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            [],
            [],
            [],
            new RecruitmentSupportAssignmentsModel(
                Guid.Empty,
                null,
                string.Empty,
                null,
                string.Empty,
                null,
                string.Empty),
            new RecruitmentConversionEditorModel());
    }
}
