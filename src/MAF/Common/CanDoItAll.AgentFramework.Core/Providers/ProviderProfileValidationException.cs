namespace CanDoItAll.AgentFramework.Core;

public sealed class ProviderProfileValidationException : Exception
{
    public ProviderProfileValidationException(string message)
        : base(message)
    {
    }

    public ProviderProfileValidationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
