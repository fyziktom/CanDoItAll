using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.SharedProviders.E2E;

internal static class E2eConsumerEvidence {
    public const string Command = "read-consumer-agent";
    private static readonly HashSet<string> FileTools = [
        ToolContractCatalog.WorkspaceWriteFile,
        ProjectStructureToolPolicy.ProjectStructureAssetCreate,
        ProjectStructureToolPolicy.ProjectStructureAssetGet,
        ProjectStructureToolPolicy.ProjectStructureAssetContentGet,
        ImageGenerationToolPolicy.ImageGenerationCreate
    ];

    public static async Task ReadAsync(string[] args, CancellationToken cancellationToken) {
        if (args.Length != 4 || !Guid.TryParse(args[1], out var agentId) || agentId == Guid.Empty || args[2] != "--role") {
            throw new E2eSafeException("Consumer evidence requires an exact fixture agent and role.");
        }
        var invocation = E2eCommandLine.Parse(["snapshot", "--role", args[3]]);
        await using var host = await E2eServiceHost.CreateAsync(invocation.Options, cancellationToken);
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = await workspace.GetAgentEditorAsync(agentId, cancellationToken);
        if (agent.Id != agentId || !agent.Name.StartsWith("PP2C file agent ", StringComparison.Ordinal)) {
            throw new E2eSafeException("Only the owned PP2 completion file agent may be inspected.");
        }
        var latest = (await workspace.ListExecutionRunsAsync(new(AgentId: agentId), cancellationToken))
            .OrderByDescending(run => run.CreatedAtUtc).FirstOrDefault();
        if (latest is null) {
            Console.WriteLine("null");
            return;
        }
        var detail = await workspace.GetExecutionRunDetailAsync(latest.Id, cancellationToken);
        var admission = detail.Run.ToolAdmission;
        var proposals = admission?.Batches.SelectMany(batch => batch.Proposals).ToArray() ?? [];
        if (proposals.Any(proposal => !FileTools.Contains(proposal.Payload.ToolName))) {
            throw new E2eSafeException("The file fixture proposed an unexpected tool; consumer evidence was refused.");
        }
        var attachments = admission?.RuntimeContext is { } saved
            ? new AgentChatContextAttachmentPersistence(services.GetServices<IAgentChatContextAttachmentCodec>()).Restore(saved).Attachments
            : [];
        var snapshots = attachments.Select(attachment => attachment.TryGetAttachment<ProjectStructureInvocationSnapshot>(out var snapshot) ? snapshot : null)
            .OfType<ProjectStructureInvocationSnapshot>().Select(snapshot => new { snapshot.ProjectId, snapshot.SelectedNodeIds }).ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new {
            AgentId = agentId,
            RunId = detail.Run.Id,
            detail.Run.State,
            detail.Run.SourceKind,
            detail.Run.SourceId,
            Session = admission?.Session,
            Snapshots = snapshots,
            Proposals = proposals.Select(proposal => new {
                proposal.IntentId, proposal.ApprovalId, proposal.State, proposal.ApprovalStatus,
                proposal.EffectState, proposal.Payload
            }),
            Receipts = detail.ToolReceipts.Select(receipt => new {
                receipt.Id, receipt.ToolName, receipt.InvocationOutcome, receipt.EffectState,
                receipt.EffectSourceKind, receipt.EffectSourceId
            })
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
