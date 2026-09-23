namespace CanDoItAll.SharedProviders.E2E;

public static class BackendCheckpointScenarioCatalog
{
    public const string CentralCatalogPublicationBoundary =
        "central-catalog-publication-boundary";
    public const string ClientATextImportWithPersonalProvider =
        "client-a-text-import-with-personal-provider";
    public const string ClientBTextAndImageImports =
        "client-b-text-and-image-imports";
    public const string SourceResyncIdempotencyAndStableLocalIds =
        "source-resync-idempotency-and-stable-local-ids";
    public const string DuplicateUpstreamModelRouting =
        "duplicate-upstream-model-routing";
    public const string ChatCompletionsAndResponsesBuffered =
        "chat-completions-and-responses-buffered";
    public const string ChatCompletionsAndResponsesStreaming =
        "chat-completions-and-responses-streaming";
    public const string FunctionToolCallRoundtrip =
        "function-tool-call-roundtrip";
    public const string StructuredOutputCapabilityAllowDeny =
        "structured-output-capability-allow-deny";
    public const string OpenAiAndComfyUiImageGeneration =
        "openai-and-comfyui-image-generation";
    public const string CatalogEtagNotModified =
        "catalog-etag-not-modified";
    public const string CatalogAndInferenceScopeIsolation =
        "catalog-and-inference-scope-isolation";
    public const string MalformedAccessContextRejected =
        "malformed-access-context-rejected";
    public const string AccessContextCentralOnly =
        "access-context-central-only";
    public const string UnpublishAndReappearance =
        "unpublish-and-reappearance";
    public const string CentralOutageRecoveryNoFallback =
        "central-outage-recovery-no-fallback";
    public const string SourceIdentityMismatch =
        "source-identity-mismatch";
    public const string StreamingDisconnectCancellation =
        "streaming-disconnect-cancellation";
    public const string SecretContentAuditRedaction =
        "secret-content-audit-redaction";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
    [
        CentralCatalogPublicationBoundary,
        ClientATextImportWithPersonalProvider,
        ClientBTextAndImageImports,
        SourceResyncIdempotencyAndStableLocalIds,
        DuplicateUpstreamModelRouting,
        ChatCompletionsAndResponsesBuffered,
        ChatCompletionsAndResponsesStreaming,
        FunctionToolCallRoundtrip,
        StructuredOutputCapabilityAllowDeny,
        OpenAiAndComfyUiImageGeneration,
        CatalogEtagNotModified,
        CatalogAndInferenceScopeIsolation,
        MalformedAccessContextRejected,
        AccessContextCentralOnly,
        UnpublishAndReappearance,
        CentralOutageRecoveryNoFallback,
        SourceIdentityMismatch,
        StreamingDisconnectCancellation,
        SecretContentAuditRedaction
    ]);
}
