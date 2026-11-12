using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectExternalIdentity(string Namespace, string Key) {
    internal const int MaximumLength = 100;
    internal const string UniqueIndexName = "UX_Projects_ExternalIdentity";
    internal const string CheckConstraintName = "CK_Projects_ExternalIdentity";
    internal const string CheckConstraintSql = """
        ("ExternalNamespace" IS NULL AND "ExternalKey" IS NULL) OR
        ("ExternalNamespace" IS NOT NULL AND "ExternalKey" IS NOT NULL AND
         "ExternalNamespace" COLLATE "C" ~ '^[a-z0-9]([a-z0-9._-]{0,98}[a-z0-9])?$' AND
         "ExternalKey" COLLATE "C" ~ '^[a-z0-9]([a-z0-9._-]{0,98}[a-z0-9])?$')
        """;

    internal string MutationScopeKey => $"projects:external:{Namespace}:{Key}";

    internal static Result<ProjectExternalIdentity> Normalize(string? externalNamespace, string? externalKey) {
        var normalizedNamespace = externalNamespace?.Trim().ToLowerInvariant();
        var normalizedKey = externalKey?.Trim().ToLowerInvariant();
        if (!IsValid(normalizedNamespace) || !IsValid(normalizedKey)) {
            return Result<ProjectExternalIdentity>.Failure(Error.Validation(
                "External namespace and key must both contain 1-100 ASCII letters, digits, dots, underscores or hyphens, starting and ending with a letter or digit.",
                ProjectErrorCodes.ExternalIdentityInvalid));
        }
        return Result<ProjectExternalIdentity>.Success(new(normalizedNamespace!, normalizedKey!));
    }

    private static bool IsValid(string? value) {
        if (value is not { Length: > 0 and <= MaximumLength } || !IsLetterOrDigit(value[0]) || !IsLetterOrDigit(value[^1])) {
            return false;
        }
        return value.All(character => IsLetterOrDigit(character) || character is '.' or '_' or '-');
    }

    private static bool IsLetterOrDigit(char character) => character is >= 'a' and <= 'z' or >= '0' and <= '9';
}
