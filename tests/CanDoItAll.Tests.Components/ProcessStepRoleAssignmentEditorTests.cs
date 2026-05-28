using Bunit;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessStepRoleAssignmentEditorTests
{
    [Fact]
    public void Render_SB01_INV_003_exposes_accountable_responsibility_option()
    {
        using var context = new TestContext();
        var roleId = Guid.NewGuid();
        var assignment = new ProcessStepRoleRequirementEditorModel
        {
            Id = Guid.NewGuid(),
            RoleRequirementId = roleId,
            ResponsibilityKind = ProcessResponsibilityKind.Responsible
        };

        var cut = context.RenderComponent<ProcessStepRoleAssignmentEditor>(
            ComponentParameter.CreateParameter(nameof(ProcessStepRoleAssignmentEditor.Model), assignment),
            ComponentParameter.CreateParameter(nameof(ProcessStepRoleAssignmentEditor.AvailableRoles), new List<ProcessRoleEditorModel>
            {
                new()
                {
                    Id = roleId,
                    DisplayName = "Delivery owner"
                }
            }));

        var responsibilitySelect = cut.FindAll("select")[1];
        Assert.Contains("Accountable", cut.Markup);

        responsibilitySelect.Change(ProcessResponsibilityKind.Accountable.ToString());

        Assert.Equal(ProcessResponsibilityKind.Accountable, assignment.ResponsibilityKind);
    }
}
