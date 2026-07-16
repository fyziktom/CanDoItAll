using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentProjectStructureReadinessTests
{
    [Fact]
    public void Non_task_structure_write_satisfies_asset_creation_requirement()
    {
        var readiness = Evaluate(
            new AgentProjectStructureAccessSettings
            {
                CanWriteNonTaskStructure = true,
                AllowAllProjects = true
            },
            "project_structure_asset_create");

        Assert.DoesNotContain(
            readiness.Findings,
            finding => finding.Code == "agent.readiness.required-project-structure-write-missing");
    }

    [Fact]
    public void Task_write_does_not_satisfy_asset_creation_requirement()
    {
        var readiness = Evaluate(
            new AgentProjectStructureAccessSettings
            {
                CanWriteTasks = true,
                AllowAllProjects = true
            },
            "project_structure_asset_create");

        Assert.Contains(
            readiness.Findings,
            finding => finding.Code == "agent.readiness.required-project-structure-write-missing");
    }

    [Fact]
    public void Non_task_structure_write_does_not_satisfy_task_creation_requirement()
    {
        var readiness = Evaluate(
            new AgentProjectStructureAccessSettings
            {
                CanWriteNonTaskStructure = true,
                AllowAllProjects = true
            },
            "project_task_create");

        Assert.Contains(
            readiness.Findings,
            finding => finding.Code == "agent.readiness.required-project-task-write-missing");
    }

    [Fact]
    public void Task_write_satisfies_task_creation_requirement()
    {
        var readiness = Evaluate(
            new AgentProjectStructureAccessSettings
            {
                CanWriteTasks = true,
                AllowAllProjects = true
            },
            "project_task_create");

        Assert.DoesNotContain(
            readiness.Findings,
            finding => finding.Code == "agent.readiness.required-project-task-write-missing");
    }

    [Fact]
    public void Non_task_structure_write_does_not_satisfy_import_requirement()
    {
        var readiness = Evaluate(
            new AgentProjectStructureAccessSettings
            {
                CanWriteNonTaskStructure = true,
                AllowAllProjects = true
            },
            "project_structure_import");

        Assert.Contains(
            readiness.Findings,
            finding => finding.Code == "agent.readiness.required-project-structure-full-write-missing");
    }

    private static AgentProcessRoleReadinessResult Evaluate(
        AgentProjectStructureAccessSettings access,
        string requiredToolName)
    {
        var now = DateTimeOffset.UtcNow;
        var agent = new AgentDefinition(
            Guid.NewGuid(),
            "QA lead",
            "QA lead",
            "Validates project outputs.",
            "Validate project outputs.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "test-model",
            AgentWorkloadKind.Qa,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            AgentProjectStructureAccessMetadata.Write(null, access),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["qa-lead"],
            now,
            now);
        var request = new AgentProcessRoleReadinessRequest(
            "project-write",
            "QA lead",
            "qa-lead",
            "qa-lead",
            "QA lead",
            AllowedOperations: [],
            OperationTargetScope: string.Empty,
            RequiredRuntimeToolNames: [requiredToolName]);

        return AgentProcessReadinessEvaluator.Evaluate(agent, request);
    }
}
