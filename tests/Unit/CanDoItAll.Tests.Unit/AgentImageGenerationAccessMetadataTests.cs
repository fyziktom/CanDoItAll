using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentImageGenerationAccessMetadataTests
{
    [Fact]
    public void Write_and_read_round_trip_image_generation_settings()
    {
        var providerId = Guid.NewGuid();
        var configurationJson = AgentImageGenerationAccessMetadata.Write(
            """{"existing":true}""",
            new AgentImageGenerationAccessSettings
            {
                CanGenerateImages = true,
                PreferredProviderProfileId = providerId,
                DefaultModel = " gpt-image-1-mini ",
                CanStoreImagesAsProjectAssets = true
            });

        var settings = AgentImageGenerationAccessMetadata.Read(configurationJson);

        Assert.True(settings.CanGenerateImages);
        Assert.Equal(providerId, settings.PreferredProviderProfileId);
        Assert.Equal("gpt-image-1-mini", settings.DefaultModel);
        Assert.True(settings.CanStoreImagesAsProjectAssets);
        Assert.Contains("\"existing\":true", configurationJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_removes_default_image_generation_settings()
    {
        var configurationJson = AgentImageGenerationAccessMetadata.Write(
            """{"imageGeneration":{"canGenerateImages":true},"existing":true}""",
            new AgentImageGenerationAccessSettings());

        using var document = JsonDocument.Parse(configurationJson);

        Assert.True(document.RootElement.GetProperty("existing").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("imageGeneration", out _));
    }

    [Fact]
    public void Normalize_drops_generation_provider_and_model_when_generation_is_not_allowed()
    {
        var providerId = Guid.NewGuid();
        var settings = AgentImageGenerationAccessMetadata.Normalize(new AgentImageGenerationAccessSettings
        {
            CanGenerateImages = false,
            PreferredProviderProfileId = providerId,
            DefaultModel = "gpt-image-1-mini",
            CanStoreImagesAsProjectAssets = true
        });

        Assert.False(settings.CanGenerateImages);
        Assert.Null(settings.PreferredProviderProfileId);
        Assert.Empty(settings.DefaultModel);
        Assert.True(settings.CanStoreImagesAsProjectAssets);
    }
}
