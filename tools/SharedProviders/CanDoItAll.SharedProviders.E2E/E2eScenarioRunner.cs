using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.E2E;

internal sealed class E2eScenarioRunner : IDisposable
{
    private const string AccessContextCanary = "sb07:access-context:central-only";
    private const string ContentCanary = "SB07_E2E_CONTENT_CANARY_7f3cc8c4";

    private readonly E2eScenarioOptions options;
    private readonly E2eScenarioHttpClient http = new();
    private readonly E2eScenarioData data;
    private readonly E2eScenarioResultStore results;

    public E2eScenarioRunner(E2eScenarioOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        data = new E2eScenarioData(options);
        results = new E2eScenarioResultStore(options.ArtifactRootPath);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var evidence = options.Phase switch
        {
            E2eScenarioPhase.Normal => await RunNormalAsync(cancellationToken),
            E2eScenarioPhase.Unpublished => await RunUnpublishedAsync(cancellationToken),
            E2eScenarioPhase.Republished => await RunRepublishedAsync(cancellationToken),
            E2eScenarioPhase.IdentityMismatch => await RunIdentityMismatchAsync(cancellationToken),
            E2eScenarioPhase.IdentityRestored => await RunIdentityRestoredAsync(cancellationToken),
            E2eScenarioPhase.Outage => await RunOutageAsync(cancellationToken),
            E2eScenarioPhase.Recovery => await RunRecoveryAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Phase), options.Phase, null)
        };
        var report = await results.MergeAsync(options.Phase, evidence, cancellationToken);
        if (evidence.SelectMany(item => item.Checks).Any(check => !check.Passed))
        {
            throw new E2eSafeException(
                $"The '{E2eScenarioCommandLine.ToToken(options.Phase)}' scenario phase failed. Inspect the sanitized scenario-results artifact.");
        }

        if (options.Phase == E2eScenarioPhase.Recovery && report.Status != E2eScenarioStatus.Passed)
        {
            throw new E2eSafeException(
                "The recovery phase completed but the 19-scenario aggregate is not fully passed.");
        }
    }

    public void Dispose()
        => http.Dispose();

    private async Task<IReadOnlyList<E2eScenarioStageEvidence>> RunNormalAsync(
        CancellationToken cancellationToken)
    {
        var evidence = new List<E2eScenarioStageEvidence>();
        var centralToken = await ReadCredentialAsync(
            E2eFixtures.CentralAccessCredentialFileName,
            cancellationToken);
        var catalogOnlyToken = await ReadCredentialAsync(
            E2eFixtures.CentralCatalogOnlyCredentialFileName,
            cancellationToken);
        var invokeOnlyToken = await ReadCredentialAsync(
            E2eFixtures.CentralInvokeOnlyCredentialFileName,
            cancellationToken);
        var clientAToken = await ReadCredentialAsync(
            E2eFixtures.ClientAAccessCredentialFileName,
            cancellationToken);
        var clientBToken = await ReadCredentialAsync(
            E2eFixtures.ClientBAccessCredentialFileName,
            cancellationToken);
        var upstreamControlToken = await E2eSecretFile.ReadRequiredAsync(
            options.UpstreamControlTokenFilePath,
            "upstream control token",
            cancellationToken);
        var personalControlToken = await E2eSecretFile.ReadRequiredAsync(
            options.PersonalUpstreamControlTokenFilePath,
            "personal upstream control token",
            cancellationToken);
        var knownSensitiveValues = await data.ReadKnownSensitiveValuesAsync(cancellationToken);
        var central = await data.ReadSnapshotAsync(E2eRole.Central, cancellationToken);
        var clientA = await data.ReadSnapshotAsync(E2eRole.ClientA, cancellationToken);
        var clientB = await data.ReadSnapshotAsync(E2eRole.ClientB, cancellationToken);
        var seedA = await data.ReadCheckpointSnapshotAsync(
            E2eRole.ClientA,
            "seed",
            cancellationToken);
        var seedB = await data.ReadCheckpointSnapshotAsync(
            E2eRole.ClientB,
            "seed",
            cancellationToken);
        var syncOutcomeA = await data.ReadSyncOutcomeAsync(E2eRole.ClientA, cancellationToken);
        var syncOutcomeB = await data.ReadSyncOutcomeAsync(E2eRole.ClientB, cancellationToken);
        await data.CaptureBaselineAsync(central, clientA, clientB, cancellationToken);
        var catalog = await GetCatalogAsync(centralToken, cancellationToken);
        await data.WriteCatalogEvidenceAsync(catalog.RawJson, cancellationToken);
        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.CentralCatalogPublicationBoundary,
            async builder =>
            {
                var unshared = FindFixture(central, E2eFixtures.Unshared);
                using var modelsResponse = await http.GetAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Models,
                    centralToken,
                    accessContext: null,
                    cancellationToken);
                var modelList = modelsResponse.StatusCode == HttpStatusCode.OK
                    ? await http.ReadJsonAsync<SharedProviderOpenAiModelList>(
                        modelsResponse,
                        cancellationToken)
                    : null;
                var catalogModelIds = catalog.Document.Providers
                    .SelectMany(provider => provider.Models)
                    .Select(model => model.Id.Value)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var openAiModelIds = modelList?.Data
                    .Select(model => model.Id.Value)
                    .Order(StringComparer.Ordinal)
                    .ToArray() ?? [];
                var rawProviderModelIds = central.Providers
                    .Select(provider => provider.DefaultModel)
                    .ToHashSet(StringComparer.Ordinal);
                builder.Expect("six-central-fixtures", central.Fixtures.Count == 6);
                builder.Expect("five-published", central.Fixtures.Count(item => item.IsPublished == true) == 5);
                builder.Expect("unshared-default-off", unshared.IsPublished == false);
                builder.Expect("catalog-filters-unshared", catalog.Document.Providers.Count == 5 &&
                    catalog.Document.Providers.All(item => item.PublicationId.Value != unshared.PublicationId));
                builder.Expect("openai-model-list-ok", modelsResponse.StatusCode == HttpStatusCode.OK);
                builder.Expect("openai-model-list-exact-public-ids", openAiModelIds.Length == 5 &&
                    openAiModelIds.SequenceEqual(catalogModelIds, StringComparer.Ordinal));
                builder.Expect("openai-model-list-routing-ids-only", openAiModelIds.All(modelId =>
                    SharedProviderRoutingModelIdCodec.TryParse(modelId, out _, out _)) &&
                    openAiModelIds.All(modelId => !rawProviderModelIds.Contains(modelId)));
                builder.Expect("catalog-sanitized", IsCatalogSanitized(
                    catalog.RawJson,
                    central,
                    knownSensitiveValues));
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.ClientATextImportWithPersonalProvider,
            builder =>
            {
                var personal = FindFixture(clientA, E2eFixtures.ClientAPersonal);
                var expectedText = FindFixture(central, E2eFixtures.ChatCompletions);
                builder.Expect("one-selected-text-import", clientA.Imports.Count == 1 &&
                    clientA.Imports[0].RemotePublicationId == expectedText.PublicationId &&
                    clientA.Imports[0].RemotePurpose == ProviderProfilePurpose.Chat.ToString() &&
                    clientA.Imports[0].SelectionState == "Selected" &&
                    clientA.Imports[0].AvailabilityState == "Available");
                builder.Expect("personal-provider-coexists", clientA.Providers.Any(item =>
                    item.Id == personal.ProviderProfileId) &&
                    clientA.Imports.All(item => item.ProviderProfileId != personal.ProviderProfileId));
                builder.Expect("central-source-available", clientA.Sources.Count == 1 &&
                    clientA.Sources[0].Status == "Available");
                return Task.CompletedTask;
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.ClientBTextAndImageImports,
            builder =>
            {
                builder.Expect("five-selected-imports", clientB.Imports.Count == 5 &&
                    clientB.Imports.All(item => item.SelectionState == "Selected"));
                builder.Expect("text-and-image-imports", clientB.Imports.Count(item =>
                    item.RemotePurpose == "Chat") == 3 &&
                    clientB.Imports.Count(item => item.RemotePurpose == "ImageGeneration") == 2);
                builder.Expect("all-imports-available", clientB.Imports.All(item =>
                    item.AvailabilityState == "Available"));
                return Task.CompletedTask;
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.SourceResyncIdempotencyAndStableLocalIds,
            builder =>
            {
                builder.Expect("client-a-no-duplicates", HasUniqueImportIdentities(clientA));
                builder.Expect("client-b-no-duplicates", HasUniqueImportIdentities(clientB));
                builder.Expect("client-a-stable-local-ids", SameImportIdentities(seedA, clientA));
                builder.Expect("client-b-stable-local-ids", SameImportIdentities(seedB, clientB));
                builder.Expect("source-ids-stable", seedA.Sources.Single().Id == clientA.Sources.Single().Id &&
                    seedB.Sources.Single().Id == clientB.Sources.Single().Id);
                builder.Expect("second-sync-observed", clientA.CapturedAtUtc > seedA.CapturedAtUtc &&
                    clientB.CapturedAtUtc > seedB.CapturedAtUtc &&
                    syncOutcomeA.SchemaVersion == 1 &&
                    syncOutcomeA.Role == E2eRole.ClientA &&
                    syncOutcomeA.Outcome == SharedProviderSourceOperationOutcome.NotModified &&
                    syncOutcomeA.CompletedAtUtc > seedA.CapturedAtUtc &&
                    syncOutcomeB.SchemaVersion == 1 &&
                    syncOutcomeB.Role == E2eRole.ClientB &&
                    syncOutcomeB.Outcome == SharedProviderSourceOperationOutcome.NotModified &&
                    syncOutcomeB.CompletedAtUtc > seedB.CapturedAtUtc);
                return Task.CompletedTask;
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.DuplicateUpstreamModelRouting,
            async builder =>
            {
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
                var chatModel = FindModel(catalog.Document, central, E2eFixtures.ChatCompletions);
                var responsesModel = FindModel(catalog.Document, central, E2eFixtures.Responses);
                var clientAResult = await InvokeClientProviderAsync(
                    options.ClientABaseUri,
                    clientAToken,
                    FindImport(clientA, FindFixture(central, E2eFixtures.ChatCompletions)),
                    "deterministic client A multi-hop checkpoint",
                    accessContext: null,
                    cancellationToken);
                var clientBResult = await InvokeClientProviderAsync(
                    options.ClientBBaseUri,
                    clientBToken,
                    FindImport(clientB, FindFixture(central, E2eFixtures.Responses)),
                    "deterministic client B multi-hop checkpoint",
                    accessContext: null,
                    cancellationToken);
                using var chat = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    accessContext: null,
                    CreateChat(chatModel.Value),
                    cancellationToken);
                using var responses = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Responses,
                    centralToken,
                    accessContext: null,
                    CreateResponses(responsesModel.Value),
                    cancellationToken);
                var captures = await GetCapturesAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    cancellationToken);
                builder.Expect("public-model-ids-distinct", chatModel != responsesModel);
                builder.Expect("duplicate-upstream-name", FindProviderDefaultModel(
                    central,
                    E2eFixtures.ChatCompletions) == E2eFixtures.DuplicateModel &&
                    FindProviderDefaultModel(
                        central,
                        E2eFixtures.Responses) == E2eFixtures.DuplicateModel);
                builder.Expect("both-routes-succeeded", chat.StatusCode == HttpStatusCode.OK &&
                    responses.StatusCode == HttpStatusCode.OK);
                builder.Expect("client-a-chat-multi-hop", clientAResult.Succeeded);
                builder.Expect("client-b-responses-multi-hop", clientBResult.Succeeded);
                builder.Expect("exact-upstream-surfaces", captures.Requests.Count(item =>
                    item.Path == "/v1/chat/completions") == 2 &&
                    captures.Requests.Count(item => item.Path == "/v1/responses") == 2 &&
                    captures.Requests.Where(item => item.Path is
                            "/v1/chat/completions" or "/v1/responses")
                        .All(item => item.Body.Contains(
                            $"\"model\":\"{E2eFixtures.DuplicateModel}\"",
                            StringComparison.Ordinal)));
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.ChatCompletionsAndResponsesBuffered,
            async builder =>
            {
                var chatModel = FindModel(catalog.Document, central, E2eFixtures.ChatCompletions);
                var responsesModel = FindModel(catalog.Document, central, E2eFixtures.Responses);
                using var chat = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    accessContext: null,
                    CreateChat(chatModel.Value),
                    cancellationToken);
                using var responses = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Responses,
                    centralToken,
                    accessContext: null,
                    CreateResponses(responsesModel.Value),
                    cancellationToken);
                builder.Expect("chat-buffered-ok", chat.StatusCode == HttpStatusCode.OK &&
                    IsJson(chat.Content.Headers.ContentType?.MediaType));
                builder.Expect("responses-buffered-ok", responses.StatusCode == HttpStatusCode.OK &&
                    IsJson(responses.Content.Headers.ContentType?.MediaType));
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
                var unknownModel = SharedProviderRoutingModelIdCodec.Create(
                    new SharedProviderPublicationId(Guid.Parse("4a13da64-5383-4de8-ad1b-5b8c7e7ebea5")),
                    "unknown-model");
                using var unknown = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    accessContext: null,
                    CreateChat(unknownModel.Value),
                    cancellationToken);
                var serializedChatModel = JsonSerializer.Serialize(chatModel.Value);
                using var uriTamper = await http.PostRawJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    accessContext: null,
                    $$"""{"model":{{serializedChatModel}},"messages":[{"role":"user","content":"tamper"}],"base_url":"https://attacker.invalid/v1"}""",
                    cancellationToken);
                using var crossRoute = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    accessContext: null,
                    CreateChat(FindModel(catalog.Document, central, E2eFixtures.OpenAiImage).Value),
                    cancellationToken);
                var invalidCaptures = await GetCapturesAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    cancellationToken);
                builder.Expect("unknown-model-rejected", unknown.StatusCode == HttpStatusCode.NotFound);
                builder.Expect("caller-uri-tamper-rejected", uriTamper.StatusCode == HttpStatusCode.BadRequest);
                builder.Expect("cross-route-model-rejected", crossRoute.StatusCode == HttpStatusCode.Conflict);
                builder.Expect("routing-negatives-not-dispatched", invalidCaptures.Count == 0);
                var failureStatuses = await ObserveControlledFailuresAsync(
                    chatModel.Value,
                    centralToken,
                    upstreamControlToken,
                    cancellationToken);
                builder.Expect("upstream-400-mapped", failureStatuses.BadRequest == HttpStatusCode.BadGateway);
                builder.Expect("upstream-401-mapped", failureStatuses.Unauthorized == HttpStatusCode.BadGateway);
                builder.Expect("upstream-429-mapped", failureStatuses.RateLimited == HttpStatusCode.TooManyRequests);
                builder.Expect("upstream-500-mapped", failureStatuses.InternalServerError == HttpStatusCode.BadGateway);
                builder.Expect("upstream-timeout-mapped", failureStatuses.Timeout == HttpStatusCode.GatewayTimeout &&
                    failureStatuses.TimeoutElapsed < TimeSpan.FromSeconds(55));
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.ChatCompletionsAndResponsesStreaming,
            async builder =>
            {
                var chat = await http.ReadSseAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    CreateChat(
                        FindModel(catalog.Document, central, E2eFixtures.ChatCompletions).Value,
                        stream: true),
                    cancellationToken);
                var responses = await http.ReadSseAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Responses,
                    centralToken,
                    CreateResponses(
                        FindModel(catalog.Document, central, E2eFixtures.Responses).Value,
                        stream: true),
                    cancellationToken);
                builder.Expect("chat-first-chunk-before-completion", IsIncremental(chat));
                builder.Expect("responses-first-chunk-before-completion", IsIncremental(responses));
                builder.Expect("chat-multiple-chunks-terminal", chat.DataFrameCount >= 3 && chat.HasDoneFrame);
                builder.Expect("responses-multiple-chunks-terminal", responses.DataFrameCount >= 3 &&
                    responses.HasResponsesCompletedEvent && responses.HasDoneFrame);
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.FunctionToolCallRoundtrip,
            async builder =>
            {
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
                using var response = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    accessContext: null,
                    CreateToolChat(FindModel(catalog.Document, central, E2eFixtures.ChatCompletions).Value),
                    cancellationToken);
                var responseJson = response.StatusCode == HttpStatusCode.OK
                    ? await http.ReadBoundedStringAsync(response, cancellationToken)
                    : string.Empty;
                var captures = await GetCapturesAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    cancellationToken);
                var capture = captures.Requests.SingleOrDefault(item =>
                    item.Path == "/v1/chat/completions");
                builder.Expect("tool-call-response-ok", response.StatusCode == HttpStatusCode.OK);
                builder.Expect("tool-definition-forwarded", capture is not null &&
                    capture.Body.Contains("\"name\":\"weather\"", StringComparison.Ordinal) &&
                    capture.Body.Contains("\"required\":[\"city\"]", StringComparison.Ordinal));
                builder.Expect("tool-call-returned-not-executed", responseJson.Contains(
                    "\"tool_calls\"",
                    StringComparison.Ordinal) && responseJson.Contains(
                    "\"name\":\"weather\"",
                    StringComparison.Ordinal));
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
                var responsesModel = FindModel(
                    catalog.Document,
                    central,
                    E2eFixtures.Responses).Value;
                using var builtInTool = await http.PostRawJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Responses,
                    centralToken,
                    accessContext: null,
                    $$"""{"model":{{JsonSerializer.Serialize(responsesModel)}},"input":"reject hosted tool","tools":[{"type":"web_search"}]}""",
                    cancellationToken);
                var rejectedCaptures = await GetCapturesAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    cancellationToken);
                builder.Expect("built-in-tool-rejected", builtInTool.StatusCode == HttpStatusCode.BadRequest);
                builder.Expect("built-in-tool-not-dispatched", rejectedCaptures.Count == 0);
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.StructuredOutputCapabilityAllowDeny,
            async builder =>
            {
                using var allowed = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Responses,
                    centralToken,
                    accessContext: null,
                    CreateStructuredResponses(
                        FindModel(catalog.Document, central, E2eFixtures.StructuredAllow).Value),
                    cancellationToken);
                using var denied = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    accessContext: null,
                    CreateStructuredChat(
                        FindModel(catalog.Document, central, E2eFixtures.ChatCompletions).Value),
                    cancellationToken);
                var allowedBody = allowed.StatusCode == HttpStatusCode.OK
                    ? await http.ReadBoundedStringAsync(allowed, cancellationToken)
                    : string.Empty;
                builder.Expect("structured-capable-succeeds", allowed.StatusCode == HttpStatusCode.OK &&
                    HasStructuredFixturePayload(allowedBody));
                var allowCapabilities = FindCapabilities(
                    catalog.Document,
                    central,
                    E2eFixtures.StructuredAllow);
                var denyCapabilities = FindCapabilities(
                    catalog.Document,
                    central,
                    E2eFixtures.ChatCompletions);
                builder.Expect("structured-capability-advertised-only-on-allow", allowCapabilities.Contains(
                    SharedProviderCapability.StructuredOutput) && !denyCapabilities.Contains(
                    SharedProviderCapability.StructuredOutput));
                builder.Expect("structured-incapable-rejected", denied.StatusCode == HttpStatusCode.BadRequest);
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.OpenAiAndComfyUiImageGeneration,
            async builder =>
            {
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
                using var openAi = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ImageGenerations,
                    centralToken,
                    accessContext: null,
                    CreateImage(FindModel(catalog.Document, central, E2eFixtures.OpenAiImage).Value, "png"),
                    cancellationToken);
                using var comfyUi = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ImageGenerations,
                    centralToken,
                    accessContext: null,
                    CreateImage(FindModel(catalog.Document, central, E2eFixtures.ComfyUiImage).Value, "png"),
                    cancellationToken);
                var captures = await GetCapturesAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    cancellationToken);
                builder.Expect("openai-image-png", await HasImageSignatureAsync(
                    openAi,
                    E2eImageFormat.Png,
                    cancellationToken));
                builder.Expect("comfyui-image-png", await HasImageSignatureAsync(
                    comfyUi,
                    E2eImageFormat.Png,
                    cancellationToken));
                builder.Expect("openai-image-adapter-used", captures.Requests.Count(item =>
                    item.Path == "/v1/images/generations") == 1);
                builder.Expect("comfyui-image-adapter-used", captures.Requests.Count(item =>
                    item.Path == "/prompt") == 1 &&
                    captures.Requests.Any(item => item.Path.StartsWith("/history/", StringComparison.Ordinal)) &&
                    captures.Requests.Any(item => item.Path == "/view"));
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.CatalogEtagNotModified,
            async builder =>
            {
                using var response = await http.GetIfNoneMatchAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Catalog,
                    centralToken,
                    catalog.EntityTag,
                    cancellationToken);
                builder.Expect("etag-present", !string.IsNullOrWhiteSpace(catalog.EntityTag));
                builder.Expect("conditional-get-304", response.StatusCode == HttpStatusCode.NotModified);
                builder.Expect("not-modified-empty", string.IsNullOrEmpty(
                    await http.ReadBoundedStringAsync(response, cancellationToken)));
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.CatalogAndInferenceScopeIsolation,
            async builder =>
            {
                var model = FindModel(catalog.Document, central, E2eFixtures.ChatCompletions).Value;
                using var catalogAllowed = await http.GetAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Catalog,
                    catalogOnlyToken,
                    accessContext: null,
                    cancellationToken);
                using var inferenceDenied = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    catalogOnlyToken,
                    accessContext: null,
                    CreateChat(model),
                    cancellationToken);
                using var catalogDenied = await http.GetAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Catalog,
                    invokeOnlyToken,
                    accessContext: null,
                    cancellationToken);
                using var inferenceAllowed = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    invokeOnlyToken,
                    accessContext: null,
                    CreateChat(model),
                    cancellationToken);
                using var invalidToken = await http.GetAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Catalog,
                    "invalid-e2e-token",
                    accessContext: null,
                    cancellationToken);
                using var missingToken = await http.GetAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Catalog,
                    bearerToken: null,
                    accessContext: null,
                    cancellationToken);
                using var clientTokenAtCentral = await http.GetAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Models,
                    clientAToken,
                    accessContext: null,
                    cancellationToken);
                builder.Expect("catalog-scope-only", catalogAllowed.StatusCode == HttpStatusCode.OK &&
                    inferenceDenied.StatusCode == HttpStatusCode.Forbidden);
                builder.Expect("invoke-scope-only", catalogDenied.StatusCode == HttpStatusCode.Forbidden &&
                    inferenceAllowed.StatusCode == HttpStatusCode.OK);
                builder.Expect("invalid-token-rejected", invalidToken.StatusCode == HttpStatusCode.Unauthorized);
                builder.Expect("missing-token-rejected", missingToken.StatusCode == HttpStatusCode.Unauthorized);
                builder.Expect(
                    "cross-role-token-rejected",
                    clientTokenAtCentral.StatusCode == HttpStatusCode.Unauthorized);
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.MalformedAccessContextRejected,
            async builder =>
            {
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
                using var response = await http.PostJsonAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.ChatCompletions,
                    centralToken,
                    "invalid context value",
                    CreateChat(FindModel(catalog.Document, central, E2eFixtures.ChatCompletions).Value),
                    cancellationToken);
                var captures = await GetCapturesAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    cancellationToken);
                builder.Expect("malformed-context-400", response.StatusCode == HttpStatusCode.BadRequest);
                builder.Expect("malformed-context-not-forwarded", captures.Count == 0);
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.AccessContextCentralOnly,
            async builder =>
            {
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
                var traceContext = E2eTraceContext.Create();
                var invocationResult = await InvokeClientProviderAsync(
                    options.ClientABaseUri,
                    clientAToken,
                    FindImport(clientA, FindFixture(central, E2eFixtures.ChatCompletions)),
                    ContentCanary,
                    AccessContextCanary,
                    cancellationToken,
                    traceContext);
                var captures = await GetCapturesAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    cancellationToken);
                var invocation = captures.Requests.SingleOrDefault(item =>
                    item.Path == "/v1/chat/completions");
                var audit = await data.ObserveAuditAsync(
                    AccessContextCanary,
                    ContentCanary,
                    sensitiveValues: [],
                    expectedTraceId: traceContext.TraceId.ToString(),
                    cancellationToken: cancellationToken);
                var upstreamTraceParent = ReadSingleSafeHeader(
                    invocation,
                    E2eTraceContext.TraceParentHeaderName);
                var upstreamTraceState = ReadSingleSafeHeader(
                    invocation,
                    E2eTraceContext.TraceStateHeaderName);
                var upstreamTraceValid = ActivityContext.TryParse(
                    upstreamTraceParent,
                    upstreamTraceState,
                    isRemote: true,
                    out var upstreamTraceContext);
                builder.Expect("access-context-client-multi-hop-ok", invocationResult.Succeeded);
                builder.Expect("access-context-in-central-audit", audit.AccessContextObserved);
                builder.Expect("trace-id-in-central-audit", audit.TraceIdObserved);
                builder.Expect("trace-context-independent", audit.ContextsIndependent);
                builder.Expect("upstream-traceparent-valid", upstreamTraceValid);
                builder.Expect("upstream-trace-id-preserved", upstreamTraceValid &&
                    upstreamTraceContext.TraceId == traceContext.TraceId);
                builder.Expect("upstream-trace-flags-preserved", upstreamTraceValid &&
                    upstreamTraceContext.TraceFlags == traceContext.TraceFlags);
                builder.Expect("upstream-span-advanced", upstreamTraceValid &&
                    upstreamTraceContext.SpanId != default &&
                    upstreamTraceContext.SpanId != traceContext.ParentSpanId);
                builder.Expect("upstream-tracestate-preserved", string.Equals(
                    upstreamTraceState,
                    traceContext.TraceState,
                    StringComparison.Ordinal));
                builder.Expect("access-context-not-w3c-baggage", invocation is not null &&
                    !invocation.Headers.Names.Contains(
                        E2eTraceContext.BaggageHeaderName,
                        StringComparer.OrdinalIgnoreCase));
                builder.Expect("access-context-not-upstream-header", invocation is not null &&
                    !invocation.Headers.Names.Contains(
                        SharedProviderHeaders.AccessContextReference,
                        StringComparer.OrdinalIgnoreCase));
                builder.Expect("access-context-not-upstream-body", invocation is not null &&
                    !invocation.Body.Contains(AccessContextCanary, StringComparison.Ordinal));
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.UnpublishAndReappearance,
            builder =>
            {
                var chat = FindFixture(central, E2eFixtures.ChatCompletions);
                builder.Expect("initially-published", chat.IsPublished == true &&
                    catalog.Document.Providers.Any(item => item.PublicationId.Value == chat.PublicationId));
                builder.Expect("initial-client-imports-available", FindImport(clientA, chat).AvailabilityState == "Available" &&
                    FindImport(clientB, chat).AvailabilityState == "Available");
                return Task.CompletedTask;
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.CentralOutageRecoveryNoFallback,
            async builder =>
            {
                using var centralHealth = await http.GetAsync(
                    options.CentralBaseUri,
                    "/health",
                    bearerToken: null,
                    accessContext: null,
                    cancellationToken);
                using var clientAHealth = await http.GetAsync(
                    options.ClientABaseUri,
                    "/health",
                    bearerToken: null,
                    accessContext: null,
                    cancellationToken);
                using var clientBHealth = await http.GetAsync(
                    options.ClientBBaseUri,
                    "/health",
                    bearerToken: null,
                    accessContext: null,
                    cancellationToken);
                builder.Expect("normal-hosts-healthy", centralHealth.StatusCode == HttpStatusCode.OK &&
                    clientAHealth.StatusCode == HttpStatusCode.OK &&
                    clientBHealth.StatusCode == HttpStatusCode.OK);
                builder.Expect("normal-imports-available", clientA.Imports.All(item =>
                    item.AvailabilityState == "Available") && clientB.Imports.All(item =>
                    item.AvailabilityState == "Available"));
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.SourceIdentityMismatch,
            builder =>
            {
                builder.Expect("source-bound-to-central-identity", clientA.Sources.Count == 1 &&
                    clientA.Sources[0].RemoteInstanceId == central.ServiceInstanceId);
                builder.Expect("source-baseline-available", clientA.Sources[0].Status == "Available");
                builder.Expect("import-baseline-preserved", SameImportIdentities(seedA, clientA));
                return Task.CompletedTask;
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.StreamingDisconnectCancellation,
            async builder =>
            {
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
                await SetControlAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    E2eFixtureFailureMode.None,
                    E2eFixtureSurface.ChatCompletions,
                    cancellationToken,
                    E2eFixtureStreamMode.HoldAfterFirstFrame);
                try
                {
                    var cancellation = await http.CancelAfterFirstSseDataAsync(
                        options.CentralBaseUri,
                        SharedProviderRoutes.ChatCompletions,
                        centralToken,
                        CreateChat(
                            FindModel(catalog.Document, central, E2eFixtures.ChatCompletions).Value,
                            stream: true),
                        cancellationToken);
                    var upstreamCancelled = await WaitForUpstreamCancellationAsync(
                        options.UpstreamControlBaseUri,
                        upstreamControlToken,
                        "/v1/chat/completions",
                        cancellationToken);
                    builder.Expect("stream-first-data-received", cancellation.StatusCode == HttpStatusCode.OK &&
                        cancellation.FirstDataReceived &&
                        cancellation.FirstDataWasNonTerminal);
                    builder.Expect("disconnect-cancels-upstream", upstreamCancelled);
                }
                finally
                {
                    await SetControlAsync(
                        options.UpstreamControlBaseUri,
                        upstreamControlToken,
                        E2eFixtureFailureMode.None,
                        E2eFixtureSurface.All,
                        cancellationToken);
                }
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.SecretContentAuditRedaction,
            async builder =>
            {
                var database = await data.ObserveDatabaseIsolationAsync(cancellationToken);
                using var controlWithoutAuth = await http.SendControlAsync<object>(
                    options.UpstreamControlBaseUri,
                    HttpMethod.Get,
                    "/_test/control",
                    bearerToken: null,
                    request: null,
                    cancellationToken);
                using var dataWithControlToken = await http.GetAsync(
                    options.UpstreamControlBaseUri,
                    "/v1/models",
                    upstreamControlToken,
                    accessContext: null,
                    cancellationToken);
                using var authorizedControl = await http.SendControlAsync<object>(
                    options.UpstreamControlBaseUri,
                    HttpMethod.Get,
                    "/_test/control",
                    upstreamControlToken,
                    request: null,
                    cancellationToken);
                builder.Expect("three-databases-queryable", database.AllRolesConnect &&
                    database.AllRolesQueryable && database.DistinctDatabases && database.DistinctUsers);
                var serviceInstanceIds = new[]
                {
                    central.ServiceInstanceId,
                    clientA.ServiceInstanceId,
                    clientB.ServiceInstanceId
                };
                builder.Expect("three-distinct-service-identities", serviceInstanceIds.All(item => item.HasValue) &&
                    serviceInstanceIds.Select(item => item!.Value).Distinct().Count() == 3);
                builder.Expect("cross-role-db-access-denied", database.AllCrossRoleConnectionsDenied);
                builder.Expect("control-auth-required", controlWithoutAuth.StatusCode == HttpStatusCode.Unauthorized &&
                    authorizedControl.StatusCode == HttpStatusCode.OK);
                builder.Expect("control-token-cannot-invoke", dataWithControlToken.StatusCode == HttpStatusCode.Unauthorized);
                await ResetCapturesAsync(options.PersonalUpstreamControlBaseUri, personalControlToken, cancellationToken);
            },
            cancellationToken);

        return evidence;
    }

    private async Task<IReadOnlyList<E2eScenarioStageEvidence>> RunUnpublishedAsync(
        CancellationToken cancellationToken)
    {
        var evidence = new List<E2eScenarioStageEvidence>();
        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.UnpublishAndReappearance,
            async builder =>
            {
                var baseline = await data.ReadBaselineAsync(cancellationToken);
                var central = await data.ReadSnapshotAsync(E2eRole.Central, cancellationToken);
                var clientA = await data.ReadSnapshotAsync(E2eRole.ClientA, cancellationToken);
                var clientB = await data.ReadSnapshotAsync(E2eRole.ClientB, cancellationToken);
                var chat = FindFixture(central, E2eFixtures.ChatCompletions);
                var clientAToken = await ReadCredentialAsync(
                    E2eFixtures.ClientAAccessCredentialFileName,
                    cancellationToken);
                var personalControlToken = await E2eSecretFile.ReadRequiredAsync(
                    options.PersonalUpstreamControlTokenFilePath,
                    "personal upstream control token",
                    cancellationToken);
                await ResetCapturesAsync(
                    options.PersonalUpstreamControlBaseUri,
                    personalControlToken,
                    cancellationToken);
                var importedChat = FindImport(clientA, chat);
                var unavailableInvocation = await InvokeClientProviderAsync(
                    options.ClientABaseUri,
                    clientAToken,
                    importedChat,
                    "shared model must not run while unpublished",
                    accessContext: null,
                    cancellationToken);
                var personalCaptures = await GetCapturesAsync(
                    options.PersonalUpstreamControlBaseUri,
                    personalControlToken,
                    cancellationToken);
                using var catalogResponse = await http.GetAsync(
                    options.CentralBaseUri,
                    SharedProviderRoutes.Catalog,
                    await ReadCredentialAsync(E2eFixtures.CentralAccessCredentialFileName, cancellationToken),
                    accessContext: null,
                    cancellationToken);
                var currentCatalog = catalogResponse.StatusCode == HttpStatusCode.OK
                    ? SharedProviderProtocolJson.DeserializeCatalog(
                        await http.ReadBoundedStringAsync(catalogResponse, cancellationToken))
                    : null;
                builder.Expect("publication-disabled", chat.IsPublished == false);
                builder.Expect("catalog-route-removed", currentCatalog is not null &&
                    currentCatalog.Providers.All(item => item.PublicationId.Value != chat.PublicationId));
                builder.Expect("imports-authoritatively-unpublished", FindImport(clientA, chat).AvailabilityState == "Unpublished" &&
                    FindImport(clientB, chat).AvailabilityState == "Unpublished");
                builder.Expect("local-ids-preserved-while-unpublished", SameImportIdentities(baseline.ClientA, clientA) &&
                    SameImportIdentities(baseline.ClientB, clientB));
                builder.Expect("unpublished-shared-inference-rejected", IsTypedProviderUnavailable(
                    unavailableInvocation,
                    importedChat.ProviderProfileId));
                builder.Expect("unpublished-no-personal-fallback", personalCaptures.Count == 0);
            },
            cancellationToken);
        return evidence;
    }

    private async Task<IReadOnlyList<E2eScenarioStageEvidence>> RunRepublishedAsync(
        CancellationToken cancellationToken)
    {
        var evidence = new List<E2eScenarioStageEvidence>();
        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.UnpublishAndReappearance,
            async builder =>
            {
                var baseline = await data.ReadBaselineAsync(cancellationToken);
                var central = await data.ReadSnapshotAsync(E2eRole.Central, cancellationToken);
                var clientA = await data.ReadSnapshotAsync(E2eRole.ClientA, cancellationToken);
                var clientB = await data.ReadSnapshotAsync(E2eRole.ClientB, cancellationToken);
                var chat = FindFixture(central, E2eFixtures.ChatCompletions);
                var catalog = await GetCatalogAsync(
                    await ReadCredentialAsync(E2eFixtures.CentralAccessCredentialFileName, cancellationToken),
                    cancellationToken);
                var invocation = await InvokeClientProviderAsync(
                    options.ClientABaseUri,
                    await ReadCredentialAsync(E2eFixtures.ClientAAccessCredentialFileName, cancellationToken),
                    FindImport(clientA, chat),
                    "shared model works after republish",
                    accessContext: null,
                    cancellationToken);
                builder.Expect("publication-restored", chat.IsPublished == true &&
                    catalog.Document.Providers.Any(item => item.PublicationId.Value == chat.PublicationId));
                builder.Expect("imports-available-after-republish", FindImport(clientA, chat).AvailabilityState == "Available" &&
                    FindImport(clientB, chat).AvailabilityState == "Available");
                builder.Expect("local-ids-preserved-after-republish", SameImportIdentities(baseline.ClientA, clientA) &&
                    SameImportIdentities(baseline.ClientB, clientB));
                builder.Expect("republished-shared-inference-succeeds", invocation.Succeeded);
            },
            cancellationToken);
        return evidence;
    }

    private async Task<IReadOnlyList<E2eScenarioStageEvidence>> RunIdentityMismatchAsync(
        CancellationToken cancellationToken)
    {
        var evidence = new List<E2eScenarioStageEvidence>();
        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.SourceIdentityMismatch,
            async builder =>
            {
                var baseline = await data.ReadBaselineAsync(cancellationToken);
                var current = await data.ReadSnapshotAsync(E2eRole.ClientA, cancellationToken);
                var source = current.Sources.Single();
                var personalControlToken = await E2eSecretFile.ReadRequiredAsync(
                    options.PersonalUpstreamControlTokenFilePath,
                    "personal upstream control token",
                    cancellationToken);
                await ResetCapturesAsync(
                    options.PersonalUpstreamControlBaseUri,
                    personalControlToken,
                    cancellationToken);
                var importedChat = FindImport(
                    current,
                    FindFixture(baseline.Central, E2eFixtures.ChatCompletions));
                var invocation = await InvokeClientProviderAsync(
                    options.ClientABaseUri,
                    await ReadCredentialAsync(E2eFixtures.ClientAAccessCredentialFileName, cancellationToken),
                    importedChat,
                    "shared model must not run across an identity mismatch",
                    accessContext: null,
                    cancellationToken);
                var personalCaptures = await GetCapturesAsync(
                    options.PersonalUpstreamControlBaseUri,
                    personalControlToken,
                    cancellationToken);
                builder.Expect("identity-mismatch-explicit", source.Status == "SourceIdentityMismatch");
                builder.Expect("remote-identity-not-rebound", source.RemoteInstanceId ==
                    baseline.ClientA.Sources.Single().RemoteInstanceId);
                builder.Expect("imports-not-deleted-on-mismatch", SameImportIdentities(baseline.ClientA, current));
                builder.Expect("source-points-to-client-b-probe", SameAuthority(source.BaseUri, options.ClientBBaseUri));
                builder.Expect("mismatch-shared-inference-rejected", IsTypedProviderUnavailable(
                    invocation,
                    importedChat.ProviderProfileId));
                builder.Expect("mismatch-no-personal-fallback", personalCaptures.Count == 0);
            },
            cancellationToken);
        return evidence;
    }

    private async Task<IReadOnlyList<E2eScenarioStageEvidence>> RunIdentityRestoredAsync(
        CancellationToken cancellationToken)
    {
        var evidence = new List<E2eScenarioStageEvidence>();
        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.SourceIdentityMismatch,
            async builder =>
            {
                var baseline = await data.ReadBaselineAsync(cancellationToken);
                var current = await data.ReadSnapshotAsync(E2eRole.ClientA, cancellationToken);
                var source = current.Sources.Single();
                var invocation = await InvokeClientProviderAsync(
                    options.ClientABaseUri,
                    await ReadCredentialAsync(E2eFixtures.ClientAAccessCredentialFileName, cancellationToken),
                    FindImport(current, FindFixture(baseline.Central, E2eFixtures.ChatCompletions)),
                    "shared model works after identity restore",
                    accessContext: null,
                    cancellationToken);
                builder.Expect("source-restored-available", source.Status == "Available");
                builder.Expect("source-restored-central-uri", SameAuthority(source.BaseUri, options.CentralBaseUri));
                builder.Expect("identity-restored-original", source.RemoteInstanceId ==
                    baseline.ClientA.Sources.Single().RemoteInstanceId);
                builder.Expect("imports-preserved-after-restore", SameImportIdentities(baseline.ClientA, current) &&
                    current.Imports.All(item => item.AvailabilityState == "Available"));
                builder.Expect("identity-restored-shared-inference-succeeds", invocation.Succeeded);
            },
            cancellationToken);
        return evidence;
    }

    private async Task<IReadOnlyList<E2eScenarioStageEvidence>> RunOutageAsync(
        CancellationToken cancellationToken)
    {
        var evidence = new List<E2eScenarioStageEvidence>();
        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.CentralOutageRecoveryNoFallback,
            async builder =>
            {
                var baseline = await data.ReadBaselineAsync(cancellationToken);
                var centralUnavailable = await IsUnavailableAsync(options.CentralBaseUri, cancellationToken);
                using var clientAHealth = await http.GetAsync(
                    options.ClientABaseUri,
                    "/health",
                    bearerToken: null,
                    accessContext: null,
                    cancellationToken);
                using var clientBHealth = await http.GetAsync(
                    options.ClientBBaseUri,
                    "/health",
                    bearerToken: null,
                    accessContext: null,
                    cancellationToken);
                var clientA = await data.ReadSnapshotAsync(E2eRole.ClientA, cancellationToken);
                var clientB = await data.ReadSnapshotAsync(E2eRole.ClientB, cancellationToken);
                var personalToken = await E2eSecretFile.ReadRequiredAsync(
                    options.PersonalUpstreamControlTokenFilePath,
                    "personal upstream control token",
                    cancellationToken);
                await ResetCapturesAsync(
                    options.PersonalUpstreamControlBaseUri,
                    personalToken,
                    cancellationToken);
                var importedChat = FindImport(
                    clientA,
                    FindFixture(baseline.Central, E2eFixtures.ChatCompletions));
                var invocation = await InvokeClientProviderAsync(
                    options.ClientABaseUri,
                    await ReadCredentialAsync(E2eFixtures.ClientAAccessCredentialFileName, cancellationToken),
                    importedChat,
                    "shared model must not run during central outage",
                    accessContext: null,
                    cancellationToken);
                var personalCaptures = await GetCapturesAsync(
                    options.PersonalUpstreamControlBaseUri,
                    personalToken,
                    cancellationToken);
                builder.Expect("central-is-unavailable", centralUnavailable);
                builder.Expect("clients-remain-healthy", clientAHealth.StatusCode == HttpStatusCode.OK &&
                    clientBHealth.StatusCode == HttpStatusCode.OK);
                builder.Expect("outage-is-explicit", clientA.Sources.Single().Status == "SourceOffline" &&
                    clientB.Sources.Single().Status == "SourceOffline" &&
                    clientA.Imports.All(item => item.AvailabilityState == "SourceOffline") &&
                    clientB.Imports.All(item => item.AvailabilityState == "SourceOffline"));
                builder.Expect("outage-does-not-delete-imports", SameImportIdentities(baseline.ClientA, clientA) &&
                    SameImportIdentities(baseline.ClientB, clientB));
                builder.Expect("outage-shared-inference-rejected", IsTypedProviderUnavailable(
                    invocation,
                    importedChat.ProviderProfileId));
                builder.Expect("no-personal-provider-fallback", personalCaptures.Count == 0);
            },
            cancellationToken);
        return evidence;
    }

    private async Task<IReadOnlyList<E2eScenarioStageEvidence>> RunRecoveryAsync(
        CancellationToken cancellationToken)
    {
        var evidence = new List<E2eScenarioStageEvidence>();
        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.CentralOutageRecoveryNoFallback,
            async builder =>
            {
                var baseline = await data.ReadBaselineAsync(cancellationToken);
                var clientA = await data.ReadSnapshotAsync(E2eRole.ClientA, cancellationToken);
                var clientB = await data.ReadSnapshotAsync(E2eRole.ClientB, cancellationToken);
                var catalog = await GetCatalogAsync(
                    await ReadCredentialAsync(E2eFixtures.CentralAccessCredentialFileName, cancellationToken),
                    cancellationToken);
                var invocation = await InvokeClientProviderAsync(
                    options.ClientABaseUri,
                    await ReadCredentialAsync(E2eFixtures.ClientAAccessCredentialFileName, cancellationToken),
                    FindImport(clientA, FindFixture(baseline.Central, E2eFixtures.ChatCompletions)),
                    "shared model works after outage recovery",
                    accessContext: null,
                    cancellationToken);
                builder.Expect("central-catalog-recovers", catalog.Document.Providers.Count == 5);
                builder.Expect("sources-recover", clientA.Sources.Single().Status == "Available" &&
                    clientB.Sources.Single().Status == "Available");
                builder.Expect("imports-recover", clientA.Imports.All(item => item.AvailabilityState == "Available") &&
                    clientB.Imports.All(item => item.AvailabilityState == "Available"));
                builder.Expect("recovery-preserves-local-ids", SameImportIdentities(baseline.ClientA, clientA) &&
                    SameImportIdentities(baseline.ClientB, clientB));
                builder.Expect("recovered-shared-inference-succeeds", invocation.Succeeded);
            },
            cancellationToken);

        await ProbeAsync(
            evidence,
            BackendCheckpointScenarioCatalog.SecretContentAuditRedaction,
            async builder =>
            {
                var sensitiveValues = await data.ReadKnownSensitiveValuesAsync(cancellationToken);
                var audit = await data.ObserveAuditAsync(
                    AccessContextCanary,
                    ContentCanary,
                    sensitiveValues,
                    expectedTraceId: null,
                    cancellationToken: cancellationToken);
                var logs = await data.ObserveLogsAsync(
                    ContentCanary,
                    sensitiveValues,
                    cancellationToken);
                var upstreamControlToken = await E2eSecretFile.ReadRequiredAsync(
                    options.UpstreamControlTokenFilePath,
                    "upstream control token",
                    cancellationToken);
                var captures = await GetCapturesAsync(
                    options.UpstreamControlBaseUri,
                    upstreamControlToken,
                    cancellationToken);
                builder.Expect("audit-records-present", audit.InvocationCount > 0);
                builder.Expect("audit-content-free", audit.ContentAbsent);
                builder.Expect("audit-secret-free", audit.SecretsAbsent);
                builder.Expect("audit-completion-truthful", audit.AllInvocationsCompleted && audit.UsageTruthful);
                builder.Expect("required-log-sources-collected", logs.SourcesComplete);
                builder.Expect("logs-content-and-known-secret-free", logs.KnownValuesAbsent);
                builder.Expect("host-all-runtime-secret-scan-clean", logs.HostSecretScanComplete);
                builder.Expect("host-database-dump-scan-clean", logs.HostDatabaseScanComplete);
                builder.Expect("capture-auth-values-not-stored", captures.Requests.All(item =>
                    !item.Headers.SafeValues.ContainsKey("Authorization") &&
                    item.Headers.AuthorizationScheme is null or "Bearer"));
                await ResetCapturesAsync(options.UpstreamControlBaseUri, upstreamControlToken, cancellationToken);
            },
            cancellationToken);
        return evidence;
    }

    private async Task ProbeAsync(
        ICollection<E2eScenarioStageEvidence> evidence,
        string scenarioId,
        Func<E2eScenarioEvidenceBuilder, Task> probe,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var builder = new E2eScenarioEvidenceBuilder();
        try
        {
            await probe(builder);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            builder.Expect("unexpected-failure", condition: false);
        }

        evidence.Add(new E2eScenarioStageEvidence(
            scenarioId,
            started,
            DateTimeOffset.UtcNow,
            builder.Build()));
    }

    private async Task<E2eCatalogObservation> GetCatalogAsync(
        string token,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            options.CentralBaseUri,
            SharedProviderRoutes.Catalog,
            token,
            accessContext: null,
            cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new E2eSafeException("The central shared-provider catalog was unavailable.");
        }

        var rawJson = await http.ReadBoundedStringAsync(response, cancellationToken);
        var document = SharedProviderProtocolJson.DeserializeCatalog(rawJson);
        var entityTag = response.Headers.ETag?.ToString();
        if (string.IsNullOrWhiteSpace(entityTag))
        {
            throw new E2eSafeException("The central shared-provider catalog did not return an entity tag.");
        }

        return new E2eCatalogObservation(document, rawJson, entityTag);
    }

    private async Task<E2eClientProviderInvocation> InvokeClientProviderAsync(
        Uri clientBaseUri,
        string clientToken,
        E2eImportState imported,
        string prompt,
        string? accessContext,
        CancellationToken cancellationToken,
        E2eTraceContext? traceContext = null)
    {
        using var response = await http.PostAppJsonAsync(
            clientBaseUri,
            $"/api/agents/providers/{imported.ProviderProfileId:D}/test-chat",
            clientToken,
            accessContext,
            new ProviderTestChatRequest(
                Model: imported.RemoteDefaultModelId,
                SystemPrompt: string.Empty,
                Messages: [],
                Prompt: prompt),
            cancellationToken,
            traceContext);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new E2eClientProviderInvocation(
                StatusCode: response.StatusCode,
                Succeeded: false,
                FailureCode: await ReadApiErrorCodeAsync(response, cancellationToken),
                ProviderProfileId: imported.ProviderProfileId);
        }

        try
        {
            var result = await http.ReadAppJsonAsync<ProviderTestChatResult>(
                response,
                cancellationToken);
            return new E2eClientProviderInvocation(
                StatusCode: response.StatusCode,
                Succeeded: !string.IsNullOrWhiteSpace(result.ResponseText) &&
                    !string.IsNullOrWhiteSpace(result.Model) &&
                    result.InputTokens > 0 &&
                    result.OutputTokens > 0,
                FailureCode: null,
                ProviderProfileId: imported.ProviderProfileId);
        }
        catch (E2eSafeException)
        {
            return new E2eClientProviderInvocation(
                StatusCode: response.StatusCode,
                Succeeded: false,
                FailureCode: null,
                ProviderProfileId: imported.ProviderProfileId);
        }
    }

    private async Task<string?> ReadApiErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await http.ReadAppJsonAsync<E2eApiErrorResponse>(
                response,
                cancellationToken);
            return error.Errors is [var item]
                ? item.Code
                : null;
        }
        catch (E2eSafeException)
        {
            return null;
        }
    }

    private static bool IsTypedProviderUnavailable(
        E2eClientProviderInvocation invocation,
        Guid expectedProviderProfileId)
        => invocation.ProviderProfileId == expectedProviderProfileId &&
            !invocation.Succeeded &&
            invocation.StatusCode == HttpStatusCode.ServiceUnavailable &&
            string.Equals(
                invocation.FailureCode,
                LlmChatErrorCodes.ProviderUnavailable,
                StringComparison.Ordinal);

    private static bool HasStructuredFixturePayload(string responseJson)
    {
        try
        {
            using var response = JsonDocument.Parse(responseJson);
            if (response.RootElement.ValueKind != JsonValueKind.Object ||
                !response.RootElement.TryGetProperty("output", out var output) ||
                output.ValueKind != JsonValueKind.Array ||
                output.GetArrayLength() == 0)
            {
                return false;
            }

            var outputItem = output[0];
            if (outputItem.ValueKind != JsonValueKind.Object ||
                !outputItem.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array ||
                content.GetArrayLength() == 0)
            {
                return false;
            }

            var contentItem = content[0];
            if (contentItem.ValueKind != JsonValueKind.Object ||
                !contentItem.TryGetProperty("text", out var textElement) ||
                textElement.ValueKind != JsonValueKind.String ||
                textElement.GetString() is not { Length: > 0 } text)
            {
                return false;
            }

            using var payload = JsonDocument.Parse(text);
            return payload.RootElement.ValueKind == JsonValueKind.Object &&
                payload.RootElement.TryGetProperty("result", out var result) &&
                result.ValueKind == JsonValueKind.String &&
                string.Equals(result.GetString(), "fixture", StringComparison.Ordinal) &&
                payload.RootElement.TryGetProperty("value", out var valueElement) &&
                valueElement.TryGetInt32(out var value) &&
                value == 42;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadSingleSafeHeader(
        E2eCapturedRequest? request,
        string headerName)
    {
        var values = request?.Headers.SafeValues
            .FirstOrDefault(item => string.Equals(
                item.Key,
                headerName,
                StringComparison.OrdinalIgnoreCase))
            .Value;
        return values?.Count == 1
            ? values[0]
            : null;
    }

    private async Task<E2eControlledFailureStatuses> ObserveControlledFailuresAsync(
        string model,
        string centralToken,
        string controlToken,
        CancellationToken cancellationToken)
    {
        async Task<HttpStatusCode> InvokeAsync(E2eFixtureFailureMode failureMode)
        {
            await SetControlAsync(
                options.UpstreamControlBaseUri,
                controlToken,
                failureMode,
                E2eFixtureSurface.ChatCompletions,
                cancellationToken);
            using var response = await http.PostJsonAsync(
                options.CentralBaseUri,
                SharedProviderRoutes.ChatCompletions,
                centralToken,
                accessContext: null,
                CreateChat(model),
                cancellationToken);
            return response.StatusCode;
        }

        try
        {
            var badRequest = await InvokeAsync(E2eFixtureFailureMode.BadRequest);
            var unauthorized = await InvokeAsync(E2eFixtureFailureMode.Unauthorized);
            var rateLimited = await InvokeAsync(E2eFixtureFailureMode.RateLimited);
            var internalServerError = await InvokeAsync(E2eFixtureFailureMode.InternalServerError);
            var timeoutStarted = Stopwatch.GetTimestamp();
            var timeout = await InvokeAsync(E2eFixtureFailureMode.Timeout);
            return new E2eControlledFailureStatuses(
                badRequest,
                unauthorized,
                rateLimited,
                internalServerError,
                timeout,
                Stopwatch.GetElapsedTime(timeoutStarted));
        }
        finally
        {
            await SetControlAsync(
                options.UpstreamControlBaseUri,
                controlToken,
                E2eFixtureFailureMode.None,
                E2eFixtureSurface.All,
                cancellationToken);
        }
    }

    private async Task SetControlAsync(
        Uri baseUri,
        string controlToken,
        E2eFixtureFailureMode failureMode,
        E2eFixtureSurface surface,
        CancellationToken cancellationToken,
        E2eFixtureStreamMode streamMode = E2eFixtureStreamMode.Complete)
    {
        using var response = await http.SendControlAsync(
            baseUri,
            HttpMethod.Put,
            "/_test/control",
            controlToken,
            new E2eFixtureControlRequest(failureMode, surface, streamMode),
            cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new E2eSafeException("The deterministic upstream control operation failed.");
        }
    }

    private async Task ResetCapturesAsync(
        Uri baseUri,
        string controlToken,
        CancellationToken cancellationToken)
    {
        using var response = await http.SendControlAsync<object>(
            baseUri,
            HttpMethod.Delete,
            "/_test/captures",
            controlToken,
            request: null,
            cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new E2eSafeException("The deterministic upstream capture reset failed.");
        }
    }

    private async Task<E2eCaptureSnapshot> GetCapturesAsync(
        Uri baseUri,
        string controlToken,
        CancellationToken cancellationToken)
    {
        using var response = await http.SendControlAsync<object>(
            baseUri,
            HttpMethod.Get,
            "/_test/captures",
            controlToken,
            request: null,
            cancellationToken);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new E2eSafeException("The deterministic upstream capture query failed.");
        }

        return await http.ReadJsonAsync<E2eCaptureSnapshot>(response, cancellationToken);
    }

    private async Task<bool> WaitForUpstreamCancellationAsync(
        Uri baseUri,
        string controlToken,
        string path,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            while (true)
            {
                var captures = await GetCapturesAsync(baseUri, controlToken, timeout.Token);
                if (captures.Requests.Any(item =>
                        item.Path == path && item.CancellationObserved))
                {
                    return true;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<bool> HasImageSignatureAsync(
        HttpResponseMessage response,
        E2eImageFormat expectedFormat,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return false;
        }

        var json = await http.ReadBoundedStringAsync(response, cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(json);
            var encoded = document.RootElement
                .GetProperty("data")[0]
                .GetProperty("b64_json")
                .GetString();
            var buffer = new byte[1024 * 1024];
            if (string.IsNullOrWhiteSpace(encoded) ||
                !Convert.TryFromBase64String(encoded, buffer, out var written))
            {
                return false;
            }

            var bytes = buffer.AsSpan(0, written);
            return expectedFormat switch
            {
                E2eImageFormat.Png => bytes.StartsWith(
                    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                E2eImageFormat.WebP => bytes.Length >= 12 &&
                    bytes[..4].SequenceEqual("RIFF"u8) &&
                    bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
                _ => false
            };
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<bool> IsUnavailableAsync(Uri baseUri, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await http.GetAsync(
                baseUri,
                "/health",
                bearerToken: null,
                accessContext: null,
                timeout.Token);
            return false;
        }
        catch (HttpRequestException)
        {
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return true;
        }
    }

    private Task<string> ReadCredentialAsync(
        string fileName,
        CancellationToken cancellationToken)
        => E2eSecretFile.ReadRequiredAsync(
            Path.Combine(options.ArtifactRootPath, "credentials", fileName),
            "generated access token",
            cancellationToken);

    private static E2eFixtureIdentity FindFixture(E2eStateSnapshot snapshot, string fixtureId)
        => snapshot.Fixtures.Single(item =>
            string.Equals(item.FixtureId, fixtureId, StringComparison.Ordinal));

    private static E2eImportState FindImport(
        E2eStateSnapshot snapshot,
        E2eFixtureIdentity fixture)
        => snapshot.Imports.Single(item => item.RemotePublicationId == fixture.PublicationId);

    private static SharedProviderRoutingModelId FindModel(
        SharedProviderCatalogDocument catalog,
        E2eStateSnapshot central,
        string fixtureId)
    {
        var fixture = FindFixture(central, fixtureId);
        var publication = catalog.Providers.Single(item =>
            item.PublicationId.Value == fixture.PublicationId);
        return publication.DefaultModelId;
    }

    private static IReadOnlyList<SharedProviderCapability> FindCapabilities(
        SharedProviderCatalogDocument catalog,
        E2eStateSnapshot central,
        string fixtureId)
    {
        var fixture = FindFixture(central, fixtureId);
        var publication = catalog.Providers.Single(item =>
            item.PublicationId.Value == fixture.PublicationId);
        return publication.Models.Single(item => item.Id == publication.DefaultModelId).Capabilities;
    }

    private static string FindProviderDefaultModel(E2eStateSnapshot snapshot, string fixtureId)
    {
        var fixture = FindFixture(snapshot, fixtureId);
        return snapshot.Providers.Single(provider => provider.Id == fixture.ProviderProfileId).DefaultModel;
    }

    private static bool IsCatalogSanitized(
        string rawJson,
        E2eStateSnapshot central,
        IReadOnlyCollection<string> sensitiveValues)
    {
        string[] forbiddenTokens =
        [
            "baseUrl",
            "apiKey",
            "secret",
            "providerProfileId",
            "connectorPluginKey",
            "configurationJson",
            "e2e-duplicate-model"
        ];
        return forbiddenTokens.All(token =>
                !rawJson.Contains(token, StringComparison.OrdinalIgnoreCase)) &&
            !rawJson.Contains(ContentCanary, StringComparison.Ordinal) &&
            sensitiveValues.All(value =>
                !string.IsNullOrEmpty(value) &&
                !rawJson.Contains(value, StringComparison.Ordinal)) &&
            central.Providers.All(provider =>
                !rawJson.Contains(provider.Id.ToString("D"), StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasUniqueImportIdentities(E2eStateSnapshot snapshot)
        => snapshot.Imports.Select(item => item.Id).Distinct().Count() == snapshot.Imports.Count &&
            snapshot.Imports.Select(item => item.ProviderProfileId).Distinct().Count() == snapshot.Imports.Count &&
            snapshot.Imports.Select(item => item.RemotePublicationId).Distinct().Count() == snapshot.Imports.Count;

    private static bool SameImportIdentities(E2eStateSnapshot expected, E2eStateSnapshot actual)
        => expected.Imports
            .OrderBy(item => item.RemotePublicationId)
            .Select(item => (item.RemotePublicationId, item.Id, item.ProviderProfileId))
            .SequenceEqual(actual.Imports
                .OrderBy(item => item.RemotePublicationId)
                .Select(item => (item.RemotePublicationId, item.Id, item.ProviderProfileId)));

    private static bool SameAuthority(string sourceBaseUri, Uri expected)
        => Uri.TryCreate(sourceBaseUri, UriKind.Absolute, out var actual) &&
            string.Equals(actual.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actual.Host, expected.Host, StringComparison.OrdinalIgnoreCase) &&
            actual.Port == expected.Port;

    private static bool IsJson(string? contentType)
        => string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase);

    private static bool IsIncremental(E2eSseObservation observation)
        => observation.StatusCode == HttpStatusCode.OK &&
            observation.FirstDataAt is { } first &&
            first < observation.CompletedAt &&
            observation.CompletedAt - first >= TimeSpan.FromMilliseconds(50);

    private static E2eChatRequest CreateChat(
        string model,
        string content = "deterministic checkpoint",
        bool stream = false)
        => new(
            model,
            [new E2eChatMessage("user", content)],
            stream ? true : null,
            Tools: null,
            ToolChoice: null,
            ResponseFormat: null);

    private static E2eResponsesRequest CreateResponses(string model, bool stream = false)
        => new(
            model,
            "deterministic checkpoint",
            stream ? true : null,
            Tools: null,
            ToolChoice: null,
            Text: null);

    private static E2eChatRequest CreateToolChat(string model)
    {
        var parameters = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                city = new
                {
                    type = "string"
                }
            },
            required = new[] { "city" }
        });
        return new E2eChatRequest(
            model,
            [new E2eChatMessage("user", "deterministic tool checkpoint")],
            Stream: null,
            [new E2eChatTool("function", new E2eChatFunction("weather", parameters))],
            new E2eChatToolChoice("function", new E2eChatToolChoiceFunction("weather")),
            ResponseFormat: null);
    }

    private static E2eResponsesRequest CreateStructuredResponses(string model)
    {
        var schema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                result = new
                {
                    type = "string"
                },
                value = new
                {
                    type = "integer"
                }
            },
            required = new[] { "result", "value" },
            additionalProperties = false
        });
        return new E2eResponsesRequest(
            model,
            "deterministic structured checkpoint",
            Stream: null,
            Tools: null,
            ToolChoice: null,
            new E2eResponsesText(new E2eResponsesFormat(
                "json_schema",
                "checkpoint_result",
                schema,
                Strict: true)));
    }

    private static E2eChatRequest CreateStructuredChat(string model)
        => new(
            model,
            [new E2eChatMessage("user", "deterministic structured rejection")],
            Stream: null,
            Tools: null,
            ToolChoice: null,
            new E2eChatResponseFormat("json_object"));

    private static E2eImageRequest CreateImage(string model, string outputFormat)
        => new(
            model,
            "deterministic image checkpoint",
            1,
            "256x256",
            "b64_json",
            outputFormat);
}

internal sealed record E2eCatalogObservation(
    SharedProviderCatalogDocument Document,
    string RawJson,
    string EntityTag);

internal enum E2eImageFormat
{
    Png,
    WebP
}

internal sealed record E2eApiErrorResponse(IReadOnlyList<E2eApiErrorItem> Errors);

internal sealed record E2eApiErrorItem(string Code);

internal sealed record E2eClientProviderInvocation(
    HttpStatusCode StatusCode,
    bool Succeeded,
    string? FailureCode,
    Guid ProviderProfileId);

internal sealed record E2eControlledFailureStatuses(
    HttpStatusCode BadRequest,
    HttpStatusCode Unauthorized,
    HttpStatusCode RateLimited,
    HttpStatusCode InternalServerError,
    HttpStatusCode Timeout,
    TimeSpan TimeoutElapsed);

internal sealed record E2eChatMessage(string Role, string Content);

internal sealed record E2eChatFunction(
    string Name,
    JsonElement Parameters);

internal sealed record E2eChatTool(
    string Type,
    E2eChatFunction Function);

internal sealed record E2eChatToolChoiceFunction(string Name);

internal sealed record E2eChatToolChoice(
    string Type,
    E2eChatToolChoiceFunction Function);

internal sealed record E2eChatResponseFormat(string Type);

internal sealed record E2eChatRequest(
    string Model,
    IReadOnlyList<E2eChatMessage> Messages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Stream,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<E2eChatTool>? Tools,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] E2eChatToolChoice? ToolChoice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] E2eChatResponseFormat? ResponseFormat);

internal sealed record E2eResponsesFormat(
    string Type,
    string Name,
    JsonElement Schema,
    bool Strict);

internal sealed record E2eResponsesText(E2eResponsesFormat Format);

internal sealed record E2eResponsesRequest(
    string Model,
    string Input,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Stream,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<object>? Tools,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? ToolChoice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] E2eResponsesText? Text);

internal sealed record E2eImageRequest(
    string Model,
    string Prompt,
    int N,
    string Size,
    string ResponseFormat,
    string OutputFormat);
