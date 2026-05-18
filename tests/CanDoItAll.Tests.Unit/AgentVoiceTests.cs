using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentVoiceTests
{
    [Fact]
    public void VoiceAccessMetadata_WriteAndRead_RoundTrips()
    {
        var configuration = AgentVoiceAccessMetadata.Write("{}", new AgentVoiceAccessSettings
        {
            CanUseVoiceMode = true,
            PreferredVoiceId = " cedar "
        });

        var settings = AgentVoiceAccessMetadata.Read(configuration);

        Assert.True(settings.CanUseVoiceMode);
        Assert.Equal("cedar", settings.PreferredVoiceId);
    }

    [Fact]
    public void VoiceAccessMetadata_Normalize_ClearsVoiceWhenVoiceModeDisabled()
    {
        var settings = AgentVoiceAccessMetadata.Normalize(new AgentVoiceAccessSettings
        {
            CanUseVoiceMode = false,
            PreferredVoiceId = "marin"
        });

        Assert.False(settings.CanUseVoiceMode);
        Assert.Empty(settings.PreferredVoiceId);
    }

    [Fact]
    public void VoiceSettingsNormalizer_UsesAgentVoiceOverrideBeforeGeneralVoice()
    {
        var settings = new AgentTextToSpeechSettings
        {
            VoiceId = "marin"
        };
        var access = new AgentVoiceAccessSettings
        {
            CanUseVoiceMode = true,
            PreferredVoiceId = "cedar"
        };

        var voiceId = AgentVoiceSettingsNormalizer.ResolveEffectiveVoiceId(settings, access);

        Assert.Equal("cedar", voiceId);
    }

    [Theory]
    [InlineData("yes", AgentVoiceConfirmationIntent.Affirm)]
    [InlineData("ok this is good, store it", AgentVoiceConfirmationIntent.Affirm)]
    [InlineData("no, do not store this", AgentVoiceConfirmationIntent.Reject)]
    [InlineData("maybe change the wording", AgentVoiceConfirmationIntent.Unknown)]
    public void ConfirmationClassifier_ClassifiesExpectedPhrases(
        string transcript,
        AgentVoiceConfirmationIntent expectedIntent)
    {
        Assert.Equal(expectedIntent, AgentVoiceConfirmationClassifier.Classify(transcript));
    }

    [Fact]
    public async Task OpenAiVoiceDriver_Transcription_BuildsExpectedRequest()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"text":"hello world"}""")
        });
        var driver = new OpenAiVoiceDriver(new HttpClient(handler), new FixedCredentialResolver("test-key"));

        var result = await driver.TranscribeAsync(new SpeechToTextDriverRequest(
            CreateProvider(),
            new AgentSpeechToTextSettings
            {
                Model = "gpt-4o-mini-transcribe",
                Language = "en"
            },
            [1, 2, 3],
            "voice.webm",
            "audio/webm"));

        Assert.Equal("hello world", result.Text);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.openai.com/v1/audio/transcriptions", handler.Request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", handler.Request.Headers.Authorization.Parameter);
        Assert.IsType<MultipartFormDataContent>(handler.Request.Content);
    }

    [Fact]
    public async Task OpenAiVoiceDriver_Synthesis_BuildsExpectedRequest()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3, 4])
        });
        var driver = new OpenAiVoiceDriver(new HttpClient(handler), new FixedCredentialResolver("test-key"));

        var result = await driver.SynthesizeAsync(new TextToSpeechDriverRequest(
            CreateProvider(),
            new AgentTextToSpeechSettings
            {
                Model = "gpt-4o-mini-tts",
                ResponseFormat = "mp3"
            },
            "hello",
            "cedar"));

        using var json = JsonDocument.Parse(handler.RequestBody);

        Assert.Equal("audio/mpeg", result.ContentType);
        Assert.Equal("https://api.openai.com/v1/audio/speech", handler.Request!.RequestUri!.AbsoluteUri);
        Assert.Equal("gpt-4o-mini-tts", json.RootElement.GetProperty("model").GetString());
        Assert.Equal("cedar", json.RootElement.GetProperty("voice").GetString());
        Assert.Equal("hello", json.RootElement.GetProperty("input").GetString());
        Assert.Equal("mp3", json.RootElement.GetProperty("response_format").GetString());
    }

    [Fact]
    public async Task AgentVoiceService_Transcribe_RequiresConfiguredProvider()
    {
        var service = new AgentVoiceService(
            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
            {
                SpeechToText = new AgentSpeechToTextSettings
                {
                    IsEnabled = true,
                    ProviderProfileId = null
                }
            }),
            new EmptyProviderRegistry(),
            new AgentVoiceDriverFactory(new OpenAiVoiceDriver(new HttpClient(new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK))), new FixedCredentialResolver("test-key"))));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TranscribeAsync(new AgentVoiceTranscriptionRequest([1], "voice.webm", "audio/webm")));

        Assert.Contains("provider profile must be selected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderProfile CreateProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "OpenAI",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1/models",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return response;
        }
    }

    private sealed class FixedCredentialResolver(string apiKey) : IAgentProviderCredentialResolver
    {
        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            return new ProviderCredentialResolution(apiKey, "test", string.Empty);
        }
    }

    private sealed class InMemoryWorkflowSettingsService(AgentVoiceSettings voiceSettings) : IWorkflowSettingsService
    {
        private WorkflowSettings settings = WorkflowSettings.Default with
        {
            VoiceSettings = voiceSettings
        };

        public Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(settings);
        }

        public Task<WorkflowSettings> SaveSettingsAsync(
            WorkflowSettings settings,
            CancellationToken cancellationToken = default)
        {
            this.settings = settings;
            return Task.FromResult(settings);
        }
    }

    private sealed class EmptyProviderRegistry : IProviderProfileRegistry
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProviderProfile>>([]);
        }

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProviderProfile?>(null);
        }

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderProfile> UpdateProviderAsync(
            Guid providerId,
            Func<ProviderProfile, ProviderProfile> update,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
