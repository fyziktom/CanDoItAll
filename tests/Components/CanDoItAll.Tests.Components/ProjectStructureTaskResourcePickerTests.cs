using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class ProjectStructureTaskResourcePickerTests : TestContext
{
    private static readonly Guid PersonId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AgentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WorkflowId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid WorkflowVersionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ProcessId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public ProjectStructureTaskResourcePickerTests()
    {
        Services.AddCanDoItAllBaseLib();
    }

    [Fact]
    public void Kind_filters_hide_and_restore_only_the_requested_resource_group()
    {
        var cut = RenderPicker();

        AssertAllResourceCardsAreVisible(cut);

        cut.Find("[data-testid='task-resource-picker-filter-process']").Click();

        Assert.Empty(cut.FindAll($"[data-testid='resource-option-process-{ProcessId:N}']"));
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-person-{PersonId:N}']"));
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-agent-{AgentId:N}']"));
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-workflow-{WorkflowId:N}']"));
        Assert.Equal(
            "false",
            cut.Find("[data-testid='task-resource-picker-filter-process']").GetAttribute("aria-pressed"));

        cut.Find("[data-testid='task-resource-picker-filter-process']").Click();

        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-process-{ProcessId:N}']"));
    }

    [Fact]
    public void All_resource_groups_can_be_hidden_without_changing_the_selection_contract()
    {
        var cut = RenderPicker();

        foreach (var kind in Enum.GetValues<ProjectStructureTaskResourceKind>())
        {
            cut.Find($"[data-testid='task-resource-picker-filter-{kind.ToString().ToLowerInvariant()}']").Click();
        }

        Assert.Contains("No resources match the current filters", cut.Markup);
        Assert.Empty(cut.FindAll(".resource-card-picker__card"));
    }

    [Fact]
    public void Workflow_card_returns_the_typed_resource_and_version()
    {
        ProjectStructureTaskResourceSelection? selected = null;
        var cut = RenderPicker(selection => selected = selection);

        cut.Find($"[data-testid='resource-option-workflow-{WorkflowId:N}']").Click();

        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Workflow,
                WorkflowId,
                WorkflowVersionId),
            selected);
    }

    [Fact]
    public void Allowed_kinds_bound_both_filters_and_cards()
    {
        var cut = RenderPicker(
            allowedKinds: new HashSet<ProjectStructureTaskResourceKind>
            {
                ProjectStructureTaskResourceKind.Person,
                ProjectStructureTaskResourceKind.Agent
            });

        Assert.NotEmpty(cut.FindAll("[data-testid='task-resource-picker-filter-person']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='task-resource-picker-filter-agent']"));
        Assert.Empty(cut.FindAll("[data-testid='task-resource-picker-filter-workflow']"));
        Assert.Empty(cut.FindAll("[data-testid='task-resource-picker-filter-process']"));
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-person-{PersonId:N}']"));
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-agent-{AgentId:N}']"));
        Assert.Empty(cut.FindAll($"[data-testid='resource-option-workflow-{WorkflowId:N}']"));
        Assert.Empty(cut.FindAll($"[data-testid='resource-option-process-{ProcessId:N}']"));
    }

    [Fact]
    public void Favorite_actions_are_opt_in_and_raise_the_typed_resource()
    {
        ProjectStructureTaskResourceSelection? favorite = null;
        var cut = RenderPicker(favoriteToggled: selection => favorite = selection);

        Assert.Empty(cut.FindAll($"[data-testid='resource-option-agent-{AgentId:N}-favorite']"));

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.ShowFavoriteActions, true));

        var favoriteButton = cut.Find($"[data-testid='resource-option-agent-{AgentId:N}-favorite']");
        Assert.Equal("true", favoriteButton.GetAttribute("aria-pressed"));

        favoriteButton.Click();

        Assert.Equal(
            new ProjectStructureTaskResourceSelection(
                ProjectStructureTaskResourceKind.Agent,
                AgentId),
            favorite);
    }

    [Fact]
    public void Favorite_action_visibility_updates_after_initial_render()
    {
        var cut = RenderPicker(favoriteToggled: _ => { });

        Assert.Empty(cut.FindAll($"[data-testid='resource-option-agent-{AgentId:N}-favorite']"));

        cut.SetParametersAndRender(parameters =>
            parameters.Add(component => component.ShowFavoriteActions, true));

        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-agent-{AgentId:N}-favorite']"));
    }

    private IRenderedComponent<ProjectStructureTaskResourcePicker> RenderPicker(
        Action<ProjectStructureTaskResourceSelection>? selectionChanged = null,
        IReadOnlySet<ProjectStructureTaskResourceKind>? allowedKinds = null,
        Action<ProjectStructureTaskResourceSelection>? favoriteToggled = null,
        bool showFavoriteActions = false)
    {
        return RenderComponent<ProjectStructureTaskResourcePicker>(parameters =>
        {
            parameters
                .Add(component => component.Options, CreateOptions())
                .Add(component => component.DataTestId, "task-resource-picker")
                .Add(component => component.OptionTestIdPrefix, "resource-option")
                .Add(component => component.ShowFavoriteActions, showFavoriteActions);
            if (selectionChanged is not null)
            {
                parameters.Add(component => component.SelectionChanged, selectionChanged);
            }

            if (allowedKinds is not null)
            {
                parameters.Add(component => component.AllowedKinds, allowedKinds);
            }

            if (favoriteToggled is not null)
            {
                parameters.Add(component => component.FavoriteToggled, favoriteToggled);
            }
        });
    }

    private static IReadOnlyList<ProjectStructureTaskResourceOption> CreateOptions()
    {
        return
        [
            new(
                ProjectStructureTaskResourceKind.Person,
                PersonId,
                null,
                "Joe Doe",
                "Person",
                "joe@example.test",
                false,
                false),
            new(
                ProjectStructureTaskResourceKind.Agent,
                AgentId,
                null,
                "Planning agent",
                "AI agent",
                "Plans delivery work.",
                true,
                false),
            new(
                ProjectStructureTaskResourceKind.Workflow,
                WorkflowId,
                WorkflowVersionId,
                "Invoice review",
                "Workflow",
                "Reviews and approves invoices.",
                false,
                false),
            new(
                ProjectStructureTaskResourceKind.Process,
                ProcessId,
                null,
                "Delivery process",
                "Process",
                "Coordinates delivery.",
                false,
                false)
        ];
    }

    private static void AssertAllResourceCardsAreVisible(
        IRenderedComponent<ProjectStructureTaskResourcePicker> cut)
    {
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-person-{PersonId:N}']"));
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-agent-{AgentId:N}']"));
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-workflow-{WorkflowId:N}']"));
        Assert.NotEmpty(cut.FindAll($"[data-testid='resource-option-process-{ProcessId:N}']"));
    }
}
