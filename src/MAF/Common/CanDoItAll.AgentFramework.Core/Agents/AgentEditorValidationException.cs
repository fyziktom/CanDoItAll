namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentEditorValidationException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
