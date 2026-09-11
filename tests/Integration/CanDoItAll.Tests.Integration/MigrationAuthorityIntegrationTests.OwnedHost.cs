using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Persistence;

public sealed partial class MigrationAuthorityIntegrationTests {
    public const string OwnedHostProcessTemplateKey = "customer-onboarding";

    public static IReadOnlyDictionary<string, string?> OwnedHostConfiguration(TestDatabaseProfile profile) {
        ArgumentNullException.ThrowIfNull(profile);
        if (!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException("The owned H host fixture uses current-user DPAPI on Windows.");
        }
        return new Dictionary<string, string?> {
            ["SecretVault:AllowInsecureDevelopmentProviders"] = "false",
            ["DataProtection:KeyProtection:Provider"] = "Dpapi",
            ["ControlPlane:DataProtectionKeysPath"] = Path.Combine(profile.ProfileRootPath, "data-protection-keys"),
            ["ControlPlane:StateRootPath"] = Path.Combine(profile.EnvironmentRootPath, "control-plane", "state")
        };
    }

    public static async Task<OwnedHostRetainedSeed> SeedOwnedHostAsync(
        TestDatabaseProfile profile, string tag, string model, string processTemplateRoot,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(processTemplateRoot);
        if (profile.Provider != TestDatabaseProviderKind.PostgreSql || tag.Length > 48 ||
            tag.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')) {
            throw new ArgumentException("The owned H fixture requires PostgreSQL and a short ASCII tag.");
        }
        var templateLoader = new ProcessTemplatePackLoader(processTemplateRoot);
        var templateJson = JsonSerializer.Serialize(templateLoader.LoadDefinition(OwnedHostProcessTemplateKey), GraphJson);
        await using var provider = BuildOwnedHostFixtureServices(profile);
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var canonical = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        await using var context = await services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync(cancellationToken);
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync(cancellationToken));
        await context.Database.GetService<IMigrator>().MigrateAsync(StartingMigration, cancellationToken);
        await AssertOwnedHostStartingMigrationsAsync(context, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var graph = await SeedRetainedGraphAsync(services, context, profile);
        var records = await SeedRecordsAsync(context, graph);
        var graphRows = await ReadRetainedGraphRowsAsync(context, graph);
        Assert.Equal(13, graphRows.Length);
        var recordRows = await ReadRecordsAsync(context, records);
        Assert.Equal(8, recordRows.Length);

        Guid providerId;
        await using (var stage = await services.GetRequiredService<ProviderDefaultsBootstrapService>()
            .PrepareAsync(canonical, openAiSecretId: null, cancellationToken)) {
            providerId = stage.MatchedProviderId;
            await stage.CommitAsync(workspaceChanged: false, cancellationToken);
        }
        await services.GetRequiredService<IProviderRuntimeProfileSnapshotInitializer>().InitializeAsync(cancellationToken);
        await AssertOwnedHostStartingMigrationsAsync(context, cancellationToken);
        var organization = new FileSandboxWorkspaceStore(profile.WorkspaceRootPath,
            WorkspaceScopeDescriptor.Organization(canonical.Profile.Id.ToString("N")));
        var now = SavedAt;
        var permissions = new AgentPermissionsPolicy(false, false, true, false, false, true, false, []);
        var marker = $"H-{tag}";
        var agent = new AgentDefinition(Guid.NewGuid(), $"Saved H ordinary Agent {tag}", "Synthetic restart observer",
            "Synthetic saved definition for H restart proof.",
            $"Return {marker} followed by one short sentence about the supplied synthetic text. Do not call tools.",
            AgentLifecycleStatus.Active, providerId, model, AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged, 0, true, false,
            JsonSerializer.Serialize(new { retainedHostMarker = marker }, GraphJson), false, string.Empty,
            permissions, [], ["h-restart-synthetic"], now, now);
        var catalog = await organization.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Append(agent).ToArray()
        }, cancellationToken);
        agent = Assert.Single(catalog.Agents, item => item.Id == agent.Id);
        var agentJson = JsonSerializer.Serialize(agent);

        var component = await services.GetRequiredService<IWorkflowComponentLibraryService>().SaveComponentAsync(new(
            null, $"Saved H text component {tag}", providerId, model, WorkflowModality.Text,
            new(null, 256, false, string.Empty), agent.Instructions, WorkflowValueShape.Text,
            WorkflowValueShape.Text, permissions), cancellationToken);
        Assert.NotNull(component.PromptArtifactId);
        Assert.NotNull(component.PromptVersionId);
        var nodes = new[] {
            OwnedHostWorkflowNode("start", WorkflowNodeKind.Start, null, string.Empty),
            OwnedHostWorkflowNode("llm", WorkflowNodeKind.LlmCall, component.Id, component.Instructions),
            OwnedHostWorkflowNode("end", WorkflowNodeKind.End, null, string.Empty)
        };
        var definition = await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(
            null, null, $"Saved H text workflow {tag}", "Synthetic saved LLM workflow for actual restart proof.",
            WorkflowLifecycleStatus.Active, new(nodes[0].Id, nodes, [
                new(new("start-llm"), nodes[0].Id, null, nodes[1].Id, null, WorkflowEdgeKind.Direct, string.Empty),
                new(new("llm-end"), nodes[1].Id, null, nodes[2].Id, null, WorkflowEdgeKind.Direct, string.Empty)
            ]), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)), cancellationToken);
        var validation = await services.GetRequiredService<IWorkflowCatalogService>()
            .ValidateDefinitionAsync(definition, cancellationToken);
        Assert.Empty(validation.Issues);

        var eventStore = services.GetRequiredService<IProcessRuntimeEventStore>();
        await eventStore.AppendAsync([
            OwnedHostHistoricalEvent(graph, ProcessRuntimeEventTypes.ProcessRunCreated, SavedAt.AddSeconds(-1)),
            OwnedHostHistoricalEvent(graph, ProcessRuntimeEventTypes.ProcessRunCompleted, SavedAt)
        ], cancellationToken);
        var history = await ReadOwnedHostTimelineAsync(services, graph, cancellationToken);
        Assert.Equal(2, history.Length);
        var usabilityRows = await ReadOwnedHostUsabilityRowsAsync(context, component, definition, cancellationToken);
        Assert.Equal(5, usabilityRows.Length);
        await AssertOwnedHostStartingMigrationsAsync(context, cancellationToken);
        Assert.Equal(graphRows, await ReadRetainedGraphRowsAsync(context, graph));
        Assert.Equal(recordRows, await ReadRecordsAsync(context, records));
        var descriptor = new OwnedHostSeedDescriptor(tag, canonical.Profile.Id, profile.WorkspaceRootPath,
            StartingMigration, graph.ProjectId, graph.AgentId, graph.AgentRunId, graph.WorkflowId,
            graph.WorkflowVersionId, graph.WorkflowRunId, graph.WorkflowNodeId, graph.FileNodeId,
            graph.ProcessPlanId, graph.ProcessRunId, agent.Id, definition.Id.Value, definition.VersionId.Value,
            component.Id.Value, component.PromptArtifactId.Value, component.PromptVersionId.Value, providerId,
            OwnedHostProcessTemplateKey,
            ProcessDefinitionCatalogProjectionService.CreateDefinitionId(new(OwnedHostProcessTemplateKey)).Value,
            HashOwnedHostValue(templateJson), HashOwnedHostValue(string.Join('\n', graphRows)),
            HashOwnedHostValue(string.Join('\n', recordRows)),
            HashOwnedHostValue(agentJson), HashOwnedHostValue(string.Join('\n', usabilityRows)),
            HashOwnedHostValue(string.Join('\n', history)), OriginalGraphRowCount: 13,
            OriginalPluginAndSchedulerRowCount: 8, SyntheticProcessEventCount: 2);
        Guid? previousLifetime = null;
        return new OwnedHostRetainedSeed(descriptor, async token => {
            await using var restarted = BuildOwnedHostFixtureServices(profile);
            await using var readScope = restarted.CreateAsyncScope();
            var readServices = readScope.ServiceProvider;
            Assert.Equal(descriptor.DatabaseProfileId, readServices.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id);
            await using var reader = await readServices.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync(token);
            await AssertCurrentMigrationsAsync(reader);
            var observedGraph = await ReadRetainedGraphRowsAsync(reader, graph);
            foreach (var originalRow in graphRows) {
                Assert.Contains(originalRow, observedGraph);
            }
            Assert.Equal(recordRows, await ReadRecordsAsync(reader, records));
            previousLifetime = await AssertRetainedGraphAsync(restarted, profile, graph, previousLifetime);
            var savedAgent = Assert.Single((await organization.LoadCatalogAsync(token)).Agents, item => item.Id == agent.Id);
            Assert.Equal(agentJson, JsonSerializer.Serialize(savedAgent));
            Assert.Equal(usabilityRows, await ReadOwnedHostUsabilityRowsAsync(reader, component, definition, token));
            Assert.Equal(history, await ReadOwnedHostTimelineAsync(readServices, graph, token));
            var hydrated = await readServices.GetRequiredService<IWorkflowComponentLibraryService>().GetComponentAsync(component.Id, token);
            Assert.NotNull(hydrated);
            Assert.Equal(component.Id, hydrated.Id);
            Assert.Equal(component.PromptArtifactId, hydrated.PromptArtifactId);
            Assert.Equal(component.PromptVersionId, hydrated.PromptVersionId);
            Assert.Equal(component.Instructions, hydrated.Instructions);
            Assert.Equal(templateJson, JsonSerializer.Serialize(new ProcessTemplatePackLoader(processTemplateRoot)
                .LoadDefinition(OwnedHostProcessTemplateKey), GraphJson));
            return new OwnedHostSeedVerification(descriptor.DatabaseProfileId, previousLifetime.Value, 13,
                observedGraph.Length - graphRows.Length, recordRows.Length, usabilityRows.Length, history.Length,
                (await reader.Database.GetAppliedMigrationsAsync(token)).ToArray(), DateTimeOffset.UtcNow);
        });
    }

    private static ServiceProvider BuildOwnedHostFixtureServices(TestDatabaseProfile profile) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services,
            TestApplicationBootstrap.BuildConfiguration(profile, OwnedHostConfiguration(profile)),
            new TestHostEnvironment(profile.EnvironmentRootPath, ApplicationName));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static async Task AssertOwnedHostStartingMigrationsAsync(AppDbContext context, CancellationToken cancellationToken) {
        var migrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        Assert.NotEmpty(migrations);
        Assert.Equal(StartingMigration, migrations[^1]);
        Assert.Equal(context.Database.GetMigrations().Take(migrations.Length), migrations);
    }

    private static WorkflowNode OwnedHostWorkflowNode(
        string id, WorkflowNodeKind kind, WorkflowComponentId? componentId, string instructions) =>
        new(new(id), kind, id, [], new(componentId, null, null, null, instructions, WorkflowValueShape.Text, WorkflowValueShape.Text));

    private static ProcessRuntimeEventEnvelope OwnedHostHistoricalEvent(
        RetainedGraph graph, ProcessEventType eventType, DateTimeOffset occurredAtUtc) =>
        new(RuntimeEventId.New(), new(graph.ProcessRunId), new(graph.ProcessRunId),
            new ProcessCorrelationId($"synthetic-h-history:{graph.ProcessRunId:N}"), null,
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId("synthetic-h-history")),
            ProcessContractVersions.RuntimeEventEnvelopeV1, ProcessEventSensitivity.Normal, occurredAtUtc,
            eventType, $"sha256:{HashOwnedHostValue(graph.ProcessPlanJson)}");

    private static async Task<string[]> ReadOwnedHostTimelineAsync(
        IServiceProvider services, RetainedGraph graph, CancellationToken cancellationToken) {
        var events = await services.GetRequiredService<IProcessRuntimeEventReplayStore>()
            .ReadByRootRunAsync(new(graph.ProcessRunId), 0, 10, cancellationToken);
        return events.Select(item => JsonSerializer.Serialize(item, GraphJson)).ToArray();
    }

    private static async Task<string[]> ReadOwnedHostUsabilityRowsAsync(
        AppDbContext context, LlmCallComponent component, WorkflowDefinition definition, CancellationToken cancellationToken) =>
        await context.Database.SqlQuery<string>($"""
            SELECT 'component:' || to_jsonb(row)::text AS "Value"
                FROM "AgentFramework_WorkflowComponents" row WHERE "Id" = {component.Id.Value}
            UNION ALL SELECT 'prompt:' || to_jsonb(row)::text
                FROM "Prompts_PromptArtifacts" row WHERE "Id" = {component.PromptArtifactId}
            UNION ALL SELECT 'prompt-version:' || to_jsonb(row)::text
                FROM "Prompts_PromptVersions" row WHERE "Id" = {component.PromptVersionId}
            UNION ALL SELECT 'definition:' || to_jsonb(row)::text
                FROM "AgentFramework_WorkflowDefinitions" row WHERE "VersionId" = {definition.VersionId.Value}
            UNION ALL SELECT 'head:' || to_jsonb(row)::text
                FROM "AgentFramework_WorkflowDefinitionHeads" row WHERE "WorkflowId" = {definition.Id.Value}
            """).OrderBy(value => value).ToArrayAsync(cancellationToken);

    private static string HashOwnedHostValue(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public sealed record OwnedHostSeedDescriptor(
        string Tag, Guid DatabaseProfileId, string WorkspaceRoot, string StartingMigrationId,
        Guid OriginalProjectId, Guid OriginalProjectAgentId, Guid OriginalAgentRunId,
        Guid OriginalWorkflowId, Guid OriginalWorkflowVersionId, Guid OriginalWorkflowRunId,
        Guid OriginalWorkflowNodeId, Guid OriginalFileNodeId, Guid OriginalProcessPlanId, Guid OriginalProcessRunId,
        Guid OrdinaryAgentId, Guid ExecutableWorkflowId, Guid ExecutableWorkflowVersionId,
        Guid ComponentId, Guid PromptArtifactId, Guid PromptVersionId, Guid ProviderId,
        string ProcessTemplateKey, Guid ProcessDefinitionId, string ProcessTemplateHash,
        string OriginalGraphHash, string OriginalPluginAndSchedulerHash, string OrdinaryAgentHash,
        string WorkflowAndPromptHash, string SyntheticTimelineHash,
        int OriginalGraphRowCount, int OriginalPluginAndSchedulerRowCount, int SyntheticProcessEventCount);

    public sealed record OwnedHostSeedVerification(
        Guid DatabaseProfileId, Guid OriginalProjectLifetimeId, int PreservedOriginalGraphRows,
        int AdditionalRelatedGraphRows, int PreservedPluginAndSchedulerRows,
        int PreservedWorkflowAndPromptRows, int PreservedSyntheticProcessEvents,
        string[] AppliedMigrations, DateTimeOffset VerifiedAtUtc);

    public sealed class OwnedHostRetainedSeed(
        OwnedHostSeedDescriptor descriptor, Func<CancellationToken, Task<OwnedHostSeedVerification>> verify) {
        private int verifying;
        public OwnedHostSeedDescriptor Descriptor { get; } = descriptor;

        public async Task<OwnedHostSeedVerification> VerifyAsync(CancellationToken cancellationToken = default) {
            if (Interlocked.CompareExchange(ref verifying, 1, 0) != 0) {
                throw new InvalidOperationException("Owned H readback is already running.");
            }
            try {
                return await verify(cancellationToken);
            } finally {
                Volatile.Write(ref verifying, 0);
            }
        }
    }
}
