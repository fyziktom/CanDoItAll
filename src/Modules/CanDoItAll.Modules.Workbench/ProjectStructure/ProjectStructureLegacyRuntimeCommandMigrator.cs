using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectStructureLegacyRuntimeCommandMigrationResult(
    string? Executable,
    IReadOnlyList<string> Arguments,
    string Message,
    bool WasWrapped)
{
    public bool IsSuccess => !string.IsNullOrWhiteSpace(Executable);

    public static ProjectStructureLegacyRuntimeCommandMigrationResult Success(
        string executable,
        IReadOnlyList<string> arguments,
        bool wasWrapped)
        => new(executable, arguments, "Legacy runtime command migrated to typed fields.", wasWrapped);

    public static ProjectStructureLegacyRuntimeCommandMigrationResult Fail(string message)
        => new(null, [], message, false);
}

internal static partial class ProjectStructureLegacyRuntimeCommandMigrator
{
    private static readonly IReadOnlySet<string> PowerShellEncodedCommandOptions = new HashSet<string>(
        ["-encodedcommand", "-enc", "-enco", "-encod", "-encode", "-encoded", "-e"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> PowerShellBoundedHostOptions = new HashSet<string>(
        ["-nologo", "-noprofile", "-noninteractive"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> BoundedWrapperExecutables = new HashSet<string>(
        [
            "dotnet",
            "dotnet.exe",
            "docker",
            "docker.exe",
            "python",
            "python.exe",
            "python3",
            "node",
            "node.exe",
            "npm",
            "npm.cmd",
            "npx",
            "npx.cmd",
            "tailwindcss",
            "tailwindcss.exe"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static ProjectStructureLegacyRuntimeCommandMigrationResult TryMigrate(
        string? command,
        string? arguments)
    {
        var commandLine = string.Join(
            ' ',
            new[] { command, arguments }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        if (!TryTokenizeStatic(commandLine, out var tokens, out var failureMessage))
        {
            return ProjectStructureLegacyRuntimeCommandMigrationResult.Fail(failureMessage);
        }

        if (tokens.Count == 0)
        {
            return ProjectStructureLegacyRuntimeCommandMigrationResult.Fail(
                "Runtime command metadata is empty and requires operator repair.");
        }

        var executableName = GetFileName(tokens[0]);
        if (executableName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
            executableName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase))
        {
            return TryUnwrapCmd(tokens);
        }

        if (IsPowerShell(executableName))
        {
            return TryUnwrapPowerShell(tokens);
        }

        return ProjectStructureLegacyRuntimeCommandMigrationResult.Success(
            tokens[0],
            tokens.Skip(1).ToArray(),
            wasWrapped: false);
    }

    public static bool TryTokenizeArguments(
        string? arguments,
        out IReadOnlyList<string> tokens,
        out string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            tokens = [];
            failureMessage = string.Empty;
            return true;
        }

        return TryTokenizeStatic(arguments, out tokens, out failureMessage);
    }

    internal static bool ContainsEncodedPowerShellOption(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           PowerShellEncodedOptionPattern().IsMatch(value);

    private static ProjectStructureLegacyRuntimeCommandMigrationResult TryUnwrapCmd(
        IReadOnlyList<string> tokens)
    {
        if (tokens.Count < 3 ||
            tokens[1] is not ("/c" or "/C" or "/k" or "/K"))
        {
            return ProjectStructureLegacyRuntimeCommandMigrationResult.Fail(
                "Only bounded static cmd /c or cmd /k wrappers can be migrated; this node requires operator repair.");
        }

        return TryUnwrapPayload(tokens.Skip(2).ToArray(), "cmd");
    }

    private static ProjectStructureLegacyRuntimeCommandMigrationResult TryUnwrapPowerShell(
        IReadOnlyList<string> tokens)
    {
        if (tokens.Any(token => PowerShellEncodedCommandOptions.Contains(token)))
        {
            return ProjectStructureLegacyRuntimeCommandMigrationResult.Fail(
                "Encoded PowerShell metadata cannot be inspected safely and requires operator repair.");
        }

        var commandIndexes = tokens
            .Select((token, index) => (token, index))
            .Where(item => item.index > 0 &&
                           (item.token.Equals("-command", StringComparison.OrdinalIgnoreCase) ||
                            item.token.Equals("-c", StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.index)
            .ToArray();
        if (commandIndexes.Length != 1)
        {
            return ProjectStructureLegacyRuntimeCommandMigrationResult.Fail(
                "Only one bounded static PowerShell -Command wrapper can be migrated; this node requires operator repair.");
        }

        var commandIndex = commandIndexes[0];
        if (commandIndex + 1 >= tokens.Count ||
            tokens.Skip(1).Take(commandIndex - 1).Any(option => !PowerShellBoundedHostOptions.Contains(option)))
        {
            return ProjectStructureLegacyRuntimeCommandMigrationResult.Fail(
                "Only bounded flag-only PowerShell host options followed by -Command can be migrated; this node requires operator repair.");
        }

        return TryUnwrapPayload(tokens.Skip(commandIndex + 1).ToArray(), "PowerShell");
    }

    private static ProjectStructureLegacyRuntimeCommandMigrationResult TryUnwrapPayload(
        IReadOnlyList<string> payloadTokens,
        string wrapperName)
    {
        if (payloadTokens.Count == 1 &&
            payloadTokens[0].Any(char.IsWhiteSpace))
        {
            if (!TryTokenizeStatic(payloadTokens[0], out payloadTokens, out var failureMessage))
            {
                return ProjectStructureLegacyRuntimeCommandMigrationResult.Fail(failureMessage);
            }
        }

        if (payloadTokens.Count == 0 ||
            !BoundedWrapperExecutables.Contains(GetFileName(payloadTokens[0])))
        {
            return ProjectStructureLegacyRuntimeCommandMigrationResult.Fail(
                $"The {wrapperName} wrapper does not contain a recognized static runtime executable and requires operator repair.");
        }

        return ProjectStructureLegacyRuntimeCommandMigrationResult.Success(
            payloadTokens[0],
            payloadTokens.Skip(1).ToArray(),
            wasWrapped: true);
    }

    private static bool TryTokenizeStatic(
        string commandLine,
        out IReadOnlyList<string> tokens,
        out string failureMessage)
    {
        var parsed = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var tokenStarted = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (character is '\r' or '\n' or '\0')
            {
                return FailTokenization(
                    "Runtime command metadata contains control characters and requires operator repair.",
                    out tokens,
                    out failureMessage);
            }

            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                    continue;
                }

                current.Append(character);
                tokenStarted = true;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                tokenStarted = true;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                AddToken(parsed, current, ref tokenStarted);
                continue;
            }

            if (character is ';' or '&' or '|' or '<' or '>' or '`')
            {
                return FailTokenization(
                    "Dynamic, chained, redirected, or encoded shell content requires operator repair.",
                    out tokens,
                    out failureMessage);
            }

            current.Append(character);
            tokenStarted = true;
        }

        if (quote != '\0')
        {
            return FailTokenization(
                "Runtime command metadata has an unterminated quote and requires operator repair.",
                out tokens,
                out failureMessage);
        }

        AddToken(parsed, current, ref tokenStarted);
        if (parsed.Any(IsDynamicToken))
        {
            return FailTokenization(
                "Dynamic shell substitutions require operator repair before this node can be executed.",
                out tokens,
                out failureMessage);
        }

        tokens = parsed;
        failureMessage = string.Empty;
        return true;
    }

    private static bool IsDynamicToken(string token)
        => token.Contains('$') ||
           EnvironmentVariablePattern().IsMatch(token);

    private static void AddToken(List<string> tokens, StringBuilder current, ref bool tokenStarted)
    {
        if (!tokenStarted)
        {
            return;
        }

        tokens.Add(current.ToString());
        current.Clear();
        tokenStarted = false;
    }

    private static bool FailTokenization(
        string message,
        out IReadOnlyList<string> tokens,
        out string failureMessage)
    {
        tokens = [];
        failureMessage = message;
        return false;
    }

    private static bool IsPowerShell(string executableName)
        => executableName.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
           executableName.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
           executableName.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ||
           executableName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase);

    private static string GetFileName(string path)
    {
        var separatorIndex = path.LastIndexOfAny(['\\', '/']);
        return separatorIndex >= 0 ? path[(separatorIndex + 1)..] : path;
    }

    [GeneratedRegex("%[^%]+%", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariablePattern();

    [GeneratedRegex(
        @"(?:^|\s)-(?:e|en|enc|enco|encod|encode|encoded|encodedcommand)(?=\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PowerShellEncodedOptionPattern();
}
