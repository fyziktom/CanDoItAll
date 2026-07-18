using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class CrmHrAgentChatSurfaceBuilderTests
{
    [Fact]
    public void Module_surfaces_publish_stable_locations_without_inventing_selections()
    {
        var surfaces = new[]
        {
            CrmHrAgentChatSurfaceBuilder.BuildHomeSurface(),
            CrmHrAgentChatSurfaceBuilder.BuildDirectorySurface(),
            CrmHrAgentChatSurfaceBuilder.BuildWorkforceSurface(),
            CrmHrAgentChatSurfaceBuilder.BuildRecruitingSurface(),
            CrmHrAgentChatSurfaceBuilder.BuildAssignmentsSurface(),
            CrmHrAgentChatSurfaceBuilder.BuildAgentsSurface()
        };

        Assert.Collection(
            surfaces,
            surface => AssertPosition(surface, "home", "overview", CrmHrAgentChatSurfaceBuilder.HomeRoute),
            surface => AssertPosition(surface, "directory", "party", CrmHrAgentChatSurfaceBuilder.DirectoryRoute),
            surface => AssertPosition(surface, "workforce", "profile", CrmHrAgentChatSurfaceBuilder.WorkforceRoute),
            surface => AssertPosition(surface, "recruiting", "application", CrmHrAgentChatSurfaceBuilder.RecruitingRoute),
            surface => AssertPosition(surface, "assignments", "project", CrmHrAgentChatSurfaceBuilder.AssignmentsRoute),
            surface => AssertPosition(surface, "agents", "agent", CrmHrAgentChatSurfaceBuilder.AgentsRoute));

        Assert.All(surfaces, surface =>
        {
            Assert.Equal(CrmHrAgentChatSurfaceBuilder.SourceKind, surface.Source.Kind.Value);
            Assert.Null(surface.Position.PrimarySelection);
            Assert.Empty(surface.Position.SelectedEntities);
            Assert.Empty(surface.Position.Facts);
        });
    }

    [Fact]
    public void Directory_and_workforce_surfaces_expose_only_party_identity_and_statuses()
    {
        var partyId = Guid.NewGuid();
        var directory = CrmHrAgentChatSurfaceBuilder.BuildDirectorySurface(
            partyId,
            "  Ada\r\nLovelace  ",
            PartyLifecycleStatus.Active);
        var workforce = CrmHrAgentChatSurfaceBuilder.BuildWorkforceSurface(
            partyId,
            "Ada Lovelace",
            PartyLifecycleStatus.Active,
            WorkforceAvailabilityState.NearAvailable);

        AssertSelection(directory.Position, "party", partyId, "Ada Lovelace");
        Assert.Collection(
            directory.Position.Facts,
            fact => AssertFact(fact, "lifecycle-status", "Active"));
        AssertSelection(workforce.Position, "workforce-party", partyId, "Ada Lovelace");
        Assert.Collection(
            workforce.Position.Facts,
            fact => AssertFact(fact, "lifecycle-status", "Active"),
            fact => AssertFact(fact, "availability-status", "NearAvailable"));
    }

    [Fact]
    public void Recruiting_surface_publishes_the_selected_application_and_candidate_party()
    {
        var applicationId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        var surface = CrmHrAgentChatSurfaceBuilder.BuildRecruitingSurface(
            applicationId,
            RecruitmentStage.Interviewing,
            RecruitmentDecision.Pending,
            partyId,
            "Ada Candidate");

        var application = Assert.IsType<AgentChatContextEntityReference>(surface.Position.PrimarySelection);
        Assert.Equal("recruitment-application", application.Kind);
        Assert.Equal(applicationId.ToString("D"), application.Id);
        Assert.Equal("Selected recruitment application", application.DisplayName);
        var candidate = Assert.Single(surface.Position.SelectedEntities);
        Assert.Equal("candidate-party", candidate.Kind);
        Assert.Equal(partyId.ToString("D"), candidate.Id);
        Assert.Equal("Ada Candidate", candidate.DisplayName);
        Assert.Collection(
            surface.Position.Facts,
            fact => AssertFact(fact, "recruitment-stage", "Interviewing"),
            fact => AssertFact(fact, "decision-status", "Pending"));
        var parameterNames = typeof(CrmHrAgentChatSurfaceBuilder)
            .GetMethod(nameof(CrmHrAgentChatSurfaceBuilder.BuildRecruitingSurface))!
            .GetParameters()
            .Select(parameter => parameter.Name ?? throw new InvalidOperationException("A parameter name is required."))
            .ToArray();
        Assert.Equal(
            ["applicationId", "stage", "decision", "partyId", "partyDisplayName"],
            parameterNames);
    }

    [Fact]
    public void Assignments_and_agents_surfaces_publish_allowlisted_semantic_state()
    {
        var projectId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        var assignments = CrmHrAgentChatSurfaceBuilder.BuildAssignmentsSurface(
            projectId,
            "Atlas",
            ProjectStatus.Active);
        var agents = CrmHrAgentChatSurfaceBuilder.BuildAgentsSurface(
            partyId,
            "Support agent",
            PartyLifecycleStatus.Active,
            AiResourceBindingStatus.Bound,
            AiValidationStatus.Approved);

        AssertSelection(assignments.Position, "project", projectId, "Atlas");
        Assert.Collection(
            assignments.Position.Facts,
            fact => AssertFact(fact, "project-status", "Active"));
        AssertSelection(agents.Position, "agent-party", partyId, "Support agent");
        Assert.Collection(
            agents.Position.Facts,
            fact => AssertFact(fact, "lifecycle-status", "Active"),
            fact => AssertFact(fact, "binding-status", "Bound"),
            fact => AssertFact(fact, "validation-status", "Approved"));
    }

    [Fact]
    public void Selected_surfaces_require_complete_defined_semantic_state()
    {
        Assert.Throws<ArgumentException>(() =>
            CrmHrAgentChatSurfaceBuilder.BuildDirectorySurface(
                Guid.NewGuid(),
                "Party",
                lifecycleStatus: null));
        Assert.Throws<ArgumentException>(() =>
            CrmHrAgentChatSurfaceBuilder.BuildAssignmentsSurface(
                Guid.NewGuid(),
                projectName: null,
                projectStatus: ProjectStatus.Active));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrmHrAgentChatSurfaceBuilder.BuildAgentsSurface(
                Guid.NewGuid(),
                "Agent",
                PartyLifecycleStatus.Active,
                (AiResourceBindingStatus)int.MaxValue,
                AiValidationStatus.Approved));
    }

    private static void AssertPosition(
        AgentChatContextSurface context,
        string surface,
        string view,
        string route)
    {
        Assert.Equal(CrmHrAgentChatSurfaceBuilder.Module, context.Position.Module);
        Assert.Equal(surface, context.Position.Surface);
        Assert.Equal(view, context.Position.View);
        Assert.Equal(route, context.Position.Route);
    }

    private static void AssertSelection(
        AgentChatSurfacePosition position,
        string kind,
        Guid id,
        string displayName)
    {
        var selection = Assert.IsType<AgentChatContextEntityReference>(position.PrimarySelection);
        Assert.Equal(kind, selection.Kind);
        Assert.Equal(id.ToString("D"), selection.Id);
        Assert.Equal(displayName, selection.DisplayName);
        Assert.Empty(position.SelectedEntities);
    }

    private static void AssertFact(
        AgentChatContextPositionFact fact,
        string name,
        string value)
    {
        Assert.Equal(name, fact.Name);
        Assert.Equal(value, fact.Value);
    }
}
