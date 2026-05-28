using Bunit;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessRoleEditorFormTests
{
    [Fact]
    public void Render_SB01_INV_001_preserves_template_executor_kind_options()
    {
        using var context = new TestContext();
        var role = new ProcessRoleEditorModel
        {
            Id = Guid.NewGuid(),
            Key = "implementation-owner",
            DisplayName = "Implementation owner",
            PreferredExecutorKind = ProcessRoleExecutorKindOptions.PersonOrAgent,
            PreferredWorkflowDefinitionId = Guid.NewGuid(),
            PreferredWorkflowVersionId = Guid.NewGuid()
        };

        var cut = context.RenderComponent<ProcessRoleEditorForm>(
            ComponentParameter.CreateParameter(nameof(ProcessRoleEditorForm.Model), role));

        var optionValues = cut.FindAll("[data-testid='processes-role-executor-kind-input'] option")
            .Select(option => option.GetAttribute("value"))
            .ToArray();

        Assert.Contains(ProcessRoleExecutorKindOptions.Person, optionValues);
        Assert.Contains(ProcessRoleExecutorKindOptions.Agent, optionValues);
        Assert.Contains(ProcessRoleExecutorKindOptions.PersonOrAgent, optionValues);
        Assert.Contains(ProcessExecutorKindNames.AiAgent, optionValues);
        Assert.Contains(ProcessExecutorKindNames.Workflow, optionValues);

        cut.Find("[data-testid='processes-role-executor-kind-input']")
            .Change(ProcessRoleExecutorKindOptions.Agent);

        Assert.Equal(ProcessRoleExecutorKindOptions.Agent, role.PreferredExecutorKind);
        Assert.Null(role.PreferredWorkflowDefinitionId);
        Assert.Null(role.PreferredWorkflowVersionId);
    }

    [Theory]
    [InlineData("person", "person")]
    [InlineData("agent", "agent")]
    [InlineData("person-or-agent", "person-or-agent")]
    [InlineData("AI agent", "AI agent")]
    [InlineData("Workflow", "Workflow")]
    public void NormalizeForSelection_SB01_INV_002_accepts_current_template_executor_vocabulary(
        string rawValue,
        string expectedSelection)
    {
        Assert.Equal(expectedSelection, ProcessRoleExecutorKindOptions.NormalizeForSelection(rawValue));
    }
}
