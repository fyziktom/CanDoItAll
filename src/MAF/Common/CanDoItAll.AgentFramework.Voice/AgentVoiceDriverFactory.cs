using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Voice;

public sealed class AgentVoiceDriverFactory(ProviderRuntimeVoiceDriver providerRuntimeVoiceDriver) : IAgentVoiceDriverFactory
{
    public ISpeechToTextVoiceDriver CreateSpeechToTextDriver(AgentVoiceDriverKind driverKind)
    {
        EnsureRegisteredDriver(driverKind, "speech-to-text");
        return providerRuntimeVoiceDriver;
    }

    public ITextToSpeechVoiceDriver CreateTextToSpeechDriver(AgentVoiceDriverKind driverKind)
    {
        EnsureRegisteredDriver(driverKind, "text-to-speech");
        return providerRuntimeVoiceDriver;
    }

    private static void EnsureRegisteredDriver(
        AgentVoiceDriverKind driverKind,
        string capabilityName)
    {
        if (driverKind != AgentVoiceDriverKind.OpenAi)
        {
            throw new InvalidOperationException($"{capabilityName} driver '{driverKind}' is not registered.");
        }
    }
}
