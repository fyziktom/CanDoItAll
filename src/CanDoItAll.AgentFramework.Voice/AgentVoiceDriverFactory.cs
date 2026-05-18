using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Voice;

public sealed class AgentVoiceDriverFactory(OpenAiVoiceDriver openAiVoiceDriver) : IAgentVoiceDriverFactory
{
    public ISpeechToTextVoiceDriver CreateSpeechToTextDriver(AgentVoiceDriverKind driverKind)
    {
        return driverKind switch
        {
            AgentVoiceDriverKind.OpenAi => openAiVoiceDriver,
            _ => throw new InvalidOperationException($"Speech-to-text driver '{driverKind}' is not registered.")
        };
    }

    public ITextToSpeechVoiceDriver CreateTextToSpeechDriver(AgentVoiceDriverKind driverKind)
    {
        return driverKind switch
        {
            AgentVoiceDriverKind.OpenAi => openAiVoiceDriver,
            _ => throw new InvalidOperationException($"Text-to-speech driver '{driverKind}' is not registered.")
        };
    }
}
