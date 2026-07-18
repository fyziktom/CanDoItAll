namespace CanDoItAll.Modules.CrmHr;

internal static class RecruitmentWorkspaceContextSelection
{
    public static bool MatchesRequestedApplication(
        Guid? requestedApplicationId,
        Guid? requestedPartyId,
        RecruitmentWorkspaceModel workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        if (!requestedApplicationId.HasValue && !requestedPartyId.HasValue)
        {
            return true;
        }

        return workspace.HasSelectedApplication &&
               (!requestedApplicationId.HasValue ||
                workspace.Application.Id == requestedApplicationId.Value) &&
               (!requestedPartyId.HasValue ||
                workspace.Application.PartyId == requestedPartyId.Value);
    }
}
