using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.SharedProviders.E2E;

using AgentProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;

internal static class E2eFixtures
{
    public const string ChatCompletions = "chat-completions";
    public const string Responses = "responses";
    public const string StructuredAllow = "structured-allow";
    public const string OpenAiImage = "openai-image";
    public const string ComfyUiImage = "comfyui-image";
    public const string Unshared = "unshared";
    public const string ClientAPersonal = "client-a-personal";

    public const string CentralSourceName = "E2E central shared providers";
    public const string CentralProviderSecretName = "E2E central upstream credential";
    public const string ClientAPersonalSecretName = "E2E client A personal credential";
    public const string CentralSourceTokenSecretName = "E2E central source access token";
    public const string ClientBMismatchTokenSecretName = "E2E client B mismatch access token";

    public const string CentralAccessCredentialFileName = "central-access.token";
    public const string CentralCatalogOnlyCredentialFileName = "central-catalog-only.token";
    public const string CentralInvokeOnlyCredentialFileName = "central-invoke-only.token";
    public const string ClientAAccessCredentialFileName = "client-a-access.token";
    public const string ClientBAccessCredentialFileName = "client-b-access.token";

    public const string DuplicateModel = "e2e-duplicate-model";

    private const string StructuredModel = "e2e-structured-allow";
    private const string OpenAiImageModel = "e2e-openai-image";
    private const string ComfyUiImageModel = "e2e-comfyui-image";
    private const string UnsharedModel = "e2e-unshared";
    private const string PersonalModel = "e2e-client-a-personal";

    public static IReadOnlyList<ProviderFixture> CentralProviders { get; } =
    [
        new(
            ChatCompletions,
            "E2E Central Chat Completions",
            AgentProviderKind.OpenAi,
            ProviderTransportKind.ChatCompletions,
            ProviderProfilePurpose.Chat,
            DuplicateModel,
            IsPublished: true,
            RequiresSecret: true,
            SupportsStructuredOutput: false),
        new(
            Responses,
            "E2E Central Responses",
            AgentProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            DuplicateModel,
            IsPublished: true,
            RequiresSecret: true,
            SupportsStructuredOutput: false),
        new(
            StructuredAllow,
            "E2E Central Structured Output Allow",
            AgentProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            StructuredModel,
            IsPublished: true,
            RequiresSecret: true,
            SupportsStructuredOutput: true),
        new(
            OpenAiImage,
            "E2E Central OpenAI Image",
            AgentProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.ImageGeneration,
            OpenAiImageModel,
            IsPublished: true,
            RequiresSecret: true,
            SupportsStructuredOutput: false),
        new(
            ComfyUiImage,
            "E2E Central ComfyUI Image",
            AgentProviderKind.ComfyUi,
            ProviderTransportKind.ChatCompletions,
            ProviderProfilePurpose.ImageGeneration,
            ComfyUiImageModel,
            IsPublished: true,
            RequiresSecret: false,
            SupportsStructuredOutput: false),
        new(
            Unshared,
            "E2E Central Unshared",
            AgentProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ProviderProfilePurpose.Chat,
            UnsharedModel,
            IsPublished: false,
            RequiresSecret: true,
            SupportsStructuredOutput: true)
    ];

    public static ProviderFixture ClientAPersonalProvider { get; } = new(
        ClientAPersonal,
        "E2E Client A Personal Provider",
        AgentProviderKind.OpenAi,
        ProviderTransportKind.Responses,
        ProviderProfilePurpose.Chat,
        PersonalModel,
        IsPublished: false,
        RequiresSecret: true,
        SupportsStructuredOutput: true);

    public static IReadOnlyList<string> ClientASelection { get; } =
    [
        ChatCompletions
    ];

    public static IReadOnlyList<string> ClientBSelection { get; } =
    [
        ChatCompletions,
        Responses,
        StructuredAllow,
        OpenAiImage,
        ComfyUiImage
    ];

    public static string? ResolveFixtureId(string providerName)
    {
        var central = CentralProviders.SingleOrDefault(fixture =>
            string.Equals(fixture.Name, providerName, StringComparison.Ordinal));
        if (central is not null)
        {
            return central.Id;
        }

        return string.Equals(
            ClientAPersonalProvider.Name,
            providerName,
            StringComparison.Ordinal)
            ? ClientAPersonalProvider.Id
            : null;
    }

    public static string CreateConfigurationJson(
        ProviderFixture fixture,
        Guid? secretId)
    {
        var configuration = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ProviderConnectorFieldKeys.TimeoutSeconds] = 45
        };
        if (secretId.HasValue)
        {
            configuration[ProviderProfileMetadataPropertyNames.SecretRecordId] =
                secretId.Value.ToString("D");
        }

        if (fixture.Kind == AgentProviderKind.ComfyUi)
        {
            configuration[ProviderConnectorFieldKeys.ComfyUiWorkflowTemplateJson] =
                "{\"6\":{\"inputs\":{\"text\":\"\"},\"class_type\":\"CLIPTextEncode\"},\"9\":{\"inputs\":{},\"class_type\":\"SaveImage\"}}";
            configuration[ProviderConnectorFieldKeys.ComfyUiPositivePromptNodeId] = "6";
            configuration[ProviderConnectorFieldKeys.ComfyUiOutputNodeId] = "9";
            configuration[ProviderConnectorFieldKeys.ComfyUiPollIntervalMilliseconds] = "100";
        }

        return JsonSerializer.Serialize(configuration);
    }
}

internal sealed record ProviderFixture(
    string Id,
    string Name,
    AgentProviderKind Kind,
    ProviderTransportKind Transport,
    ProviderProfilePurpose Purpose,
    string DefaultModel,
    bool IsPublished,
    bool RequiresSecret,
    bool SupportsStructuredOutput);
