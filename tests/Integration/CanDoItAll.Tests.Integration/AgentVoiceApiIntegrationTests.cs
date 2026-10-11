using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Integration.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed partial class AgentVoiceApiIntegrationTests {
    private const string TranscriptionsPath = "/api/agents/voice/transcriptions";
    private const string SpeechPath = "/api/agents/voice/speech";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Voice_routes_enforce_execute_scope(bool transcribe) {
        var voice = new VoiceHarness();
        await using var host = await CreateHostAsync(voice, jwtEnabled: true);

        using var anonymous = await SendVoiceAsync(host.Client, transcribe);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        SetScope(host, ApiAccessScopeNames.ReadAgents);
        using var reader = await SendVoiceAsync(host.Client, transcribe);
        Assert.Equal(HttpStatusCode.Forbidden, reader.StatusCode);
        Assert.Equal(0, voice.CallCount);

        SetScope(host, ApiAccessScopeNames.ExecuteAgents);
        using var execution = await SendVoiceAsync(host.Client, transcribe);
        Assert.Equal(HttpStatusCode.OK, execution.StatusCode);
        Assert.Equal(1, voice.CallCount);
        Assert.True(execution.Headers.CacheControl?.NoStore);
        if (!transcribe) {
            Assert.Equal("audio/mpeg", execution.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Encoding.UTF8.GetBytes("Synthetic speech"), await execution.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task Transcription_preserves_chunk_order_and_replaces_uploaded_names() {
        var voice = new VoiceHarness();
        await using var host = await CreateHostAsync(voice);
        using var form = new MultipartFormDataContent();
        AddAudio(form, "chunks", "../../private-first.wav", "audio/wav", Encoding.UTF8.GetBytes("first"));
        AddAudio(form, "chunks", "second.webm", "audio/webm; codecs=opus", Encoding.UTF8.GetBytes("second"));

        using var response = await host.Client.PostAsync(TranscriptionsPath, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AgentVoiceTranscriptionResult>();
        Assert.Equal($"first{Environment.NewLine}second", result?.Text);
        Assert.Equal(AgentVoiceDefaults.OpenAiTranscriptionModel, result?.Model);
        Assert.Equal(new[] { "voice-1.wav", "voice-2.webm" }, voice.Transcriptions.Select(request => request.FileName));
        Assert.Equal("audio/webm", voice.Transcriptions[1].ContentType);
    }

    [Theory]
    [InlineData(InvalidAudio.EmptyForm)]
    [InlineData(InvalidAudio.EmptyFile)]
    [InlineData(InvalidAudio.MismatchedType)]
    [InlineData(InvalidAudio.UnsupportedExtension)]
    [InlineData(InvalidAudio.UnknownField)]
    [InlineData(InvalidAudio.ExtraText)]
    [InlineData(InvalidAudio.MixedFileAndChunks)]
    [InlineData(InvalidAudio.DuplicateFile)]
    [InlineData(InvalidAudio.TooManyChunks)]
    public async Task Transcription_rejects_invalid_or_ambiguous_forms_before_dispatch(InvalidAudio scenario) {
        var voice = new VoiceHarness();
        await using var host = await CreateHostAsync(voice);
        using var form = new MultipartFormDataContent();
        if (scenario != InvalidAudio.EmptyForm) {
            AddAudio(form, scenario == InvalidAudio.UnknownField ? "unexpected" : "file",
                scenario == InvalidAudio.UnsupportedExtension ? "voice.txt" : "voice.mp3",
                scenario == InvalidAudio.MismatchedType ? "text/plain" : "audio/mpeg",
                scenario == InvalidAudio.EmptyFile ? [] : [1]);
        }
        if (scenario == InvalidAudio.ExtraText) {
            form.Add(new StringContent("unexpected"), "prompt");
        }
        if (scenario is InvalidAudio.MixedFileAndChunks or InvalidAudio.DuplicateFile) {
            AddAudio(form, scenario == InvalidAudio.DuplicateFile ? "file" : "chunks", "second.mp3", "audio/mpeg", [2]);
        }
        if (scenario == InvalidAudio.TooManyChunks) {
            form.Dispose();
            using var chunks = new MultipartFormDataContent();
            for (var index = 0; index < 13; index++) {
                AddAudio(chunks, "chunks", $"part-{index}.mp3", "audio/mpeg", [1]);
            }
            using var tooMany = await host.Client.PostAsync(TranscriptionsPath, chunks);
            Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        } else {
            using var response = await host.Client.PostAsync(TranscriptionsPath, form);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(0, voice.CallCount);
    }

    [Theory]
    [InlineData(InvalidSpeech.Blank)]
    [InlineData(InvalidSpeech.Null)]
    [InlineData(InvalidSpeech.TooLong)]
    [InlineData(InvalidSpeech.EmptyAgentId)]
    [InlineData(InvalidSpeech.UnknownVoice)]
    public async Task Speech_rejects_invalid_input_before_dispatch(InvalidSpeech scenario) {
        var voice = new VoiceHarness();
        await using var host = await CreateHostAsync(voice);
        var text = scenario switch {
            InvalidSpeech.Blank => "  ",
            InvalidSpeech.Null => null,
            InvalidSpeech.TooLong => new string('x', 4097),
            _ => "Synthetic speech"
        };
        using var response = await host.Client.PostAsJsonAsync(SpeechPath, new {
            text,
            agentId = scenario == InvalidSpeech.EmptyAgentId ? (Guid?)Guid.Empty : null,
            voiceId = scenario == InvalidSpeech.UnknownVoice ? "unsupported-voice" : null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, voice.CallCount);
    }

    [Theory]
    [InlineData(VoiceFailure.Disabled, true, 409, "agents.voice-disabled")]
    [InlineData(VoiceFailure.Disabled, false, 409, "agents.voice-disabled")]
    [InlineData(VoiceFailure.MissingProvider, true, 503, "agents.voice-provider-unavailable")]
    [InlineData(VoiceFailure.MissingProvider, false, 503, "agents.voice-provider-unavailable")]
    [InlineData(VoiceFailure.SharedProvider, true, 409, "agents.voice-capability-unavailable")]
    [InlineData(VoiceFailure.SharedProvider, false, 409, "agents.voice-capability-unavailable")]
    [InlineData(VoiceFailure.ImageProvider, true, 409, "agents.voice-capability-unavailable")]
    [InlineData(VoiceFailure.ImageProvider, false, 409, "agents.voice-capability-unavailable")]
    [InlineData(VoiceFailure.ProviderFailure, true, 503, "agents.voice-provider-unavailable")]
    [InlineData(VoiceFailure.ProviderFailure, false, 503, "agents.voice-provider-unavailable")]
    [InlineData(VoiceFailure.EmptyResult, true, 503, "agents.voice-provider-unavailable")]
    [InlineData(VoiceFailure.EmptyResult, false, 503, "agents.voice-provider-unavailable")]
    public async Task Voice_returns_safe_actionable_errors(VoiceFailure failure, bool transcribe, int status, string code) {
        var voice = new VoiceHarness(failure);
        await using var host = await CreateHostAsync(voice);

        using var response = await SendVoiceAsync(host.Client, transcribe);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Contains(code, body, StringComparison.Ordinal);
        Assert.DoesNotContain(VoiceHarness.PrivateFailureText, body, StringComparison.Ordinal);
        if (failure is not VoiceFailure.ProviderFailure and not VoiceFailure.EmptyResult) {
            Assert.Equal(0, voice.CallCount);
        }
    }

    [Fact]
    public async Task Speech_resolves_agent_preference_enforces_permission_and_does_not_execute_agent() {
        var voice = new VoiceHarness();
        await using var host = await CreateHostAsync(voice);
        await using var scope = host.App.Services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var form = new AgentEditorModel {
            Name = "Synthetic voice permission probe",
            Instructions = "A synthetic test agent; never run it.",
            VoiceAccess = new AgentVoiceAccessSettings { CanUseVoiceMode = true, PreferredVoiceId = "cedar" }
        };
        var agentId = await workspace.SaveAgentAsync(form);
        using var preferred = await host.Client.PostAsJsonAsync(SpeechPath, new { text = "Synthetic speech", agentId });
        Assert.Equal(HttpStatusCode.OK, preferred.StatusCode);
        Assert.Equal("cedar", Assert.Single(voice.Syntheses).VoiceId);

        using var overridden = await host.Client.PostAsJsonAsync(SpeechPath, new { text = "Synthetic speech", agentId, voiceId = "marin" });
        Assert.Equal(HttpStatusCode.OK, overridden.StatusCode);
        Assert.Equal("marin", voice.Syntheses[1].VoiceId);

        form = await workspace.GetAgentEditorAsync(agentId);
        form.VoiceAccess.CanUseVoiceMode = false;
        await workspace.SaveAgentAsync(form);
        using var denied = await host.Client.PostAsJsonAsync(SpeechPath, new { text = "Synthetic speech", agentId, voiceId = "marin" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var unknown = await host.Client.PostAsJsonAsync(SpeechPath, new { text = "Synthetic speech", agentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(2, voice.CallCount);
        Assert.Empty(await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Voice_request_body_limits_apply_before_provider_dispatch(bool transcribe) {
        var voice = new VoiceHarness();
        await using var host = await CreateHostAsync(voice);
        using HttpContent content = transcribe
            ? CreateAudioForm(new byte[26 * 1024 * 1024])
            : new StringContent(new string(' ', 66 * 1024) + "{\"text\":\"Synthetic speech\"}", Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, transcribe ? TranscriptionsPath : SpeechPath) { Content = content };
        request.Headers.ExpectContinue = true;
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(0, voice.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Voice_client_cancellation_reaches_the_provider(bool transcribe) {
        var voice = new VoiceHarness { WaitForCancellation = true };
        await using var host = await CreateHostAsync(voice);
        using var cancellation = new CancellationTokenSource();
        var request = SendVoiceAsync(host.Client, transcribe, cancellation.Token);
        await voice.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);
        await voice.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, voice.CallCount);
    }

    [Fact]
    public async Task Voice_openapi_documents_multipart_json_audio_and_safe_failures() {
        await using var host = await CreateHostAsync(new VoiceHarness());
        var json = await host.Client.GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(json);
        var gaps = OpenApiDescriptionCoverage.FindGaps(JsonNode.Parse(json)!.AsObject(), "/api/agents/voice");
        Assert.True(gaps.Count == 0, string.Join(Environment.NewLine, gaps));
        var paths = document.RootElement.GetProperty("paths");
        var transcription = paths.GetProperty(TranscriptionsPath).GetProperty("post");
        var speech = paths.GetProperty(SpeechPath).GetProperty("post");
        var multipartSchema = transcription.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
        if (multipartSchema.TryGetProperty("$ref", out var reference)) {
            multipartSchema = document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1]);
        }
        var audioFields = multipartSchema.GetProperty("properties");
        Assert.True(audioFields.TryGetProperty("file", out _));
        Assert.Equal("array", audioFields.GetProperty("chunks").GetProperty("type").GetString());
        Assert.Equal("binary", audioFields.GetProperty("chunks").GetProperty("items").GetProperty("format").GetString());
        Assert.True(speech.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        foreach (var operation in new[] { transcription, speech }) {
            Assert.Contains("api.agents.execute", operation.GetProperty("description").GetString());
            Assert.True(operation.GetProperty("responses").TryGetProperty("409", out _));
            Assert.True(operation.GetProperty("responses").TryGetProperty("503", out _));
        }
        var audio = speech.GetProperty("responses").GetProperty("200").GetProperty("content");
        Assert.True(audio.TryGetProperty("audio/mpeg", out _));
        Assert.True(audio.TryGetProperty("audio/wav", out _));
        Assert.Equal("binary", audio.GetProperty("audio/mpeg").GetProperty("schema").GetProperty("format").GetString());
    }

    private static Task<ApiTestHost> CreateHostAsync(VoiceHarness voice, bool jwtEnabled = false) =>
        ApiTestHost.CreateAsync(jwtEnabled, services => services.AddSingleton<IAgentVoiceService>(
            new AgentVoiceService(voice, voice, voice, new AgentVoiceSpeechTextPreprocessor())), useInMemoryDatabase: true);

    private static void SetScope(ApiTestHost host, string scope) {
        var issued = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new ApiTokenIssueRequest {
            Subject = "synthetic-voice-http-test", Scopes = [scope]
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(issued.TokenType, issued.Token);
    }

    private static async Task<HttpResponseMessage> SendVoiceAsync(HttpClient client, bool transcribe, CancellationToken cancellationToken = default) {
        using HttpContent content = transcribe ? CreateAudioForm(Encoding.UTF8.GetBytes("Synthetic transcription"))
            : JsonContent.Create(new { text = "Synthetic speech" });
        return await client.PostAsync(transcribe ? TranscriptionsPath : SpeechPath, content, cancellationToken);
    }

    private static MultipartFormDataContent CreateAudioForm(byte[] bytes) {
        var form = new MultipartFormDataContent();
        AddAudio(form, "file", "synthetic.mp3", "audio/mpeg", bytes);
        return form;
    }

    private static void AddAudio(MultipartFormDataContent form, string field, string name, string contentType, byte[] bytes) {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(content, field, name);
    }

    public enum InvalidAudio { EmptyForm, EmptyFile, MismatchedType, UnsupportedExtension, UnknownField, ExtraText, MixedFileAndChunks, DuplicateFile, TooManyChunks }
    public enum InvalidSpeech { Blank, Null, TooLong, EmptyAgentId, UnknownVoice }
    public enum VoiceFailure { None, Disabled, MissingProvider, SharedProvider, ImageProvider, ProviderFailure, EmptyResult }
}
