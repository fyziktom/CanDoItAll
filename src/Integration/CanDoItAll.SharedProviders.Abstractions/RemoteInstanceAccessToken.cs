namespace CanDoItAll.RemoteInstances;

public sealed class RemoteInstanceAccessToken {
    public const int MaximumLength = 16 * 1024;
    private readonly string value;

    public RemoteInstanceAccessToken(string value) {
        if (value is not { Length: > 0 and <= MaximumLength } || !value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~' or '+' or '/' or '=')) {
            throw new ArgumentException("The remote instance access token is invalid.", nameof(value));
        }
        this.value = value;
    }

    public TResult UseValue<TResult>(Func<string, TResult> action) {
        ArgumentNullException.ThrowIfNull(action);
        return action(value);
    }

    public override string ToString() => "[REDACTED]";
}
