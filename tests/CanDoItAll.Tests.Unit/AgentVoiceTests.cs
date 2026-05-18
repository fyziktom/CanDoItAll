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

    [Fact]
    public void SpeechTextPreprocessor_RemovesFullGuidsAndAddsNotice()
    {
        var preprocessor = new AgentVoiceSpeechTextPreprocessor();
        var result = preprocessor.Prepare(
            "Project 5128a19c-2c76-4ea6-9458-349616e2c383 is active.",
            suppressIdentifierOmissionNotice: false);

        Assert.True(result.IdentifiersOmitted);
        Assert.True(result.IdentifierOmissionNoticeIncluded);
        Assert.Equal(1, result.RemovedIdentifierCount);
        Assert.StartsWith(AgentVoiceSpeechTextPreprocessor.IdentifierOmissionNotice, result.SpokenText, StringComparison.Ordinal);
        Assert.DoesNotContain("5128a19c-2c76-4ea6-9458-349616e2c383", result.SpokenText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Project", result.SpokenText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AI Tap id a845e5c9... is newest.", "a845e5c9")]
    [InlineData("Review item bf8ba85a\u2026 should be compared.", "bf8ba85a")]
    public void SpeechTextPreprocessor_RemovesTruncatedHexEllipsisIdsConservatively(
        string text,
        string identifierFragment)
    {
        var preprocessor = new AgentVoiceSpeechTextPreprocessor();
        var result = preprocessor.Prepare(text, suppressIdentifierOmissionNotice: false);

        Assert.True(result.IdentifiersOmitted);
        Assert.DoesNotContain(identifierFragment, result.SpokenText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(AgentVoiceSpeechTextPreprocessor.IdentifierOmissionNotice, result.SpokenText, StringComparison.Ordinal);
    }

    [Fact]
    public void SpeechTextPreprocessor_DoesNotRemoveShortOrNonHexEllipsisText()
    {
        var preprocessor = new AgentVoiceSpeechTextPreprocessor();
        var result = preprocessor.Prepare(
            "Use docs... then inspect project-alpha...",
            suppressIdentifierOmissionNotice: false);

        Assert.False(result.IdentifiersOmitted);
        Assert.False(result.IdentifierOmissionNoticeIncluded);
        Assert.Equal("Use docs... then inspect project-alpha...", result.SpokenText);
    }

    [Fact]
    public void SpeechTextPreprocessor_SuppressesNoticeWhenRequested()
    {
        var preprocessor = new AgentVoiceSpeechTextPreprocessor();
        var result = preprocessor.Prepare(
            "Project 5128a19c-2c76-4ea6-9458-349616e2c383 is active.",
            suppressIdentifierOmissionNotice: true);

        Assert.True(result.IdentifiersOmitted);
        Assert.False(result.IdentifierOmissionNoticeIncluded);
        Assert.DoesNotContain(AgentVoiceSpeechTextPreprocessor.IdentifierOmissionNotice, result.SpokenText, StringComparison.Ordinal);
        Assert.Equal("Project is active.", result.SpokenText);
    }

    [Fact]
    public void SpeechTextChunker_SplitsLongTextOnSentenceBoundaries()
    {
        var text = string.Join(
            ' ',
            Enumerable.Range(1, 6)
                .Select(index => $"Sentence {index} keeps enough detail to prove that speech text is grouped by complete sentences."));

        var chunks = AgentVoiceSpeechTextChunker.Split(text, maxChunkCharacters: 150);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 150, chunk));
        Assert.All(chunks.Take(chunks.Count - 1), chunk => Assert.EndsWith(".", chunk));
        Assert.Equal(text, string.Join(' ', chunks));
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
                Language = "en",
                Prompt = "The phrase may contain the words testing record."
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
        Assert.Contains("gpt-4o-mini-transcribe", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("en", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("The phrase may contain the words testing record.", handler.RequestBody, StringComparison.Ordinal);
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
        Assert.False(json.RootElement.TryGetProperty("instructions", out _));
    }

    [Theory]
    [InlineData("opus", "audio/ogg; codecs=opus")]
    [InlineData("aac", "audio/aac")]
    [InlineData("flac", "audio/flac")]
    [InlineData("wav", "audio/wav")]
    public async Task OpenAiVoiceDriver_Synthesis_ReturnsBrowserPlayableContentType(
        string responseFormat,
        string expectedContentType)
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
                ResponseFormat = responseFormat
            },
            "hello",
            "cedar"));

        using var json = JsonDocument.Parse(handler.RequestBody);

        Assert.Equal(expectedContentType, result.ContentType);
        Assert.Equal(responseFormat, result.ResponseFormat);
        Assert.Equal(responseFormat, json.RootElement.GetProperty("response_format").GetString());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.AudioBytes);
    }

    [Fact]
    public async Task OpenAiVoiceDriver_Synthesis_WrapsPcmForBrowserPlayback()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 0, 2, 0])
        });
        var driver = new OpenAiVoiceDriver(new HttpClient(handler), new FixedCredentialResolver("test-key"));

        var result = await driver.SynthesizeAsync(new TextToSpeechDriverRequest(
            CreateProvider(),
            new AgentTextToSpeechSettings
            {
                Model = "gpt-4o-mini-tts",
                ResponseFormat = "pcm"
            },
            "hello",
            "cedar"));

        using var json = JsonDocument.Parse(handler.RequestBody);

        Assert.Equal("audio/wav", result.ContentType);
        Assert.Equal("pcm", result.ResponseFormat);
        Assert.Equal("pcm", json.RootElement.GetProperty("response_format").GetString());
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(result.AudioBytes, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(result.AudioBytes, 8, 4));
        Assert.Equal(48, result.AudioBytes.Length);
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
            new AgentVoiceDriverFactory(new OpenAiVoiceDriver(new HttpClient(new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK))), new FixedCredentialResolver("test-key"))),
            new AgentVoiceSpeechTextPreprocessor());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TranscribeAsync(new AgentVoiceTranscriptionRequest([1], "voice.webm", "audio/webm")));

        Assert.Contains("provider profile must be selected", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentVoiceService_Synthesize_RejectsImageGenerationProvider()
    {
        var provider = CreateProvider(ProviderProfilePurpose.ImageGeneration);
        var service = new AgentVoiceService(
            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
            {
                TextToSpeech = new AgentTextToSpeechSettings
                {
                    IsEnabled = true,
                    ProviderProfileId = provider.Id
                }
            }),
            new InMemoryProviderRegistry([provider]),
            new AgentVoiceDriverFactory(new OpenAiVoiceDriver(new HttpClient(new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK))), new FixedCredentialResolver("test-key"))),
            new AgentVoiceSpeechTextPreprocessor());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SynthesizeSampleAsync("hello"));

        Assert.Contains("OpenAI text-to-speech requires an enabled OpenAI chat provider profile", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentVoiceService_Synthesize_UsesPreparedSpeechText()
    {
        var provider = CreateProvider();
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3, 4])
        });
        var service = new AgentVoiceService(
            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
            {
                TextToSpeech = new AgentTextToSpeechSettings
                {
                    IsEnabled = true,
                    ProviderProfileId = provider.Id,
                    Model = "gpt-4o-mini-tts",
                    ResponseFormat = "opus",
                    VoiceId = "cedar"
                }
            }),
            new InMemoryProviderRegistry([provider]),
            new AgentVoiceDriverFactory(new OpenAiVoiceDriver(new HttpClient(handler), new FixedCredentialResolver("test-key"))),
            new AgentVoiceSpeechTextPreprocessor());

        var result = await service.SynthesizeAsync(new AgentVoiceSynthesisRequest(
            "Project 5128a19c-2c76-4ea6-9458-349616e2c383 and project bf8ba85a... are active.",
            SuppressIdentifierOmissionNotice: false));
        using var json = JsonDocument.Parse(handler.RequestBody);
        var input = json.RootElement.GetProperty("input").GetString() ?? string.Empty;

        Assert.True(result.IdentifiersOmitted);
        Assert.True(result.IdentifierOmissionNoticeIncluded);
        Assert.Equal(result.SpokenText, input);
        Assert.Contains(AgentVoiceSpeechTextPreprocessor.IdentifierOmissionNotice, input, StringComparison.Ordinal);
        Assert.DoesNotContain("5128a19c-2c76-4ea6-9458-349616e2c383", input, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bf8ba85a", input, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentVoiceService_SynthesizeChunks_UsesPreparedSpeechTextInOrder()
    {
        var provider = CreateProvider();
        var driver = new CapturingVoiceDriver([]);
        var service = new AgentVoiceService(
            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
            {
                TextToSpeech = new AgentTextToSpeechSettings
                {
                    IsEnabled = true,
                    ProviderProfileId = provider.Id,
                    Model = "gpt-4o-mini-tts",
                    ResponseFormat = "mp3",
                    VoiceId = "cedar"
                }
            }),
            new InMemoryProviderRegistry([provider]),
            new StaticVoiceDriverFactory(driver, driver),
            new AgentVoiceSpeechTextPreprocessor());
        var text = string.Join(
            ' ',
            Enumerable.Range(1, 10)
                .Select(index => $"Sentence {index} explains why progressive speech playback should not wait for the entire answer before the first audio starts."))
            + " Project 5128a19c-2c76-4ea6-9458-349616e2c383 remains visible in text.";

        var results = new List<AgentVoiceSynthesisResult>();
        await foreach (var result in service.SynthesizeChunksAsync(new AgentVoiceSynthesisRequest(
                           text,
                           SuppressIdentifierOmissionNotice: false)))
        {
            results.Add(result);
        }

        Assert.True(results.Count > 1);
        Assert.Equal(results.Count, driver.SynthesisRequests.Count);
        Assert.All(driver.SynthesisRequests, request => Assert.True(
            request.Text.Length <= AgentVoiceSpeechTextChunker.DefaultMaxChunkCharacters,
            request.Text));
        Assert.All(driver.SynthesisRequests, request =>
            Assert.DoesNotContain("5128a19c-2c76-4ea6-9458-349616e2c383", request.Text, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(driver.SynthesisRequests.Select(request => request.Text), results.Select(result => result.SpokenText));
        Assert.Equal(0, results[0].ChunkIndex);
        Assert.All(results, result => Assert.Equal(results.Count, result.ChunkCount));
        Assert.True(results[0].IdentifierOmissionNoticeIncluded);
        Assert.All(results.Skip(1), result => Assert.False(result.IdentifierOmissionNoticeIncluded));
    }

    [Fact]
    public async Task AgentVoiceService_Transcribe_TranscribesOrderedAudioChunks()
    {
        var provider = CreateProvider();
        var driver = new CapturingVoiceDriver(["first chunk", "second chunk"]);
        var service = new AgentVoiceService(
            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
            {
                SpeechToText = new AgentSpeechToTextSettings
                {
                    IsEnabled = true,
                    ProviderProfileId = provider.Id,
                    Model = "gpt-4o-mini-transcribe"
                }
            }),
            new InMemoryProviderRegistry([provider]),
            new StaticVoiceDriverFactory(driver, driver),
            new AgentVoiceSpeechTextPreprocessor());

        var result = await service.TranscribeAsync(new AgentVoiceTranscriptionRequest([], "voice.webm", "audio/webm")
        {
            AudioChunks =
            [
                new AgentVoiceAudioChunk([1, 2, 3], "voice-1.webm", "audio/webm"),
                new AgentVoiceAudioChunk([4, 5, 6], "voice-2.webm", "audio/webm")
            ]
        });

        Assert.Equal($"first chunk{Environment.NewLine}second chunk", result.Text);
        Assert.Equal("gpt-4o-mini-transcribe", result.Model);
        Assert.Equal(2, driver.TranscriptionRequests.Count);
        Assert.Equal(new byte[] { 1, 2, 3 }, driver.TranscriptionRequests[0].AudioBytes);
        Assert.Equal(new byte[] { 4, 5, 6 }, driver.TranscriptionRequests[1].AudioBytes);
        Assert.Equal("voice-1.webm", driver.TranscriptionRequests[0].FileName);
        Assert.Equal("voice-2.webm", driver.TranscriptionRequests[1].FileName);
    }

    [Fact]
    public async Task AgentVoiceService_Transcribe_RejectsEmptyAudioChunk()
    {
        var provider = CreateProvider();
        var driver = new CapturingVoiceDriver(["ignored"]);
        var service = new AgentVoiceService(
            new InMemoryWorkflowSettingsService(new AgentVoiceSettings
            {
                SpeechToText = new AgentSpeechToTextSettings
                {
                    IsEnabled = true,
                    ProviderProfileId = provider.Id
                }
            }),
            new InMemoryProviderRegistry([provider]),
            new StaticVoiceDriverFactory(driver, driver),
            new AgentVoiceSpeechTextPreprocessor());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TranscribeAsync(new AgentVoiceTranscriptionRequest([], "voice.webm", "audio/webm")
            {
                AudioChunks = [new AgentVoiceAudioChunk([], "voice-1.webm", "audio/webm")]
            }));

        Assert.Contains("audio chunk 1 is empty", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(driver.TranscriptionRequests);
    }

    private static ProviderProfile CreateProvider(
        ProviderProfilePurpose purpose = ProviderProfilePurpose.Chat)
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
            SuggestedModels: [],
            Purpose: purpose);
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

    private sealed class StaticVoiceDriverFactory(
        ISpeechToTextVoiceDriver speechToTextDriver,
        ITextToSpeechVoiceDriver textToSpeechDriver) : IAgentVoiceDriverFactory
    {
        public ISpeechToTextVoiceDriver CreateSpeechToTextDriver(AgentVoiceDriverKind driverKind)
        {
            return speechToTextDriver;
        }

        public ITextToSpeechVoiceDriver CreateTextToSpeechDriver(AgentVoiceDriverKind driverKind)
        {
            return textToSpeechDriver;
        }
    }

    private sealed class CapturingVoiceDriver(IEnumerable<string> transcriptResults) : ISpeechToTextVoiceDriver, ITextToSpeechVoiceDriver
    {
        private readonly Queue<string> transcriptQueue = new(transcriptResults);

        public AgentVoiceDriverKind DriverKind => AgentVoiceDriverKind.OpenAi;

        public List<SpeechToTextDriverRequest> TranscriptionRequests { get; } = [];

        public List<TextToSpeechDriverRequest> SynthesisRequests { get; } = [];

        public Task<AgentVoiceTranscriptionResult> TranscribeAsync(
            SpeechToTextDriverRequest request,
            CancellationToken cancellationToken = default)
        {
            TranscriptionRequests.Add(request);
            var transcript = transcriptQueue.Count == 0
                ? string.Empty
                : transcriptQueue.Dequeue();

            return Task.FromResult(new AgentVoiceTranscriptionResult(transcript, request.Settings.Model));
        }

        public Task<AgentVoiceSynthesisResult> SynthesizeAsync(
            TextToSpeechDriverRequest request,
            CancellationToken cancellationToken = default)
        {
            SynthesisRequests.Add(request);
            return Task.FromResult(new AgentVoiceSynthesisResult(
                [(byte)SynthesisRequests.Count],
                "audio/mpeg",
                request.Settings.Model,
                request.VoiceId,
                request.Settings.ResponseFormat));
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

    private sealed class EmptyProviderRegistry : InMemoryProviderRegistry
    {
        public EmptyProviderRegistry()
            : base([])
        {
        }
    }

    private class InMemoryProviderRegistry(IReadOnlyList<ProviderProfile> providers) : IProviderProfileRegistry
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(providers);
        }

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(providers.FirstOrDefault(provider => provider.Id == providerId));
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
