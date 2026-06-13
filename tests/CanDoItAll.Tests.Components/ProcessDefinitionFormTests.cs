using Bunit;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class ProcessDefinitionFormTests
{
    [Fact]
    public void Render_shows_all_lint_issues()
    {
        using var context = new TestContext();
        var issues = Enumerable.Range(1, 5)
            .Select(index => new ProcessDefinitionLintIssue(
                $"processes.lint.sb10.issue-{index}",
                ProcessDefinitionLintSeverity.Warning,
                $"Issue {index} message",
                Guid.NewGuid(),
                $"Step {index}",
                $"Suggestion {index}"))
            .ToList();
        var model = new ProcessDefinitionEditorModel
        {
            Name = "SB10 lint issue display process",
            LintResult = new ProcessDefinitionLintResult(issues, ProcessDefinitionLintMode.Strict)
        };

        var cut = context.RenderComponent<ProcessDefinitionForm>(
            ComponentParameter.CreateParameter(nameof(ProcessDefinitionForm.Model), model),
            ComponentParameter.CreateParameter(nameof(ProcessDefinitionForm.ScopeLabel), "Project"),
            ComponentParameter.CreateParameter(nameof(ProcessDefinitionForm.ShowActions), false));

        foreach (var issue in issues)
        {
            Assert.Contains(issue.Code, cut.Markup);
        }
    }
}
