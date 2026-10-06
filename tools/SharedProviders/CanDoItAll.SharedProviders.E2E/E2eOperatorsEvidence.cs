using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Security.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.SharedProviders.E2E;

internal static class E2eOperatorsEvidence {
    public const string Command = "read-wb5-operators";

    public static async Task ReadAsync(string[] args, CancellationToken cancellationToken) {
        if (args.Length is not (4 or 6) || !Guid.TryParse(args[1], out var projectId) || projectId == Guid.Empty || args[2] != "--role" ||
            args.Length == 6 && (args[4] != "--agent" || !Guid.TryParse(args[5], out _))) {
            throw new E2eSafeException("Operator evidence requires one exact owned project and fixture role.");
        }
        var invocation = E2eCommandLine.Parse(["snapshot", "--role", args[3]]);
        await using var host = await E2eServiceHost.CreateAsync(invocation.Options, cancellationToken);
        await using var scope = host.Services.CreateAsyncScope();
        var project = await scope.ServiceProvider.GetRequiredService<ProjectsService>().GetAsync(projectId, cancellationToken);
        const string prefix = "PP2C files ";
        if (project.Id != projectId || !project.Name.StartsWith(prefix, StringComparison.Ordinal)) {
            throw new E2eSafeException("Only the owned operator/file project may be inspected.");
        }
        var marker = project.Name[prefix.Length..];
        if (!Regex.IsMatch(marker, "^PP2C_FILES_[a-f0-9]{32}$", RegexOptions.CultureInvariant)) {
            throw new E2eSafeException("The owned file marker was not exact.");
        }
        var assignments = await scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>()
            .ListAssignmentsDetailedAsync(projectId, cancellationToken);
        var denied = Path.Combine(invocation.Options.InstanceRootPath, "workspace", "pp2c", marker, "denied.md");
        var secretResults = new List<object>();
        if (args.Length == 6) {
            var agentId = Guid.Parse(args[5]);
            var agent = await scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentEditorAsync(agentId, cancellationToken);
            if (agent.Name != "PP2C file agent " + marker || agent.ProjectStructureAccess.AllowAllProjects ||
                !agent.ProjectStructureAccess.AllowedProjectIds.SequenceEqual([projectId])) {
                throw new E2eSafeException("Secret observation requires the exact owned single-project Agent.");
            }
            var structure = await scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId, cancellationToken);
            var ids = structure.Nodes.Select(node => ProjectObjectMetadataSerializer.Parse(node.MetadataJson).SecretReference?.SecretId)
                .OfType<Guid>().Distinct().ToArray();
            var allowed = agent.AllowedSecretReferences.Select(reference => reference.SecretId).ToHashSet();
            var resolver = scope.ServiceProvider.GetRequiredService<ISecretRuntimeResolver>();
            foreach (var id in ids) {
                try {
                    var value = await resolver.ResolveValueAsync(new(id, SecretRuntimePurposes.AgentMcpHeader, allowed,
                        SecretRuntimeConsumerTypes.AgentMcp, SecretRuntimeConsumerIds.AgentMcp(agentId, "wb5-owned-observation", "header")), cancellationToken);
                    secretResults.Add(new { SecretId = id, Outcome = "resolved", Sha256 = value is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))) });
                } catch (InvalidOperationException) when (!allowed.Contains(id)) {
                    secretResults.Add(new { SecretId = id, Outcome = "denied", Sha256 = (string?)null });
                }
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new {
            ProjectId = projectId, Assignments = assignments, DeniedFileExists = File.Exists(denied), Secrets = secretResults
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
