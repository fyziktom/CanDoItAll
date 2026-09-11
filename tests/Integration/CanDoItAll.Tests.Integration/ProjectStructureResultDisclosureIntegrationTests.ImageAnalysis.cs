using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Runtime;

public sealed partial class ProjectStructureResultDisclosureIntegrationTests {
    [Fact]
    public async Task Metered_image_analysis_lost_acknowledgement_cannot_dispatch_again_after_file_store_restart() {
        var analysis = new ImageAnalysisCounter { FailAfterDispatch = true };
        await using var application = await CreateImageApplicationAsync(analysis);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Metered image analysis");
        var image = await CreateImageAsync(services, project);
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var agent = await SaveImageActorAsync(services, fixture.Agent, project, canRead: true, canTransform: true);
        const string toolName = ProjectStructureToolPolicy.ProjectStructureAssetImageAnalyze;
        var arguments = ImageArguments(project.ProjectId, image.Id);
        var original = new ScriptClient(toolName, arguments);

        var dispatchFailure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, original));
        var injected = Assert.IsType<IOException>(analysis.InjectedDispatchFailure);
        Assert.Equal("Injected acknowledgement loss after the metered analysis was dispatched.", injected.Message);
        Assert.Contains("tool-admission.reconciliation-required", Codes(dispatchFailure));
        Assert.Single(analysis.Requests);
        Assert.Equal(1, original.Requests);
        var saved = await ReadProposalAsync(fixture);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, saved.State);
        Assert.Equal(AgentToolProposalEffect.Read, saved.Payload.Effect);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, saved.Payload.Recovery);
        Assert.Null(saved.Result);
        Assert.Null(saved.DisclosureEvidence);
        AssertOriginalGenericArguments(saved.Payload, arguments);

        analysis.FailAfterDispatch = false;
        var restarted = new ScriptClient(toolName, arguments);
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, restarted));
        Assert.Contains("tool-admission.reconciliation-required", Codes(failure));
        Assert.Equal(0, restarted.Requests);
        Assert.Single(analysis.Requests);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acknowledged_image_analysis_retains_its_result_and_rechecks_current_read_and_transform_access(bool revokeTransform) {
        var analysis = new ImageAnalysisCounter();
        await using var application = await CreateImageApplicationAsync(analysis);
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services, "Acknowledged image analysis");
        var image = await CreateImageAsync(services, project);
        await using var fixture = await CreateJournalAsync(services, project.ProjectId);
        var agent = await SaveImageActorAsync(services, fixture.Agent, project, canRead: true, canTransform: true);
        const string toolName = ProjectStructureToolPolicy.ProjectStructureAssetImageAnalyze;
        var arguments = ImageArguments(project.ProjectId, image.Id);
        var original = new ScriptClient(toolName, arguments, failAfterResult: true);
        var checkpointFailure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, original));
        AgentToolAdmissionJournalFixture.RequireScriptedFault(checkpointFailure,
            "Injected final provider acknowledgement loss after the saved tool result.");
        Assert.Equal(2, original.Requests);
        var request = Assert.Single(analysis.Requests);
        Assert.Equal("image/png", Assert.Single(request.Sources).ContentType);
        Assert.Equal(Convert.FromBase64String(ImagePng), Assert.Single(request.Sources).Bytes);
        var saved = await ReadProposalAsync(fixture);
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(AgentToolProposalEffect.Read, saved.Payload.Effect);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, saved.Payload.Recovery);
        Assert.Equal(AgentToolEffectState.None, saved.EffectState);
        Assert.Contains("Original metered analysis", saved.Result!.PayloadJson, StringComparison.Ordinal);
        Assert.Contains(project, ProjectStructureDisclosureEvidenceCodec.Read(saved.DisclosureEvidence!).Targets);
        AssertOriginalGenericArguments(saved.Payload, arguments);

        await SaveImageActorAsync(services, agent, project, canRead: revokeTransform, canTransform: !revokeTransform);
        var deniedClient = new ScriptClient(toolName, arguments);
        var denial = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(fixture, services, agent, deniedClient));
        Assert.Contains("project-structure.result-disclosure-denied", Codes(denial));
        Assert.Equal(0, deniedClient.Requests);
        Assert.Single(analysis.Requests);
        Assert.Equal(saved, await ReadProposalAsync(fixture));

        await SaveImageActorAsync(services, agent, project, canRead: true, canTransform: true);
        var restored = new ScriptClient(toolName, arguments);
        Assert.Contains("completed", (await ExecuteAsync(fixture, services, agent, restored)).ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, restored.Requests);
        Assert.Contains("Original metered analysis", Assert.Single(restored.Inputs), StringComparison.Ordinal);
        Assert.Single(analysis.Requests);
        Assert.Equal(saved, await ReadProposalAsync(fixture));
    }

    private static void AssertOriginalGenericArguments(AgentToolPreparedPayload payload, Dictionary<string, object?> arguments) {
        var element = JsonSerializer.SerializeToElement(arguments, MafToolProtocolCodec.SerializationOptions);
        Assert.Equal(1, payload.SemanticVersion);
        Assert.Equal(MafToolProtocolCodec.Canonicalize(element), payload.ArgumentsJson);
        Assert.Equal(MafToolProtocolCodec.Digest(new { Name = payload.ToolName, Arguments = element }), payload.Digest);
    }

    private static Task<TestApplication> CreateImageApplicationAsync(ImageAnalysisCounter analysis)
        => TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                services.RemoveAll<IAgentImageAnalysisService>();
                services.AddSingleton<IAgentImageAnalysisService>(analysis);
            }
        });

    private static Task<ProjectStructureNode> CreateImageAsync(IServiceProvider services, ProjectWriteAdmission project)
        => services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project.ProjectId,
            new(ProjectObjectType.ImageAsset, "Managed source image", string.Empty, string.Empty, $"project:{project.ProjectId:D}",
                ObjectSubtype: "png", Media: new("metered-image.png", "image/png", ImagePng)) {
                ExpectedProjectAdmission = project
            });

    private static async Task<AgentDefinition> SaveImageActorAsync(IServiceProvider services, AgentDefinition original,
        ProjectWriteAdmission project, bool canRead, bool canTransform) {
        var access = AgentWorkspaceToolAccessMetadata.Read(original.ConfigurationJson);
        access.Profile = AgentWorkspaceToolProfileKind.Custom;
        access.CanTransformArtifacts = canTransform;
        var actor = await SaveActorAsync(services, original with {
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write(original.ConfigurationJson, access)
        }, project, canRead);
        Assert.Equal(canTransform, AgentWorkspaceToolAccessMetadata.Read(actor.ConfigurationJson).CanTransformArtifacts);
        return actor;
    }

    private static Dictionary<string, object?> ImageArguments(Guid projectId, string nodeId) => new() {
        ["projectId"] = projectId,
        ["nodeId"] = nodeId,
        ["prompt"] = "Describe this authorized image."
    };

    private const string ImagePng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    private sealed class ImageAnalysisCounter : IAgentImageAnalysisService {
        internal List<AgentImageAnalysisRequest> Requests { get; } = [];
        internal bool FailAfterDispatch { get; set; }
        internal IOException? InjectedDispatchFailure { get; private set; }

        public Task<AgentImageAnalysisResult> AnalyzeAsync(AgentImageAnalysisRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (FailAfterDispatch) {
                Assert.Null(InjectedDispatchFailure);
                InjectedDispatchFailure = new IOException("Injected acknowledgement loss after the metered analysis was dispatched.");
                throw InjectedDispatchFailure;
            }
            return Task.FromResult(new AgentImageAnalysisResult("vision-model", "Original metered analysis", 12, 4));
        }
    }
}
