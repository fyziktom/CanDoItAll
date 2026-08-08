using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentRuntimePortContractTests
{
    private static readonly Func<ExecutionState, string, string, Task> ProgressCallback =
        static (_, _, _) => Task.CompletedTask;

    [Fact]
    public void Continuation_request_requires_at_least_one_decision()
    {
        var agent = CreateAgent();

        Assert.Throws<ArgumentException>(() => new AgentRuntimeContinuationRequest(
            agent,
            CreateProvider(),
            CreateSession(agent.Id),
            Capabilities: [],
            Memory: [],
            Decisions: [],
            RuntimeSessionKey: null,
            ProgressCallback));
    }

    [Fact]
    public void Approval_decision_requires_a_non_blank_proposal_id()
    {
        Assert.Throws<ArgumentException>(() => new AgentRuntimeApprovalDecision(" ", true));
        Assert.Throws<ArgumentNullException>(() => new AgentRuntimeApprovalDecision(null!, true));
        Assert.Throws<ArgumentException>(
            () => new AgentRuntimeApprovalDecision("proposal-1", true) with { ProposalId = "" });
    }

    [Fact]
    public void Runtime_abstractions_sources_are_sdk_and_product_free()
    {
        var root = FindRepoRoot();
        var projectRoot = Path.Combine(
            root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Runtime.Abstractions");
        Assert.True(Directory.Exists(projectRoot), $"Missing project directory: {projectRoot}");

        var sourceFiles = Directory.GetFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(sourceFiles);

        string[] forbiddenTokens =
        [
            "Microsoft.Agents.AI",
            "Microsoft.Extensions.AI",
            "OpenAI.",
            "Azure.AI",
            "CanDoItAll.Modules.",
            "IServiceProvider",
            "CanDoItAll.AgentFramework.Maf"
        ];

        foreach (var sourceFile in sourceFiles)
        {
            var text = File.ReadAllText(sourceFile);
            foreach (var forbiddenToken in forbiddenTokens)
            {
                Assert.False(
                    text.Contains(forbiddenToken, StringComparison.Ordinal),
                    $"Runtime.Abstractions source '{Path.GetFileName(sourceFile)}' must not reference '{forbiddenToken}'.");
            }
        }

        var projectText = File.ReadAllText(Path.Combine(
            projectRoot, "CanDoItAll.AgentFramework.Runtime.Abstractions.csproj"));
        Assert.DoesNotContain("PackageReference", projectText, StringComparison.Ordinal);
        Assert.Equal(1, projectText.Split("<ProjectReference", StringSplitOptions.None).Length - 1);
        Assert.Contains("CanDoItAll.AgentFramework.Models.csproj", projectText, StringComparison.Ordinal);
    }

    [Fact]
    public void Core_execution_coordination_call_sites_use_the_narrow_ports()
    {
        var root = FindRepoRoot();
        var executionRuns = File.ReadAllText(Path.Combine(
            root,
            "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core",
            "Execution", "AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs"));
        var chat = File.ReadAllText(Path.Combine(
            root,
            "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core",
            "Execution", "AgentFrameworkWorkspaceExecutionService.Chat.cs"));

        Assert.DoesNotContain("runtime.RunAsync(", executionRuns, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime.RespondToPendingApprovalsAsync(", executionRuns, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime.RunAsync(", chat, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime.RespondToPendingApprovalsAsync(", chat, StringComparison.Ordinal);

        Assert.Contains("executionRuntime.ExecuteAsync(", executionRuns, StringComparison.Ordinal);
        Assert.Contains("continuationRuntime.ContinueAsync(", executionRuns, StringComparison.Ordinal);
        Assert.Contains("continuationRuntime.ContinueAsync(", chat, StringComparison.Ordinal);
    }

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Port Contract Agent",
            "Contract Reviewer",
            "Validates runtime port delegation.",
            "Delegate faithfully.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "contract-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.1,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Port Contract Provider",
            ProviderKind.OpenAi,
            BaseUrl: "https://provider.example",
            ApiKeyEnvironmentVariable: "PORT_CONTRACT_KEY",
            DefaultModel: "contract-model",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private static ChatSessionRecord CreateSession(Guid agentId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ChatSessionRecord(
            Guid.NewGuid(),
            agentId,
            "Port contract session",
            now,
            now,
            Messages: []);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
