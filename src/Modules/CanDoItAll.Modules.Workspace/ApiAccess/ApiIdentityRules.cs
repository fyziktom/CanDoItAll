namespace CanDoItAll.Modules.Workspace.ApiAccess;

public static class ApiIdentityRules {
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 256;
    public const int MaximumUserNameLength = 64;
    public const int MaximumDisplayNameLength = 128;

    public static string UserName(string? value) {
        if (value is null || value.Length is < 1 or > MaximumUserNameLength ||
            value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '_' or '-' or '@'))) {
            throw new ArgumentException("Username must contain 1–64 ASCII letters, digits, '.', '_', '-' or '@'.");
        }
        return value;
    }

    public static string NormalizeUserName(string? value) => UserName(value).ToUpperInvariant();

    public static string DisplayName(string? value) {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumDisplayNameLength || value.Any(char.IsControl)) {
            throw new ArgumentException("Display name must contain 1–128 characters without control characters.");
        }
        return value.Trim();
    }

    public static void ValidatePassword(string? password) {
        if (password is null || password.Length is < MinimumPasswordLength or > MaximumPasswordLength) {
            throw new ArgumentException("Password must contain 12–256 characters.");
        }
    }
}
