using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAgentRuntimeToolProviderTests
{
    [Fact]
    public void ShouldAttachForContext_returns_false_for_plain_agent_chat()
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "chat-session",
            IsGovernedProcessStep = false,
            AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure]
        };

        Assert.False(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }

    [Fact]
    public void ShouldAttachForContext_returns_true_for_project_structure_chat()
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "project-structure"
        };

        Assert.True(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }

    [Theory]
    [InlineData(ProcessOperationContractNames.ReadProjectStructure)]
    [InlineData(ProcessOperationContractNames.StartProjectNodeProcess)]
    [InlineData(ProcessOperationContractNames.ExecuteExternalAction)]
    public void ShouldAttachForContext_returns_true_for_governed_project_structure_operations(string operation)
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "process-step",
            IsGovernedProcessStep = true,
            AllowedOperations = [operation]
        };

        Assert.True(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }

    [Fact]
    public void ShouldAttachForContext_returns_false_for_governed_non_project_structure_operations()
    {
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = "process-step",
            IsGovernedProcessStep = true,
            AllowedOperations = [ProcessOperationContractNames.ReadUpstreamArtifacts]
        };

        Assert.False(ProjectStructureAgentRuntimeToolProvider.ShouldAttachForContext(intent));
    }
}
