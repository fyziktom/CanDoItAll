using System.ComponentModel;
using System.Net.Http.Headers;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class AgentVoiceApi {
    internal const long MaximumAudioBytes = 25L * 1024 * 1024;
    internal const int MaximumAudioChunks = 12;
    internal const int MaximumTextCharacters = 4096;
    private const long MaximumAudioRequestBytes = MaximumAudioBytes + 64 * 1024;
    private const string FileField = "file";
    private const string ChunksField = "chunks";
    private const string InputInvalidCode = "agents.voice-input-invalid";

    public static RouteGroupBuilder MapAgentVoiceApi(this RouteGroupBuilder group) {
        var voice = group.MapGroup("/agents/voice")
            .WithApiSection(ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents)
            .WithTags("Agents")
            .DisableAntiforgery();

        voice.MapPost("/transcriptions", TranscribeAsync)
            .WithName("TranscribeAgentVoice")
            .WithApiPermission(ApiAccessScopeNames.ExecuteAgents)
            .Accepts<AgentVoiceTranscriptionApiRequest>("multipart/form-data")
            .WithMetadata(new RequestSizeLimitAttribute(MaximumAudioRequestBytes), new RequestFormLimitsAttribute {
                MultipartBodyLengthLimit = MaximumAudioBytes,
                ValueCountLimit = MaximumAudioChunks
            })
            .Produces<AgentVoiceTranscriptionApiResponse>()
            .AddOpenApiOperationTransformer(async (operation, context, cancellationToken) => {
                operation.RequestBody!.Content!["multipart/form-data"].Schema = await context.GetOrCreateSchemaAsync(
                    typeof(AgentVoiceTranscriptionApiRequest), parameterDescription: null, cancellationToken);
            })
            .ProducesApiErrors(400, 401, 403, 409, 413, 415, 503)
            .AddOpenApiOperationTransformer(DescribeVoiceErrorsAsync)
            .DescribeApi("Transcribe audio using workspace voice settings.",
                "Requires api.agents.execute. Send one file part named file, or 1–12 independently decodable files " +
                "named chunks in transcription order; never mix both. Accepted extensions are .mp3, .mp4, .mpeg, " +
                ".mpga, .m4a, .wav and .webm with a matching media type. Each file must be nonempty; the combined " +
                "audio limit is 25 MiB (26214400 bytes), and the whole request limit adds 64 KiB of multipart overhead. " +
                "No files are stored by this route. Chunks are sent sequentially to the configured native OpenAI Chat " +
                "profile and transcripts are joined with line breaks. Request cancellation cancels remaining work. " +
                "A later chunk failure returns no partial transcript; earlier provider calls may have incurred cost. " +
                "409 agents.voice-disabled or agents.voice-capability-unavailable means configuration blocks voice; " +
                "503 agents.voice-provider-unavailable means a provider is missing, disabled or failed. " +
                "Invalid input returns 400 agents.voice-input-invalid. Framework body-size/content-type rejection " +
                "may return 400, 413 or 415 without an error envelope. There are no automatic retries or model substitutions.",
                "The trimmed transcript and configured transcription model.",
                "Exactly one file or ordered chunks; other form fields are rejected.");

        voice.MapPost("/speech", SynthesizeAsync)
            .WithName("SynthesizeAgentVoice")
            .WithApiPermission(ApiAccessScopeNames.ExecuteAgents)
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024))
            .Produces(StatusCodes.Status200OK, typeof(byte[]), "audio/mpeg", "audio/wav", "audio/ogg", "audio/aac", "audio/flac")
            .AddOpenApiOperationTransformer((operation, _, _) => {
                foreach (var media in operation.Responses!["200"].Content!.Values) {
                    media.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary", Description = "Synthesized audio bytes." };
                }
                return Task.CompletedTask;
            })
            .ProducesApiErrors(400, 401, 403, 404, 409, 413, 415, 503)
            .AddOpenApiOperationTransformer(DescribeVoiceErrorsAsync)
            .DescribeApi("Synthesize speech using workspace voice settings.",
                "Requires api.agents.execute. text is required, nonblank and at most 4096 UTF-16 characters. " +
                "Optional agentId selects an existing non-template agent's voice permission/preferred voice; " +
                "it does not execute the agent or read its conversations. An unknown agent returns 404 " +
                "agents.voice-agent-not-found; an agent without voice permission returns 403 agents.voice-access-denied. " +
                "Without agentId this is workspace-level speech. Optional voiceId selects one of the built-in OpenAI " +
                "voices; otherwise the agent preference or workspace default applies. The existing speech preprocessor " +
                "may omit technical identifiers and add its omission notice. The workspace controls model and audio format; " +
                "PCM is wrapped as WAV. No audio is stored by this route. Responses are not cacheable. Request cancellation " +
                "cancels synthesis. 409 agents.voice-disabled or agents.voice-capability-unavailable means configuration " +
                "blocks voice; 503 agents.voice-provider-unavailable means a provider is missing, disabled or failed. " +
                "Invalid input returns 400 agents.voice-input-invalid. Framework size/content-type rejection may return " +
                "400, 413 or 415 without an error envelope. There are no automatic retries or model substitutions.",
                "Audio bytes with their playback content type; no JSON or base64 wrapper.",
                "JSON text, optional agentId and optional voiceId.");
        return group;
    }

    private static async Task<IResult> TranscribeAsync(
        [FromForm] IFormCollection form,
        HttpContext context,
        IAgentVoiceService voiceService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        try {
            var chunks = await ReadAudioAsync(form, cancellationToken);
            var result = await voiceService.TranscribeAsync(new AgentVoiceTranscriptionRequest([], string.Empty, string.Empty) {
                AudioChunks = chunks
            }, cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new AgentVoiceTranscriptionApiResponse(result.Text, result.Model));
        } catch (Exception exception) when (IsVoiceFailure(exception, cancellationToken)) {
            return VoiceFailure(context, exception, loggerFactory, AgentProviderOperationKind.TranscribeSpeech);
        }
    }

    private static async Task<IResult> SynthesizeAsync(
        AgentVoiceSpeechApiRequest request,
        HttpContext context,
        IAgentVoiceService voiceService,
        IAgentFrameworkWorkspaceService workspaceService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > MaximumTextCharacters ||
            request.AgentId == Guid.Empty ||
            request.VoiceId is not null && !AgentVoiceDefaults.OpenAiVoiceIds.Contains(request.VoiceId, StringComparer.Ordinal)) {
            return ApiEndpointResults.BadRequest("Supply nonblank text up to 4096 characters, a valid agent ID and a supported voice ID.", InputInvalidCode);
        }

        try {
            AgentVoiceAccessSettings? access = null;
            if (request.AgentId is { } agentId) {
                var agent = (await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken))
                    .FirstOrDefault(candidate => candidate.Id == agentId);
                if (agent is null) {
                    return ApiEndpointResults.NotFound("The requested agent was not found.", "agents.voice-agent-not-found");
                }
                access = AgentVoiceAccessMetadata.Read(agent.ConfigurationJson);
            }

            var result = await voiceService.SynthesizeAsync(new AgentVoiceSynthesisRequest(
                request.Text.Trim(), access, request.VoiceId), cancellationToken);
            if (result.AudioBytes.Length == 0) {
                throw new AgentVoiceException(AgentVoiceFailureKind.ProviderUnavailable, "The provider returned empty audio.");
            }
            context.Response.Headers.CacheControl = "no-store";
            return Results.Bytes(result.AudioBytes, result.ContentType);
        } catch (Exception exception) when (IsVoiceFailure(exception, cancellationToken)) {
            return VoiceFailure(context, exception, loggerFactory, AgentProviderOperationKind.SynthesizeSpeech, request.AgentId);
        }
    }

    private static async Task<IReadOnlyList<AgentVoiceAudioChunk>> ReadAudioAsync(
        IFormCollection form, CancellationToken cancellationToken) {
        var files = form.Files;
        if (files.Count is < 1 or > MaximumAudioChunks || form.Count != 0 ||
            files.Any(part => !part.Name.Equals(FileField, StringComparison.OrdinalIgnoreCase) &&
                              !part.Name.Equals(ChunksField, StringComparison.OrdinalIgnoreCase)) ||
            files.Count != 1 && files.Any(part => part.Name.Equals(FileField, StringComparison.OrdinalIgnoreCase))) {
            throw new AgentVoiceException(AgentVoiceFailureKind.InvalidInput, "Supply exactly one audio file or 1–12 ordered chunks.");
        }

        if (files.Any(part => part.Length is <= 0 or > MaximumAudioBytes) || files.Sum(part => part.Length) > MaximumAudioBytes) {
            throw new AgentVoiceException(AgentVoiceFailureKind.InvalidInput, "Audio must be nonempty and total at most 25 MiB.");
        }
        var chunks = new List<AgentVoiceAudioChunk>(files.Count);
        foreach (var part in files) {
            var extension = Path.GetExtension(part.FileName).ToLowerInvariant();
            var mediaType = MediaTypeHeaderValue.TryParse(part.ContentType, out var parsed) ? parsed.MediaType?.ToLowerInvariant() : null;
            var valid = extension switch {
                ".mp3" or ".mpga" => mediaType is "audio/mpeg" or "audio/mp3",
                ".mpeg" => mediaType is "audio/mpeg" or "video/mpeg",
                ".mp4" => mediaType is "audio/mp4" or "video/mp4",
                ".m4a" => mediaType is "audio/mp4" or "audio/x-m4a",
                ".wav" => mediaType is "audio/wav" or "audio/x-wav" or "audio/wave",
                ".webm" => mediaType is "audio/webm" or "video/webm",
                _ => false
            };
            if (!valid) {
                throw new AgentVoiceException(AgentVoiceFailureKind.InvalidInput, "Audio extension and content type must identify the same supported format.");
            }
            await using var stream = part.OpenReadStream();
            var bytes = new byte[checked((int)part.Length)];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            chunks.Add(new AgentVoiceAudioChunk(bytes, $"voice-{chunks.Count + 1}{extension}", mediaType!));
        }
        return chunks;
    }

    private static Task DescribeVoiceErrorsAsync(OpenApiOperation operation,
        OpenApiOperationTransformerContext _, CancellationToken cancellationToken) {
        foreach (var (status, response) in operation.Responses ?? []) {
            response.Description = status switch {
                "404" => "The requested non-template agent does not exist.",
                "409" => "Workspace voice is disabled or the configured provider/driver cannot provide native voice; inspect the safe error code.",
                "413" => "The request body exceeds the documented byte limit; no provider call was made.",
                "415" => "The request content type does not match the route's required multipart or JSON body.",
                _ => response.Description
            };
        }
        return Task.CompletedTask;
    }

    private static bool IsVoiceFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is InvalidOperationException or HttpRequestException or TimeoutException or IOException ||
        exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private static IResult VoiceFailure(HttpContext context, Exception exception, ILoggerFactory loggerFactory,
        AgentProviderOperationKind operation, Guid? agentId = null) {
        var kind = exception switch {
            AgentVoiceException voice => voice.Kind,
            ProviderAudioCapabilityException or UnsupportedProviderCapabilityException => AgentVoiceFailureKind.CapabilityUnavailable,
            EndOfStreamException => AgentVoiceFailureKind.InvalidInput,
            _ => AgentVoiceFailureKind.ProviderUnavailable
        };
        var (status, code, message) = kind switch {
            AgentVoiceFailureKind.InvalidInput => (400, InputInvalidCode, "Voice input is invalid. Check the documented input limits and formats."),
            AgentVoiceFailureKind.Disabled => (409, "agents.voice-disabled", "This voice operation is disabled in workspace settings."),
            AgentVoiceFailureKind.AccessDenied => (403, "agents.voice-access-denied", "The selected agent does not allow voice mode."),
            AgentVoiceFailureKind.CapabilityUnavailable => (409, "agents.voice-capability-unavailable", "Voice requires a supported native OpenAI Chat provider and driver."),
            _ => (503, "agents.voice-provider-unavailable", "The configured voice provider is unavailable or failed. Check provider settings and health before retrying.")
        };
        loggerFactory.CreateLogger(typeof(AgentVoiceApi).FullName!).LogWarning(
            "Voice operation failed. Operation={Operation} Code={Code} AgentId={AgentId} TraceId={TraceId} FailureType={FailureType}",
            operation, code, agentId, context.TraceIdentifier, exception.GetType().Name);
        return ApiEndpointResults.AgentFailure(context, status, message, code, agentId);
    }
}

[Description("Multipart audio input: send one file or ordered chunks, never both. The combined audio limit is 25 MiB.")]
internal sealed class AgentVoiceTranscriptionApiRequest {
    [Description("One nonempty supported audio file; omit when sending chunks. Its extension and media type must match.")]
    public IFormFile? File { get; set; }
    [Description("Alternative to file: 1–12 independently decodable audio files, in transcription order; repeat the chunks field.")]
    public List<IFormFile> Chunks { get; set; } = [];
}

[Description("Speech input using the configured workspace voice model and format. No agent execution is started.")]
internal sealed record AgentVoiceSpeechApiRequest(
    [property: Description("Required nonblank text, at most 4096 UTF-16 characters; trimmed before speech preprocessing.")] string Text,
    [property: Description("Optional non-template agent identifier from the agent catalog. Applies its voice permission and preference; null uses workspace speech.")] Guid? AgentId = null,
    [property: Description("Optional built-in OpenAI voice ID: alloy, ash, ballad, coral, echo, fable, nova, onyx, sage, shimmer, verse, marin or cedar. Null uses the agent preference or workspace default.")] string? VoiceId = null);

[Description("Completed audio transcription. No transcript is returned if any chunk fails.")]
internal sealed record AgentVoiceTranscriptionApiResponse(
    [property: Description("Trimmed transcript, with ordered chunk transcripts joined by line breaks.")] string Text,
    [property: Description("The configured transcription model that processed the audio.")] string Model);
