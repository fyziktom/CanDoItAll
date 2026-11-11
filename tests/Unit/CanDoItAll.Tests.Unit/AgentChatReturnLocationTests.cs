using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentChatReturnLocationTests {
    [Theory]
    [InlineData("https://example.test/projects")]
    [InlineData("//example.test/projects")]
    [InlineData("/\\example.test/projects")]
    [InlineData("/%2fexample.test/projects")]
    [InlineData("/%2f%2fexample.test/projects")]
    [InlineData("/%5cexample.test/projects")]
    [InlineData("/projects/../admin")]
    [InlineData("/projects/%2e%2e/admin")]
    [InlineData("/projects\n/admin")]
    [InlineData("/projects/%0a/admin")]
    [InlineData(" /projects")]
    public void Return_location_rejects_external_or_escaping_routes(string route) {
        Assert.Throws<ArgumentException>(() => new AgentChatReturnLocation(route));
    }

    [Fact]
    public void Resolving_a_return_location_preserves_the_application_base_path_and_route_state() {
        var location = new AgentChatReturnLocation("/projects?tab=gantt#selection");

        var target = location.ResolveUri(new Uri("https://example.test/workspace/"));

        Assert.Equal("https://example.test/workspace/projects?tab=gantt#selection", target.AbsoluteUri);
    }

    [Theory]
    [InlineData("/javascript:alert(1)")]
    [InlineData("/https:example.test/projects")]
    public void A_colon_in_the_first_route_segment_cannot_select_an_external_scheme(string route) {
        var applicationBase = new Uri("https://example.test/workspace/");

        var target = new AgentChatReturnLocation(route).ResolveUri(applicationBase);

        Assert.Equal("https://example.test/workspace" + route, target.AbsoluteUri);
        Assert.True(applicationBase.IsBaseOf(target));
    }

    [Theory]
    [InlineData(ProjectStructureView.Canvas)]
    [InlineData(ProjectStructureView.Gantt)]
    [InlineData(ProjectStructureView.ManagerSummary)]
    public void Frozen_context_retains_canonical_project_view(ProjectStructureView view) {
        var project = Guid.NewGuid();
        var route = ProjectStructureUrlCodec.Route(project, view);
        var scope = new AgentChatContextScope(AgentChatContextScopeId.Create(),
            new(new("project-structure"), new(project.ToString("D"))), "Project",
            surfacePosition: new("projects", "project-structure", "view", route));
        var snapshot = new AgentChatContextSnapshot(scope, [], 1, DateTimeOffset.UtcNow);

        var location = Assert.IsType<AgentChatReturnLocation>(AgentChatReturnLocation.FromContext(snapshot));

        Assert.Equal(route, location.Route);
    }

    [Fact]
    public void Context_without_a_surface_has_no_invented_return_location() {
        Assert.Null(AgentChatReturnLocation.FromContext(null));
        var scope = new AgentChatContextScope(AgentChatContextScopeId.Create(), new(new("module"), new("source")), "Context");
        Assert.Null(AgentChatReturnLocation.FromContext(new(scope, [], 1, DateTimeOffset.UtcNow)));
    }
}
