using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
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

[Trait("Category", "HostPlatform")]
public sealed class AgentEditorCoreRoundTripTests : AgentMemorySettingsPanelTestBase {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Core_then_A2_UI_saves_preserve_other_sections_extensions_and_native_authority_bindings(bool template) {
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
        Assert.Empty(cut.FindAll("[data-testid='agents-editor-refresh-pending']"));
        Assert.Equal(otherBefore, JsonSerializer.Serialize(await workspace.GetAgentEditorAsync(otherId)));
        var conflict = Assert.IsType<AgentEditorSaveOutcome.Rejected>(await services.GetRequiredService<IAgentEditorCommands>().SaveAsync(before));
        Assert.True(conflict.IsConflict);
        Assert.Equal(saved.Name, (await workspace.GetAgentEditorAsync(id)).Name);
        Assert.DoesNotContain(bindings[0].ProtectedRootToken, cut.Markup, StringComparison.Ordinal);

        var coreSaved = saved;
        var a2Form = cut.FindComponent<EditForm>().Instance.EditContext;
        await Tab(AgentEditorSection.Memory);
        cut.Find("[data-testid='agents-catalog-crm-source-read']").Change(true);
        cut.Find("[data-testid='agents-catalog-memory-required']").Change(false);
        cut.Find("[data-testid='agents-catalog-memory-new-alias']").Input("unadded-東京");
        await Tab(AgentEditorSection.ProjectStructureAccess);
        await cut.Find("[data-testid='agents-catalog-project-structure-load']").ClickAsync();
        cut.Find("[data-testid='agents-catalog-project-structure-task-write']").Change(false);
        cut.Find("[data-testid='agents-catalog-project-structure-non-task-write']").Change(true);
        await Tab(AgentEditorSection.WorkspaceTools);
        cut.Find("[data-testid='agents-catalog-workspace-profile']").Change(nameof(AgentWorkspaceToolProfileKind.ReadOnly));
        cut.Find("[data-testid='agents-catalog-workspace-write']").Change(true);
        cut.Find("[data-testid='agents-catalog-workspace-validation']").Change(true);
        Assert.DoesNotContain(bindings[0].ProtectedRootToken, cut.Markup, StringComparison.Ordinal);
        await Tab(AgentEditorSection.Secrets);
        Assert.Contains(before.AllowedSecretReferences[0].SecretId.ToString(), cut.Markup);
        await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Remove selection").ClickAsync();
        await Tab(AgentEditorSection.ProcessAccess);
        cut.Find("[data-testid='agents-catalog-process-write']").Change(false);
        Assert.Contains(before.ProcessAccess.AllowedDefinitionIds[0].ToString(), cut.Markup);
        await Tab(AgentEditorSection.Capabilities);
        Assert.Same(a2Form, cut.FindComponent<EditForm>().Instance.EditContext);
        await cut.FindAll("[data-testid='agents-details-capability-toggle']").First(button => button.TextContent.Contains("Remove", StringComparison.Ordinal)).ClickAsync();
        Assert.Empty(((AgentEditorModel)cut.FindComponent<EditForm>().Instance.EditContext!.Model).SelectedCapabilityIds);
        var allSaved = await workspace.GetAgentEditorAsync(id);
        Assert.True(allSaved.SelectedCapabilityIds.Count == 0, string.Join("; ", services.GetRequiredService<NotificationService>().Messages.Select(message => $"{message.Summary}: {message.Detail}")));
        Assert.Empty(allSaved.AllowedSecretReferences);
        Assert.Contains(MemorySourceScope.Crm, allSaved.MemoryAccess.AllowedSourceScopes);
        Assert.Contains(MemorySourceScope.Project, allSaved.MemoryAccess.AllowedSourceScopes);
        Assert.Contains(MemorySourceScope.Agent, allSaved.MemoryAccess.AllowedSourceScopes);
        Assert.False(allSaved.MemoryAccess.RequireContextContributions);
        Assert.Equal(before.MemoryAccess.ProviderBindings, allSaved.MemoryAccess.ProviderBindings);
        Assert.Equal(before.MemoryAccess.ProviderAssignments, allSaved.MemoryAccess.ProviderAssignments);
        Assert.False(allSaved.ProjectStructureAccess.CanWriteTasks);
        Assert.True(allSaved.ProjectStructureAccess.CanWriteNonTaskStructure);
        Assert.Equal(before.ProjectStructureAccess.AllowedProjectIds, allSaved.ProjectStructureAccess.AllowedProjectIds);
        Assert.Equal(before.ProjectStructureAccess.AllowedProjectLifetimes, allSaved.ProjectStructureAccess.AllowedProjectLifetimes);
        Assert.True(allSaved.WorkspaceToolAccess.CanWriteFiles);
        Assert.True(allSaved.WorkspaceToolAccess.CanRunValidationCommands);
        Assert.Equal(before.WorkspaceToolAccess.AllowedStorageCatalogIds, allSaved.WorkspaceToolAccess.AllowedStorageCatalogIds);
        Assert.True(before.WorkspaceToolAccess.ExternalTargetRootBindings.SequenceEqual(allSaved.WorkspaceToolAccess.ExternalTargetRootBindings), "Native protected root bindings changed.");
        Assert.False(allSaved.ProcessAccess.CanWrite);
        Assert.Equal(before.ProcessAccess.AllowedDefinitionIds, allSaved.ProcessAccess.AllowedDefinitionIds);
        Assert.Equal(coreSaved.Name, allSaved.Name);
        Assert.Equal(coreSaved.Instructions, allSaved.Instructions);
        Assert.Equal(JsonSerializer.Serialize(coreSaved.Permissions with { AllowedSecrets = [] }), JsonSerializer.Serialize(allSaved.Permissions));
        Assert.Equal(coreSaved.Temperature, allSaved.Temperature);
        Assert.Equal(coreSaved.EnableBackgroundResponses, allSaved.EnableBackgroundResponses);
        Assert.Equal(coreSaved.RequirePerServiceCallChatHistoryPersistence, allSaved.RequirePerServiceCallChatHistoryPersistence);
        Assert.Equal(coreSaved.TemplateKey, allSaved.TemplateKey);
        Assert.Equal(coreSaved.IsTemplate, allSaved.IsTemplate);
        Assert.Equal(JsonNode.Parse(coreSaved.ConfigurationJson)!["futureExtension"]!.ToJsonString(), JsonNode.Parse(allSaved.ConfigurationJson)!["futureExtension"]!.ToJsonString());
        Assert.Equal(otherBefore, JsonSerializer.Serialize(await workspace.GetAgentEditorAsync(otherId)));

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
