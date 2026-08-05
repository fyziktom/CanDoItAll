using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;

namespace CanDoItAll.Tests.Components;

public sealed class HrAgentToolPolicyTests
{
    [Theory]
    [InlineData(AgentToolInvocationPolicyMetadata.HrAgentCreate)]
    [InlineData(AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate)]
    [InlineData(AgentToolInvocationPolicyMetadata.HrAgentAvatarGenerate)]
    [InlineData(AgentToolInvocationPolicyMetadata.HrAgentProcessManagerReviewRequest)]
    [InlineData(AgentToolInvocationPolicyMetadata.HrCrmPartyCreate)]
    [InlineData(AgentToolInvocationPolicyMetadata.HrCrmAffiliationUpsert)]
    public void HR_mutations_require_host_approval(string toolName)
    {
        Assert.Equal(ToolInvocationClassification.Mutation, AgentToolInvocationPolicyMetadata.Classify(toolName));
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(toolName));
        Assert.True(AgentToolInvocationPolicyMetadata.IsMutationTool(toolName));
    }

    [Fact]
    public void HR_argument_redaction_masks_business_text_and_preserves_target_identity()
    {
        var targetAgentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate,
        [
            new KeyValuePair<string, object?>("request", new
            {
                agentId = targetAgentId,
                name = "Private agent name",
                instructions = "Confidential operating instructions"
            })
        ]);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate,
            redacted);

        Assert.Contains(targetAgentId.ToString("D"), signature, StringComparison.Ordinal);
        Assert.DoesNotContain("Private agent name", signature, StringComparison.Ordinal);
        Assert.DoesNotContain("Confidential operating instructions", signature, StringComparison.Ordinal);
    }

    [Fact]
    public void HR_CRM_search_redaction_masks_the_search_text()
    {
        const string privateSearchText = "private.person@example.test";
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.HrCrmSearch,
        [
            new KeyValuePair<string, object?>("request", new
            {
                searchText = privateSearchText,
                take = 20
            })
        ]);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.HrCrmSearch,
            redacted);

        Assert.DoesNotContain(privateSearchText, signature, StringComparison.Ordinal);
        Assert.Contains("take", signature, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AgentToolInvocationPolicyMetadata.HrCrmPartyCreate)]
    [InlineData(AgentToolInvocationPolicyMetadata.HrCrmAffiliationUpsert)]
    public void HR_CRM_mutation_redaction_masks_party_business_data(
        string toolName)
    {
        const string privateDisplayName = "Private CRM person";
        const string privateTitle = "Confidential engagement lead";
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
            toolName,
        [
            new KeyValuePair<string, object?>("request", new
            {
                personPartyId =
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                organizationPartyId =
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                displayName = privateDisplayName,
                jobTitle = privateTitle
            })
        ]);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            toolName,
            redacted);

        Assert.DoesNotContain(
            privateDisplayName,
            signature,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            privateTitle,
            signature,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "11111111-1111-1111-1111-111111111111",
            signature,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HR_CRM_affiliation_list_redaction_masks_person_identity()
    {
        const string personPartyId =
            "11111111-1111-1111-1111-111111111111";
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.HrCrmPartyAffiliationsList,
        [
            new KeyValuePair<string, object?>("request", new
            {
                personPartyId
            })
        ]);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.HrCrmPartyAffiliationsList,
            redacted);

        Assert.DoesNotContain(
            personPartyId,
            signature,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HR_pending_approval_keeps_raw_arguments_until_the_decision_boundary()
    {
        const string privateInstructions = "Only promote agents approved by the confidential committee.";
        var pendingApproval = CreatePendingApproval(
            "approval-create",
            AgentToolInvocationPolicyMetadata.HrAgentCreate,
            CreateRequestArguments(new { instructions = privateInstructions }));
        var run = CreateRun([pendingApproval]);

        var synchronized = ExecutionRunStateTransitions.SynchronizePendingApprovals(
            [],
            run,
            run.PendingApprovals,
            run.UpdatedAtUtc);

        Assert.Contains(privateInstructions, run.PendingApprovals[0].ArgumentsJson, StringComparison.Ordinal);
        Assert.Contains(privateInstructions, synchronized.Pending[0].ArgumentsJson, StringComparison.Ordinal);
        Assert.Equal(ExecutionApprovalStatus.Pending, synchronized.Pending[0].Status);
    }

    [Fact]
    public void HR_decision_replaces_private_arguments_with_deterministic_audit_data()
    {
        const string privateInstructions = "Only promote agents approved by the confidential committee.";
        const string privateQuestion = "Did the agent expose Project Nightfall compensation data?";
        const string privateAvatarBrief = "Portrait based on the unreleased reorganization campaign.";
        var pendingApprovals = new[]
        {
            CreatePendingApproval(
                "approval-create",
                AgentToolInvocationPolicyMetadata.HrAgentCreate,
                CreateRequestArguments(new
                {
                    agentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    instructions = privateInstructions
                })),
            CreatePendingApproval(
                "approval-review",
                AgentToolInvocationPolicyMetadata.HrAgentProcessManagerReviewRequest,
                CreateRequestArguments(new
                {
                    processRunId = "run-42",
                    question = privateQuestion
                })),
            CreatePendingApproval(
                "approval-avatar",
                AgentToolInvocationPolicyMetadata.HrAgentAvatarGenerate,
                CreateRequestArguments(new
                {
                    agentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    visualBrief = privateAvatarBrief
                }))
        };
        var run = CreateRun(pendingApprovals);

        var decision = ExecutionRunStateTransitions.ApplyApprovalDecision(
            [],
            run,
            approved: true,
            DateTimeOffset.Parse("2026-07-14T16:00:00Z"),
            "chat-session",
            "session-42");

        Assert.All(decision.Decided, approval =>
        {
            Assert.Equal(ExecutionApprovalStatus.Approved, approval.Status);
            Assert.Contains("hr-approval-redacted-v1", approval.ArgumentsJson, StringComparison.Ordinal);
            Assert.DoesNotContain(privateInstructions, approval.ArgumentsJson, StringComparison.Ordinal);
            Assert.DoesNotContain(privateQuestion, approval.ArgumentsJson, StringComparison.Ordinal);
            Assert.DoesNotContain(privateAvatarBrief, approval.ArgumentsJson, StringComparison.Ordinal);

            using var audit = JsonDocument.Parse(approval.ArgumentsJson);
            Assert.Equal(64, audit.RootElement.GetProperty("argumentsSha256").GetString()!.Length);
        });

        var firstAudit = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
            pendingApprovals[0].ToolName,
            pendingApprovals[0].ArgumentsJson);
        var repeatedAudit = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
            pendingApprovals[0].ToolName,
            pendingApprovals[0].ArgumentsJson);
        Assert.Equal(firstAudit, repeatedAudit);

        using var firstAuditDocument = JsonDocument.Parse(firstAudit);
        const string expectedCanonicalArgumentsSha256 =
            "46396d165c033823eecfd2164365f35966a70b6cb95775433123689112418bc5";
        Assert.Equal(
            expectedCanonicalArgumentsSha256,
            firstAuditDocument.RootElement.GetProperty("argumentsSha256").GetString());
        var protectedRequest = firstAuditDocument.RootElement
            .GetProperty("arguments")
            .GetProperty("request");
        Assert.Equal(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            protectedRequest.GetProperty("agentId").GetGuid());
        Assert.StartsWith(
            "<redacted>#",
            protectedRequest.GetProperty("instructions").GetString(),
            StringComparison.Ordinal);

        var reprotectedAudit = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
            pendingApprovals[0].ToolName,
            firstAudit);
        using var reprotectedAuditDocument = JsonDocument.Parse(reprotectedAudit);
        Assert.NotEqual(
            expectedCanonicalArgumentsSha256,
            reprotectedAuditDocument.RootElement.GetProperty("argumentsSha256").GetString());
        Assert.Equal(
            firstAudit,
            AgentToolInvocationPolicyMetadata.ProtectPreviouslyProtectedApprovalArgumentsForExport(
                pendingApprovals[0].ToolName,
                firstAudit));
    }

    [Fact]
    public async Task Agent_package_export_removes_private_HR_approval_arguments()
    {
        const string privateInstructions = "Agent instructions for Project Juniper.";
        const string privateQuestion = "Was the failed compensation analysis repeated?";
        const string privateAvatarBrief = "Use the confidential acquisition visual identity.";
        const string privateReviewPrompt = "Review whether the manager mishandled the confidential pay-equity investigation.";
        const string privateReviewResponse = "The manager repeated analysis involving named employees.";
        const string privateReviewLog = "Provider persisted the confidential manager-review response.";
        const string privateManagerApprovalDetails = "Tool request derived from confidential manager evidence.";
        const string privateManagerApprovalArguments = "{\"employee\":\"private-manager-approval-secret\"}";
        var pendingApprovals = new[]
        {
            CreatePendingApproval(
                "approval-create",
                AgentToolInvocationPolicyMetadata.HrAgentCreate,
                CreateRequestArguments(new { instructions = privateInstructions })),
            CreatePendingApproval(
                "approval-review",
                AgentToolInvocationPolicyMetadata.HrAgentProcessManagerReviewRequest,
                CreateRequestArguments(new { question = privateQuestion })),
            CreatePendingApproval(
                "approval-avatar",
                AgentToolInvocationPolicyMetadata.HrAgentAvatarGenerate,
                CreateRequestArguments(new { visualBrief = privateAvatarBrief }))
        };
        var run = CreateRun(pendingApprovals) with
        {
            SerializedSessionStateJson = JsonSerializer.Serialize(new
            {
                instructions = privateInstructions,
                question = privateQuestion,
                visualBrief = privateAvatarBrief
            })
        };
        var managerReviewPendingApproval = CreatePendingApproval(
            "manager-review-pending",
            "unexpected_manager_tool",
            privateManagerApprovalArguments) with
        {
            Details = privateManagerApprovalDetails
        };
        var managerReviewRun = CreateRun([managerReviewPendingApproval]) with
        {
            Id = Guid.NewGuid(),
            SourceKind = HrAgentExecutionSourceKinds.ManagerReview,
            InputSummary = privateReviewPrompt,
            ResultSummary = privateReviewResponse,
            RuntimeSessionKey = "manager-review-runtime-secret",
            SerializedSessionStateJson = JsonSerializer.Serialize(new
            {
                prompt = privateReviewPrompt,
                response = privateReviewResponse
            }),
            State = ExecutionState.Completed,
            Outcome = RunOutcome.Succeeded,
            CompletedAtUtc = DateTimeOffset.Parse("2026-07-14T16:00:00Z")
        };
        var managerReviewDecidedApproval = new ExecutionApprovalRecord(
            ApprovalId: "manager-review-decided",
            ExecutionRunId: managerReviewRun.Id,
            CallId: "manager-review-call",
            ToolName: "unexpected_manager_tool",
            ToolKind: "function",
            Details: privateManagerApprovalDetails,
            ArgumentsJson: privateManagerApprovalArguments,
            Status: ExecutionApprovalStatus.Rejected,
            RequestedAtUtc: managerReviewRun.CreatedAtUtc,
            DecidedAtUtc: managerReviewRun.UpdatedAtUtc,
            DecisionSourceKind: "manager-review-policy",
            DecisionSourceId: managerReviewRun.Id.ToString("D"),
            DecisionNotes: "Rejected because manager-review tools are disabled.");
        var legacyDecidedApproval = new ExecutionApprovalRecord(
            ApprovalId: pendingApprovals[0].ApprovalId,
            ExecutionRunId: run.Id,
            CallId: pendingApprovals[0].CallId,
            ToolName: pendingApprovals[0].ToolName,
            ToolKind: pendingApprovals[0].ToolKind,
            Details: pendingApprovals[0].Details,
            ArgumentsJson: pendingApprovals[0].ArgumentsJson,
            Status: ExecutionApprovalStatus.Approved,
            RequestedAtUtc: run.CreatedAtUtc,
            DecidedAtUtc: run.UpdatedAtUtc,
            DecisionSourceKind: "chat-session",
            DecisionSourceId: "session-42",
            DecisionNotes: "Approved through execution continuation.");
        var agent = CreateAgent(run.AgentId);
        var legacySession = new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            Title: "Legacy pending review",
            CreatedAtUtc: run.CreatedAtUtc,
            UpdatedAtUtc: run.UpdatedAtUtc,
            Messages: [],
            Compatibility: new ChatSessionRuntimeCompatibilityRecord(
                runtimeSessionKey: "legacy-runtime",
                serializedSessionStateJson: JsonSerializer.Serialize(new
                {
                    question = privateQuestion,
                    visualBrief = privateAvatarBrief
                }),
                pendingApprovals: [pendingApprovals[1], pendingApprovals[2]]));
        var document = SandboxWorkspaceDocument.Empty with
        {
            Agents = [agent],
            ChatSessions = [legacySession],
            ExecutionRuns = [run, managerReviewRun],
            ExecutionApprovals = [legacyDecidedApproval, managerReviewDecidedApproval],
            ExecutionLog =
            [
                new ExecutionLogEntry(
                    Id: Guid.NewGuid(),
                    AgentId: agent.Id,
                    ChatSessionId: null,
                    CreatedAtUtc: managerReviewRun.UpdatedAtUtc,
                    State: ExecutionState.Completed,
                    Phase: "Completed",
                    Message: privateReviewLog)
                {
                    ExecutionRunId = managerReviewRun.Id
                }
            ]
        };
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"hr-approval-export-{Guid.NewGuid():N}");

        try
        {
            var packageService = new ZipAgentPackageService(workspaceRoot);
            var export = await packageService.ExportAsync(document, agent);
            using var archive = ZipFile.OpenRead(export.PackagePath);
            var exportedText = new StringBuilder();
            foreach (var entry in archive.Entries.Where(entry => entry.Length > 0))
            {
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                exportedText.Append(await reader.ReadToEndAsync());
            }

            var packageText = exportedText.ToString();
            Assert.DoesNotContain(privateInstructions, packageText, StringComparison.Ordinal);
            Assert.DoesNotContain(privateQuestion, packageText, StringComparison.Ordinal);
            Assert.DoesNotContain(privateAvatarBrief, packageText, StringComparison.Ordinal);
            Assert.DoesNotContain(privateReviewPrompt, packageText, StringComparison.Ordinal);
            Assert.DoesNotContain(privateReviewResponse, packageText, StringComparison.Ordinal);
            Assert.DoesNotContain(privateReviewLog, packageText, StringComparison.Ordinal);
            Assert.DoesNotContain(privateManagerApprovalDetails, packageText, StringComparison.Ordinal);
            Assert.DoesNotContain("private-manager-approval-secret", packageText, StringComparison.Ordinal);
            Assert.DoesNotContain("manager-review-runtime-secret", packageText, StringComparison.Ordinal);
            Assert.Contains("hr-approval-redacted-v1", packageText, StringComparison.Ordinal);
            Assert.Contains("hr-manager-review-redacted-v1", packageText, StringComparison.Ordinal);
            Assert.Contains("HR manager-review request redacted for export.", packageText, StringComparison.Ordinal);
            Assert.Contains("HR manager-review response redacted for export.", packageText, StringComparison.Ordinal);
            Assert.Contains("HR manager-review execution log redacted for export.", packageText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Non_HR_approval_arguments_are_not_changed()
    {
        const string argumentsJson = "{\"path\":\"artifacts/result.md\"}";

        var protectedArguments = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
            "workspace_write_text",
            argumentsJson);

        Assert.Equal(argumentsJson, protectedArguments);
    }

    private static PendingToolApprovalRecord CreatePendingApproval(
        string approvalId,
        string toolName,
        string argumentsJson)
    {
        return new PendingToolApprovalRecord(
            ApprovalId: approvalId,
            CallId: $"call-{approvalId}",
            ToolName: toolName,
            ToolKind: "function",
            Details: string.Empty,
            ArgumentsJson: argumentsJson);
    }

    private static string CreateRequestArguments(object request)
    {
        return JsonSerializer.Serialize(new { request });
    }

    private static ExecutionRunRecord CreateRun(IReadOnlyList<PendingToolApprovalRecord> pendingApprovals)
    {
        var now = DateTimeOffset.Parse("2026-07-14T15:00:00Z");
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: HrAgentIdentity.AgentId,
            ChatSessionId: null,
            Title: "HR approval test",
            SourceKind: "manual",
            SourceId: string.Empty,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: string.Empty,
            RequestedBy: "test",
            RequestedByKind: "test",
            MetadataJson: "{}",
            InputSummary: "Test HR approval retention.",
            ResultSummary: "Awaiting approval.",
            ProviderName: "Test provider",
            Model: "test-model",
            State: ExecutionState.WaitingOnTool,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: "runtime-session",
            SerializedSessionStateJson: "{}",
            PendingApprovals: pendingApprovals);
    }

    private static AgentDefinition CreateAgent(Guid agentId)
    {
        var now = DateTimeOffset.Parse("2026-07-14T15:00:00Z");
        return new AgentDefinition(
            Id: agentId,
            Name: "HR Agent",
            RoleTitle: "Agent governance",
            Summary: "Manages technical agents.",
            Instructions: "Use approval-gated tools.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "test-model",
            Workload: AgentWorkloadKind.Hr,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.1,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: true,
            TemplateKey: HrAgentIdentity.TemplateKey,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["hr"],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}
