using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.Logging;

public interface ISecretRedactor
{
    string Redact(string? input);
}

public sealed class SecretRedactor : ISecretRedactor
{
    public string Redact(string? input) => SensitiveTextRedactor.Redact(input);
}
