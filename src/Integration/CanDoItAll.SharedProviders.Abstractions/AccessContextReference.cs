namespace CanDoItAll.SharedProviders.Abstractions;

public readonly record struct AccessContextReference
{
    public const int MaximumLength = 256;

    public AccessContextReference(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                $"An access-context reference must be a 1 to {MaximumLength} character opaque ASCII token.",
                nameof(value));
        }

        _value = value;
    }

    private readonly string? _value;

    public string Value
        => _value ?? throw new InvalidOperationException("The access-context reference is invalid.");

    public static AccessContextReference Parse(string value) => new(value);

    public static bool TryParse(string? value, out AccessContextReference reference)
    {
        if (!IsValid(value))
        {
            reference = default;
            return false;
        }

        reference = new AccessContextReference(value!);
        return true;
    }

    public override string ToString() => Value;

    private static bool IsValid(string? value)
        => value is { Length: > 0 and <= MaximumLength } &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '.' or '_' or '~' or ':' or '-');
}

public interface IAccessContextReferenceAccessor
{
    AccessContextReference? Current { get; }
}
