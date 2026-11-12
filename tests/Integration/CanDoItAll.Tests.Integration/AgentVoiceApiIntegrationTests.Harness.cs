using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed partial class AgentVoiceApiIntegrationTests {
    private sealed class VoiceHarness : IWorkflowSettingsService, IProviderRuntimeProfileSource,
        IAgentVoiceDriverFactory, ISpeechToTextVoiceDriver, ITextToSpeechVoiceDriver {
        public const string PrivateFailureText = "private-provider-diagnostic-never-return";
        private readonly VoiceFailure failure;
        private readonly ProviderProfile provider;
        private WorkflowSettings settings;

        public VoiceHarness(VoiceFailure failure = VoiceFailure.None) {
            this.failure = failure;
            provider = new ProviderProfile(Guid.NewGuid(), "Synthetic voice provider", ProviderKind.OpenAi,
                "https://voice.example/v1", string.Empty, "synthetic-model", ProviderTransportKind.Responses,
                true, false, false, false, false, "{}", string.Empty, string.Empty, null, [],
                failure == VoiceFailure.ImageProvider ? ProviderProfilePurpose.ImageGeneration : ProviderProfilePurpose.Chat) {
                CredentialBinding = failure == VoiceFailure.SharedProvider
                    ? new ProviderCredentialBinding(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                        ProviderCredentialConsumerKind.Source, Guid.NewGuid())
                    : null
            };
            settings = WorkflowSettings.Default with {
                VoiceSettings = new AgentVoiceSettings {
                    SpeechToText = new AgentSpeechToTextSettings { IsEnabled = failure != VoiceFailure.Disabled, ProviderProfileId = provider.Id },
                    TextToSpeech = new AgentTextToSpeechSettings { IsEnabled = failure != VoiceFailure.Disabled, ProviderProfileId = provider.Id }
                }
            };
        }

        public AgentVoiceDriverKind DriverKind => AgentVoiceDriverKind.OpenAi;
        public List<SpeechToTextDriverRequest> Transcriptions { get; } = [];
        public List<TextToSpeechDriverRequest> Syntheses { get; } = [];
        public int CallCount => Transcriptions.Count + Syntheses.Count;
        public bool WaitForCancellation { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);

        public Task<WorkflowSettings> SaveSettingsAsync(WorkflowSettings value, CancellationToken cancellationToken = default) {
            settings = value;
            return Task.FromResult(settings);
        }

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProviderProfile?>(failure == VoiceFailure.MissingProvider ? null : provider);

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);

        public ISpeechToTextVoiceDriver CreateSpeechToTextDriver(AgentVoiceDriverKind driverKind) => this;
        public ITextToSpeechVoiceDriver CreateTextToSpeechDriver(AgentVoiceDriverKind driverKind) => this;

        public async Task<AgentVoiceTranscriptionResult> TranscribeAsync(SpeechToTextDriverRequest request, CancellationToken cancellationToken = default) {
            Transcriptions.Add(request);
            await BeforeResultAsync(cancellationToken);
            return new AgentVoiceTranscriptionResult(failure == VoiceFailure.EmptyResult ? string.Empty : Encoding.UTF8.GetString(request.AudioBytes), request.Settings.Model);
        }

        public async Task<AgentVoiceSynthesisResult> SynthesizeAsync(TextToSpeechDriverRequest request, CancellationToken cancellationToken = default) {
            Syntheses.Add(request);
            await BeforeResultAsync(cancellationToken);
            return new AgentVoiceSynthesisResult(failure == VoiceFailure.EmptyResult ? [] : Encoding.UTF8.GetBytes(request.Text),
                "audio/mpeg", request.Settings.Model, request.VoiceId, request.Settings.ResponseFormat);
        }

        private async Task BeforeResultAsync(CancellationToken cancellationToken) {
            Started.TrySetResult();
            if (failure == VoiceFailure.ProviderFailure) {
                throw new InvalidOperationException(PrivateFailureText);
            }
            if (!WaitForCancellation) {
                return;
            }
            try {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            } catch (OperationCanceledException) {
                Cancelled.TrySetResult();
                throw;
            }
        }
    }
}
