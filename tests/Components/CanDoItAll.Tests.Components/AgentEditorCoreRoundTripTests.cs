using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorCoreRoundTripTests : AgentMemorySettingsPanelTestBase {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Core_only_UI_save_preserves_every_deferred_field_and_native_authority_binding(bool template) {
        var memory = CreateProvider("provider.core-roundtrip", "Core fixture memory", true);
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IMemoryProviderProfileStore>(new TestProfileStore(memory));
            services.RemoveAll<IMemoryProviderDriver>();
            services.AddSingleton<IMemoryProviderDriver>(new TestMemoryProviderDriver(MemoryProviderDriverKind.Mock));
        });
        var services = harness.Context.Services;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var project = await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Core-only editor project" });
        Assert.True(project.IsSuccess);
        var capability = (await workspace.ListCapabilitiesAsync()).First();
        var root = Path.Combine(harness.RootPath, "core-only-external-root");
        Directory.CreateDirectory(root);
        var registry = services.GetRequiredService<IExternalTargetPathRegistryFactory>().Create([]);
        Assert.True(registry.TryCreateAlias(root, out var alias));
        var bindings = registry.ExportBindings([alias]);
        Assert.Single(bindings);
        var seed = new AgentEditorModel {
            Name = "Core-only original", TemplateKey = "core-only-preserved-template", IsTemplate = template,
            Temperature = 0.43, EnableBackgroundResponses = true, RequirePerServiceCallChatHistoryPersistence = true,
            ConfigurationJson = """{"futureExtension":{"name":"retained-東京","version":37}}""",
            Permissions = AgentPermissionsPolicy.Default with { CanObserveOtherAgents = true, CanScheduleWork = true },
            AllowedSecretReferences = [new(Guid.NewGuid(), "Synthetic reference metadata", AgentSecretPurposes.GeneralAgentRequest)],
            ProjectStructureAccess = new() { CanRead = true, CanWriteTasks = true, AllowedProjectIds = [project.Value] },
            ProcessAccess = new() { CanRead = true, CanWrite = true, AllowedDefinitionIds = [Guid.NewGuid()] },
            WorkspaceToolAccess = new() {
                Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = true, CanWriteFiles = true,
                CanReadStorage = true, AllowedStorageCatalogIds = [Guid.NewGuid()],
                AllowedExternalTargetAliases = [alias], ExternalTargetRootBindings = bindings.ToList()
            },
            MemoryAccess = new() {
                InvocationMode = AgentMemoryInvocationMode.Automatic, CanUseMemoryTools = true, RequireContextContributions = true,
                PreferredProviderInstanceId = memory.InstanceId, DefaultProviderInstanceId = memory.InstanceId,
                AllowedProviderInstanceIds = [memory.InstanceId],
                ProviderBindings = [new(AgentMemoryProviderAlias.Parse("core-memory"), memory.InstanceId, true, AgentMemoryProviderRequirement.Required)],
                AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync], DeniedCapabilityIds = [MemoryCapabilityIds.OperationStatus],
                AllowedSourceScopes = [MemorySourceScope.Project, MemorySourceScope.Agent],
                ProviderAssignments = [new(MemoryProviderAssignmentScope.AgentRole, nameof(AgentWorkloadKind.Programming), memory.InstanceId)]
            },
            SelectedCapabilityIds = [capability.Id], Tags = ["preserved", AgentSpecialTags.Favorite]
        };
        var id = await workspace.SaveAgentAsync(seed);
        var otherId = await workspace.SaveAgentAsync(new() { Name = "Independent other agent", Summary = "Never edited" });
        var otherBefore = JsonSerializer.Serialize(await workspace.GetAgentEditorAsync(otherId));
        var before = await workspace.GetAgentEditorAsync(id);
        Assert.Single(before.ProjectStructureAccess.AllowedProjectLifetimes);
        var cut = harness.Context.Render<AgentDetailsDialog>(p => p.Add(x => x.AgentId, id).Add(x => x.InitialProviders, Array.Empty<ProviderProfile>()));
        cut.WaitForElement("[data-testid='agents-catalog-name']");
        var form = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Find("[data-testid='agents-catalog-name']").Input("Core-only Žluťoučký 東京");
        cut.Find("[data-testid='agents-catalog-role']").Input("Revised role");
        cut.Find("[data-testid='agents-catalog-summary']").Input("Revised summary");
        cut.Find("[data-testid='agents-catalog-instructions']").Input("Revised instructions");
        await Tab(AgentEditorSection.Runtime);
        cut.FindComponent<InputSelect<AgentLifecycleStatus>>().Find("select").Change(nameof(AgentLifecycleStatus.Active));
        cut.FindComponent<InputSelect<AgentWorkloadKind>>().Find("select").Change(nameof(AgentWorkloadKind.Programming));
        cut.FindComponent<InputSelect<AgentChatHistoryMode>>().Find("select").Change(nameof(AgentChatHistoryMode.FrameworkManaged));
        await Tab(AgentEditorSection.Images);
        cut.Find("[data-testid='agents-catalog-image-generation-project-assets']").Change(true);
        await Tab(AgentEditorSection.Voice);
        cut.Find("[data-testid='agents-catalog-voice-enabled']").Change(true);
        cut.Find("[data-testid='agents-catalog-voice-override']").Change("alloy");
        Assert.Same(form, cut.FindComponent<EditForm>().Instance.EditContext);
        await cut.Find("form").SubmitAsync();
        var saved = await workspace.GetAgentEditorAsync(id);
        Assert.Equal("Core-only Žluťoučký 東京", saved.Name);
        Assert.Equal("Revised role", saved.RoleTitle);
        Assert.Equal("Revised summary", saved.Summary);
        Assert.Equal("Revised instructions", saved.Instructions);
        Assert.Equal(AgentLifecycleStatus.Active, saved.Status);
        Assert.Equal(AgentWorkloadKind.Programming, saved.Workload);
        Assert.Equal(AgentChatHistoryMode.FrameworkManaged, saved.ChatHistoryMode);
        Assert.True(saved.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.True(saved.VoiceAccess.CanUseVoiceMode);
        Assert.Equal("alloy", saved.VoiceAccess.PreferredVoiceId);
        Assert.True(string.Equals(Deferred(before), Deferred(saved), StringComparison.Ordinal),
            "Core-only save changed a deferred field, hidden setting, binding, tag or extension. Opaque bindings are omitted from diagnostics.");
        Assert.NotEqual(before.ExpectedUpdatedAtUtc, saved.ExpectedUpdatedAtUtc);
        Assert.Equal(otherBefore, JsonSerializer.Serialize(await workspace.GetAgentEditorAsync(otherId)));
        var conflict = Assert.IsType<AgentEditorSaveOutcome.Rejected>(await services.GetRequiredService<IAgentEditorCommands>().SaveAsync(before));
        Assert.True(conflict.IsConflict);
        Assert.Equal(saved.Name, (await workspace.GetAgentEditorAsync(id)).Name);
        Assert.DoesNotContain(bindings[0].ProtectedRootToken, cut.Markup, StringComparison.Ordinal);

        async Task Tab(AgentEditorSection section) {
            await cut.InvokeAsync(() => cut.FindAll("[role='tab']")[AgentEditorSections.IndexOf(section)].ClickAsync());
        }
    }

    private static string Deferred(AgentEditorModel model) => JsonSerializer.Serialize(new {
        model.Id, model.Temperature, model.EnableBackgroundResponses, model.RequirePerServiceCallChatHistoryPersistence,
        model.TemplateKey, model.IsTemplate, model.AvatarImageUrl, model.Permissions, model.AllowedSecretReferences,
        model.ProjectStructureAccess, model.ProcessAccess, model.WorkspaceToolAccess, model.MemoryAccess,
        model.SelectedCapabilityIds, Tags = model.Tags.Order(StringComparer.Ordinal),
        Extension = JsonNode.Parse(model.ConfigurationJson)!["futureExtension"]
    });
}
