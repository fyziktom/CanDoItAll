using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Voice;

public sealed class ProviderRuntimeVoiceDriver(
    IProviderRuntimeDescriptorStore descriptorStore,
    IProviderRuntimePool runtimePool) : ISpeechToTextVoiceDriver, ITextToSpeechVoiceDriver
{
    public AgentVoiceDriverKind DriverKind => AgentVoiceDriverKind.OpenAi;

    public async Task<AgentVoiceTranscriptionResult> TranscribeAsync(
        SpeechToTextDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = AgentVoiceSettingsNormalizer.NormalizeSpeechToText(request.Settings);
        var model = settings.Model;
        var handle = await GetRuntimeHandleAsync(request.Provider, cancellationToken).ConfigureAwait(false);
        var query = new ProviderDispatchQuery(
            request.Provider,
            AgentProviderCapabilityKind.SpeechToText,
            AgentProviderOperationKind.TranscribeSpeech,
            model);
        var payload = new ProviderSpeechToTextRequest(
            request.Provider,
            model,
            [new ProviderSpeechToTextAudio(request.FileName, request.ContentType, request.AudioBytes)],
            settings.Language,
            settings.Prompt);
        var result = await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderSpeechToTextRequest>(query, payload),
            async (context, token) =>
            {
                EnsureProviderKindMatches(context.Descriptor, context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<IProviderSpeechToTextDriver>(context.Query.Provider.Kind);
                return await driver.TranscribeSpeechAsync(context.Payload, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return new AgentVoiceTranscriptionResult(result.Text, result.Model);
    }

    public async Task<AgentVoiceSynthesisResult> SynthesizeAsync(
        TextToSpeechDriverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = AgentVoiceSettingsNormalizer.NormalizeTextToSpeech(request.Settings);
        var model = settings.Model;
        var handle = await GetRuntimeHandleAsync(request.Provider, cancellationToken).ConfigureAwait(false);
        var query = new ProviderDispatchQuery(
            request.Provider,
            AgentProviderCapabilityKind.TextToSpeech,
            AgentProviderOperationKind.SynthesizeSpeech,
            model);
        var payload = new ProviderTextToSpeechRequest(
            request.Provider,
            model,
            request.Text,
            request.VoiceId,
            settings.ResponseFormat,
            settings.Instructions);
        var result = await handle.DispatchAsync(
            new ProviderRuntimeDispatchRequest<ProviderTextToSpeechRequest>(query, payload),
            async (context, token) =>
            {
                EnsureProviderKindMatches(context.Descriptor, context.Query.Provider);
                var driver = handle.ProviderFactory.Resolve<IProviderTextToSpeechDriver>(context.Query.Provider.Kind);
                return await driver.SynthesizeSpeechAsync(context.Payload, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return new AgentVoiceSynthesisResult(
            result.AudioBytes,
            result.ContentType,
            result.Model,
            result.VoiceId,
            result.ResponseFormat);
    }

    private async ValueTask<IProviderRuntimeHandle> GetRuntimeHandleAsync(
        ProviderProfile provider,
        CancellationToken cancellationToken)
    {
        descriptorStore.Upsert(provider, secretReferenceIdentity: provider.ApiKeyEnvironmentVariable);
        return await runtimePool.GetRequiredAsync(provider.Id, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureProviderKindMatches(
        ProviderRuntimeDescriptor descriptor,
        ProviderProfile provider)
    {
        if (descriptor.ProviderKind != provider.Kind)
        {
            throw new InvalidOperationException("Provider runtime descriptor kind does not match the voice request provider kind.");
        }
    }
}
