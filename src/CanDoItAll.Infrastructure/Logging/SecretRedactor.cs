using System.Text.RegularExpressions;

namespace CanDoItAll.Infrastructure.Logging;

public interface ISecretRedactor
{
    string Redact(string? input);
}

public sealed partial class SecretRedactor : ISecretRedactor
{
    public string Redact(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var redacted = input;
        redacted = ApiKeyRegex().Replace(redacted, "api_key=[REDACTED]");
        redacted = BearerRegex().Replace(redacted, "Bearer [REDACTED]");
        redacted = PasswordRegex().Replace(redacted, "password=[REDACTED]");
        return redacted;
    }

    [GeneratedRegex(@"api[_-]?key\s*=\s*[^;\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ApiKeyRegex();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-\._~\+/]+=*", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"password\s*=\s*[^;\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PasswordRegex();
}
