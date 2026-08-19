using System.Text;
using System.Text.Json;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class InteractiveServerConfigurationTests
{
    [Fact]
    public void Interactive_server_configures_receive_limit_for_project_asset_create_messages()
    {
        ServiceCollection services = new();
        services.AddCanDoItAllInteractiveServer(detailedErrors: true);

        using var serviceProvider = services.BuildServiceProvider();
        var componentHubOptionsType = services
            .Select(static descriptor => descriptor.ServiceType)
            .Where(static type =>
                type.IsConstructedGenericType &&
                type.GetGenericTypeDefinition() == typeof(IConfigureOptions<>))
            .Select(static type => type.GenericTypeArguments[0])
            .Where(static type =>
                type.IsConstructedGenericType &&
                type.GetGenericTypeDefinition() == typeof(HubOptions<>))
            .Distinct()
            .Single();
        var optionsAccessorType = typeof(IOptions<>).MakeGenericType(componentHubOptionsType);
        var optionsAccessor = serviceProvider.GetRequiredService(optionsAccessorType);
        var hubOptions = Assert.IsAssignableFrom<IOptions<HubOptions>>(optionsAccessor).Value;
        var uploadedBytes = new byte[48 * 1024];
        var request = new CanvasWorkbenchCreateActionRequest(
            "add-image-asset",
            "project:test",
            0,
            0,
            "project:test",
            "Transport regression image",
            string.Empty,
            string.Empty,
            "child",
            "dialog",
            string.Empty,
            new CanvasWorkbenchUploadedFile
            {
                FileName = "transport-regression.png",
                ContentType = "image/png",
                Base64Data = Convert.ToBase64String(uploadedBytes)
            });
        var serializedRequestBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(request));
        var maximumReceiveMessageBytes = Assert.IsType<long>(hubOptions.MaximumReceiveMessageSize);

        Assert.True(serializedRequestBytes > 32 * 1024);
        Assert.Equal(
            InteractiveServerServiceCollectionExtensions.MaximumReceiveMessageBytes,
            maximumReceiveMessageBytes);
        Assert.True(
            maximumReceiveMessageBytes >=
            ProjectStructureAssetUploadLimits.MaximumBase64Characters + 1024L * 1024L);
        Assert.True(serializedRequestBytes < maximumReceiveMessageBytes);
    }
}
