using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.Pages.Components;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class WorkflowImageGenerationSettingsRendererTests
{
    [Fact]
    public void Trusted_image_renderer_edits_every_schema_field_and_filters_provider_capability()
    {
        using var context = new BunitContext();
        context.Services.AddLogging();
        var imageProviderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var chatProviderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var disabledImageProviderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        context.Services.AddSingleton<IWorkflowComponentLibraryService>(new ProviderComponentLibrary(
        [
            CreateProvider(imageProviderId, "Image provider", ProviderProfilePurpose.ImageGeneration),
            CreateProvider(chatProviderId, "Chat provider", ProviderProfilePurpose.Chat),
            CreateProvider(
                disabledImageProviderId,
                "Disabled image provider",
                ProviderProfilePurpose.ImageGeneration,
                isEnabled: false)
        ]));
        context.Services.AddSingleton<ISettingsRendererRegistry>(new SettingsRendererRegistry(
        [
            new WorkflowSettingsRendererSource()
        ]));
        var descriptor = BuiltInWorkflowExecutorDescriptors.ImageGeneration;
        var state = WorkflowExecutorConfigurationMapper.ReadState(
            descriptor.DefaultSettingsJson,
            descriptor.ConfigurationSchema);
        var providerField = Assert.Single(
            descriptor.ConfigurationSchema.Fields,
            field => field.FieldType == ConfigurationFieldType.Guid);
        state.SetText(providerField.Key, disabledImageProviderId.ToString("D"));
        ConfigurationState? changedState = null;

        var cut = context.Render<SettingsRendererHost>(parameters => parameters
            .Add(component => component.RendererKey, descriptor.SetupRendererKey)
            .Add(component => component.RendererOwnerId, descriptor.Source.SourceId)
            .Add(component => component.RendererTrustLevel, SettingsRendererTrustLevel.Application)
            .Add(component => component.Schema, descriptor.ConfigurationSchema)
            .Add(component => component.State, state)
            .Add(component => component.StateChanged, updated => changedState = updated)
            .Add(component => component.TestIdPrefix, "image-settings"));

        cut.WaitForAssertion(() => Assert.Contains("Image provider", cut.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Chat provider", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-settings-renderer-resolution", cut.Markup, StringComparison.Ordinal);
        var disabledOption = cut.Find($"option[value='{disabledImageProviderId:D}']");
        Assert.True(disabledOption.HasAttribute("disabled"));
        Assert.Contains("selected image-generation provider is disabled", cut.Markup, StringComparison.Ordinal);
        Assert.All(
            descriptor.ConfigurationSchema.Fields,
            field => Assert.NotNull(cut.Find($"[data-testid='image-settings-{field.Key}']")));

        cut.Find($"[data-testid='image-settings-{providerField.Key}']")
            .Change(imageProviderId.ToString("D"));

        Assert.Same(state, changedState);
        Assert.Equal(imageProviderId.ToString("D"), state.GetText(providerField.Key));
    }

    [Fact]
    public void Image_renderer_source_matches_the_builtin_trust_contract()
    {
        var builtIn = BuiltInWorkflowExecutorDescriptors.ImageGeneration;

        var renderer = Assert.Single(new WorkflowSettingsRendererSource().ListRenderers());

        Assert.Equal(WorkflowExecutorSettingsPresentationMode.CustomRenderer, builtIn.SettingsPresentationMode);
        Assert.Equal(builtIn.SetupRendererKey, renderer.RendererKey);
        Assert.Equal(builtIn.Source.SourceId, renderer.OwnerId);
        Assert.Equal(SettingsRendererTrustLevel.Application, renderer.TrustLevel);
        Assert.Equal(builtIn.ConfigurationSchema.Version, renderer.SupportedSchemaVersion);
        Assert.Equal(typeof(WorkflowImageGenerationSettingsRenderer), renderer.ComponentType);
    }

    [Fact]
    public void Image_renderer_keeps_saved_provider_neutral_until_capabilities_load()
    {
        using var context = new BunitContext();
        context.Services.AddLogging();
        var providerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var providerCompletion = new TaskCompletionSource<IReadOnlyList<WorkflowProviderOption>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        context.Services.AddSingleton<IWorkflowComponentLibraryService>(new ProviderComponentLibrary(
            cancellationToken => providerCompletion.Task.WaitAsync(cancellationToken)));
        context.Services.AddSingleton<ISettingsRendererRegistry>(new SettingsRendererRegistry(
        [
            new WorkflowSettingsRendererSource()
        ]));
        var descriptor = BuiltInWorkflowExecutorDescriptors.ImageGeneration;
        var state = WorkflowExecutorConfigurationMapper.ReadState(
            descriptor.DefaultSettingsJson,
            descriptor.ConfigurationSchema);
        var providerField = Assert.Single(
            descriptor.ConfigurationSchema.Fields,
            field => field.FieldType == ConfigurationFieldType.Guid);
        state.SetText(providerField.Key, providerId.ToString("D"));

        var cut = context.Render<SettingsRendererHost>(parameters => parameters
            .Add(component => component.RendererKey, descriptor.SetupRendererKey)
            .Add(component => component.RendererOwnerId, descriptor.Source.SourceId)
            .Add(component => component.RendererTrustLevel, SettingsRendererTrustLevel.Application)
            .Add(component => component.Schema, descriptor.ConfigurationSchema)
            .Add(component => component.State, state)
            .Add(component => component.TestIdPrefix, "deferred-image-settings"));

        Assert.Contains("Loading image providers", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("not an available image-generation provider", cut.Markup, StringComparison.Ordinal);

        providerCompletion.SetResult(
        [
            CreateProvider(providerId, "Saved image provider", ProviderProfilePurpose.ImageGeneration)
        ]);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Saved image provider", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Loading image providers", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("not an available image-generation provider", cut.Markup, StringComparison.Ordinal);
        });
    }

    private static WorkflowProviderOption CreateProvider(
        Guid id,
        string name,
        ProviderProfilePurpose purpose,
        bool isEnabled = true)
        => new(
            id,
            name,
            CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            purpose,
            "gpt-image-1",
            ["gpt-image-1"],
            IsEnabled: isEnabled,
            SupportsStreaming: false,
            SupportsTools: false,
            SupportsStructuredOutput: false,
            SupportsVision: true,
            SupportsBackgroundResponses: false);

    private sealed class ProviderComponentLibrary(
        Func<CancellationToken, Task<IReadOnlyList<WorkflowProviderOption>>> listProviders) : IWorkflowComponentLibraryService
    {
        public ProviderComponentLibrary(IReadOnlyList<WorkflowProviderOption> providers)
            : this(_ => Task.FromResult(providers))
        {
        }

        public Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
            CancellationToken cancellationToken = default)
            => listProviders(cancellationToken);

        public Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LlmCallComponent>>([]);

        public Task<LlmCallComponent?> GetComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<LlmCallComponent?>(null);

        public Task<LlmCallComponent> SaveComponentAsync(
            LlmCallComponentSaveRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteComponentAsync(
            WorkflowComponentId componentId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
