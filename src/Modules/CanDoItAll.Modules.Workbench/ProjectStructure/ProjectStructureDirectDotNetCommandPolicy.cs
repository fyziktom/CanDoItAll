using System.Text;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructureDirectDotNetCommandKind
{
    Run,
    Watch
}

internal static class ProjectStructureDirectDotNetCommandPolicy
{
    private enum ShellDialect
    {
        Unspecified,
        Cmd,
        PowerShell
    }

    private readonly record struct CommandToken(string Value, bool WasQuoted);

    private static readonly IReadOnlySet<string> CmdStartOptionsWithValues = new HashSet<string>(
        ["/d", "/node", "/affinity", "/machine"],
        StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlySet<string> PowerShellStartProcessOptionsWithValues = new HashSet<string>(
        [
            "-credential",
            "-environment",
            "-erroraction",
            "-errorvariable",
            "-informationaction",
            "-informationvariable",
            "-outbuffer",
            "-outvariable",
            "-pipelinevariable",
            "-progressaction",
            "-redirectstandarderror",
            "-redirectstandardinput",
            "-redirectstandardoutput",
            "-verb",
            "-warningaction",
            "-warningvariable",
            "-windowstyle",
            "-workingdirectory"
        ],
        StringComparer.OrdinalIgnoreCase);

    public const string TypedEnvironmentRequiredMessage =
        "Direct dotnet run and dotnet watch commands, including standard cmd and PowerShell process wrappers, must use a typed Environment node with objectSubtype dotnet-runtime, dotnet-watch, or dotnet-release and an exact verified metadata.environment.projectPath. To repair an existing Script node, use project_structure_node_update; a metadata-only update cannot reclassify it. Static metadata validation does not interpret arbitrary script files, aliases, functions, encoded commands, or dynamically constructed commands.";

    public static bool TryClassify(
        string? command,
        string? arguments,
        out ProjectStructureDirectDotNetCommandKind commandKind)
    {
        commandKind = default;
        var commandLine = string.Join(
            ' ',
            new[] { command, arguments }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
        if (!TryTokenize(commandLine, out var tokens))
        {
            return false;
        }

        return TryClassifyTokens(
            tokens,
            recursionDepth: 0,
            ShellDialect.PowerShell,
            out commandKind);
    }

    private static bool TryClassifyTokens(
        IReadOnlyList<CommandToken> tokens,
        int recursionDepth,
        ShellDialect shellDialect,
        out ProjectStructureDirectDotNetCommandKind commandKind)
    {
        commandKind = default;
        var executableIndex = tokens.Count > 0 && tokens[0].Value == "&" ? 1 : 0;
        if (tokens.Count <= executableIndex)
        {
            return false;
        }

        if (IsDotNetExecutable(tokens[executableIndex].Value) && tokens.Count > executableIndex + 1)
        {
            if (tokens[executableIndex + 1].Value.Equals("run", StringComparison.OrdinalIgnoreCase))
            {
                commandKind = ProjectStructureDirectDotNetCommandKind.Run;
                return true;
            }

            if (tokens[executableIndex + 1].Value.Equals("watch", StringComparison.OrdinalIgnoreCase))
            {
                commandKind = ProjectStructureDirectDotNetCommandKind.Watch;
                return true;
            }
        }

        if (recursionDepth >= 4)
        {
            return false;
        }

        if (TryClassifyKnownProcessWrapper(
                tokens,
                executableIndex,
                recursionDepth,
                shellDialect,
                out commandKind))
        {
            return true;
        }

        for (var index = executableIndex; index < tokens.Count; index++)
        {
            if (IsCommandBoundary(tokens[index].Value) &&
                index + 1 < tokens.Count &&
                TryClassifyTokens(
                    tokens.Skip(index + 1).ToArray(),
                    recursionDepth + 1,
                    shellDialect,
                    out commandKind))
            {
                return true;
            }
        }

        if (!TryFindShellCommandPayload(
                tokens,
                executableIndex,
                out var payloadTokens,
                out var payloadDialect))
        {
            return false;
        }

        if (TryClassifyTokens(
                payloadTokens,
                recursionDepth + 1,
                payloadDialect,
                out commandKind))
        {
            return true;
        }

        return payloadTokens.Count == 1 &&
               TryTokenize(payloadTokens[0].Value, out var parsedPayloadTokens) &&
               TryClassifyTokens(
                   parsedPayloadTokens,
                   recursionDepth + 1,
                   payloadDialect,
                   out commandKind);
    }

    private static bool TryClassifyKnownProcessWrapper(
        IReadOnlyList<CommandToken> tokens,
        int executableIndex,
        int recursionDepth,
        ShellDialect shellDialect,
        out ProjectStructureDirectDotNetCommandKind commandKind)
    {
        commandKind = default;
        var wrapper = tokens[executableIndex].Value;
        if (wrapper.Equals("call", StringComparison.OrdinalIgnoreCase))
        {
            return TryClassifyTokens(
                tokens.Skip(executableIndex + 1).ToArray(),
                recursionDepth + 1,
                shellDialect,
                out commandKind);
        }

        if (wrapper.Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            if (shellDialect == ShellDialect.PowerShell)
            {
                return TryClassifyPowerShellStartProcess(
                    tokens,
                    executableIndex,
                    recursionDepth,
                    out commandKind);
            }

            var commandIndex = executableIndex + 1;
            if (!TrySkipCmdStartOptions(tokens, ref commandIndex))
            {
                return false;
            }

            if (commandIndex < tokens.Count && tokens[commandIndex].WasQuoted)
            {
                commandIndex++;
                if (!TrySkipCmdStartOptions(tokens, ref commandIndex))
                {
                    return false;
                }
            }

            return commandIndex < tokens.Count &&
                   TryClassifyTokens(
                       tokens.Skip(commandIndex).ToArray(),
                       recursionDepth + 1,
                       shellDialect,
                       out commandKind);
        }

        if (!IsExecutable(wrapper, "start-process", "saps"))
        {
            return false;
        }

        return TryClassifyPowerShellStartProcess(
            tokens,
            executableIndex,
            recursionDepth,
            out commandKind);
    }

    private static bool TryClassifyPowerShellStartProcess(
        IReadOnlyList<CommandToken> tokens,
        int executableIndex,
        int recursionDepth,
        out ProjectStructureDirectDotNetCommandKind commandKind)
    {
        commandKind = default;
        string? targetExecutable = null;
        var argumentListIndex = -1;
        for (var index = executableIndex + 1; index < tokens.Count; index++)
        {
            if (tokens[index].Value.Equals("-filepath", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= tokens.Count)
                {
                    return false;
                }

                targetExecutable = tokens[++index].Value;
                continue;
            }

            if (tokens[index].Value.Equals("-argumentlist", StringComparison.OrdinalIgnoreCase) ||
                tokens[index].Value.Equals("-args", StringComparison.OrdinalIgnoreCase))
            {
                argumentListIndex = index;
                break;
            }

            if (PowerShellStartProcessOptionsWithValues.Contains(tokens[index].Value))
            {
                if (index + 1 >= tokens.Count)
                {
                    return false;
                }

                index++;
                continue;
            }

            if (tokens[index].Value.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            if (targetExecutable is null)
            {
                targetExecutable = tokens[index].Value;
            }
        }

        if (!IsDotNetExecutable(targetExecutable ?? string.Empty))
        {
            return false;
        }

        var dotNetExecutable = targetExecutable!;
        var rawArguments = argumentListIndex >= 0
            ? tokens.Skip(argumentListIndex + 1)
            : tokens.Skip(executableIndex + 1)
                .SkipWhile(token => !string.Equals(token.Value, dotNetExecutable, StringComparison.OrdinalIgnoreCase))
                .Skip(1);
        var argumentText = string.Join(' ', rawArguments.Select(token => token.Value)).Replace(',', ' ');
        if (!TryTokenize(argumentText, out var argumentTokens))
        {
            return false;
        }

        return TryClassifyTokens(
            new[] { new CommandToken(dotNetExecutable, WasQuoted: false) }
                .Concat(argumentTokens)
                .ToArray(),
            recursionDepth + 1,
            ShellDialect.PowerShell,
            out commandKind);
    }

    private static bool TrySkipCmdStartOptions(
        IReadOnlyList<CommandToken> tokens,
        ref int index)
    {
        while (index < tokens.Count && tokens[index].Value.StartsWith("/", StringComparison.Ordinal))
        {
            var option = tokens[index++].Value;
            if (!CmdStartOptionsWithValues.Contains(option))
            {
                continue;
            }

            if (index >= tokens.Count)
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static bool IsDotNetExecutable(string token)
        => IsExecutable(token, "dotnet", "dotnet.exe");

    private static bool IsExecutable(string token, params string[] executableNames)
    {
        var separatorIndex = token.LastIndexOfAny(['\\', '/']);
        var fileName = separatorIndex >= 0 ? token[(separatorIndex + 1)..] : token;
        return executableNames.Any(name => fileName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryFindShellCommandPayload(
        IReadOnlyList<CommandToken> tokens,
        int executableIndex,
        out IReadOnlyList<CommandToken> payloadTokens,
        out ShellDialect shellDialect)
    {
        payloadTokens = [];
        shellDialect = ShellDialect.Unspecified;
        string[] commandSwitches;
        if (IsExecutable(tokens[executableIndex].Value, "powershell", "powershell.exe", "pwsh", "pwsh.exe"))
        {
            commandSwitches = ["-command", "-c"];
            shellDialect = ShellDialect.PowerShell;
        }
        else if (IsExecutable(tokens[executableIndex].Value, "cmd", "cmd.exe"))
        {
            commandSwitches = ["/c", "/k"];
            shellDialect = ShellDialect.Cmd;
        }
        else
        {
            return false;
        }

        for (var index = executableIndex + 1; index < tokens.Count; index++)
        {
            if (!commandSwitches.Contains(tokens[index].Value, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= tokens.Count)
            {
                return false;
            }

            payloadTokens = tokens.Skip(index + 1).ToArray();
            return true;
        }

        return false;
    }

    private static bool IsCommandBoundary(string token)
        => token is ";" or "&" or "&&" or "|" or "||" or "{" or "}" or "(" or ")";

    private static bool TryTokenize(string commandLine, out List<CommandToken> tokens)
    {
        tokens = [];
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return true;
        }

        var current = new StringBuilder();
        var quote = '\0';
        var tokenStarted = false;
        var tokenWasQuoted = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var character = commandLine[index];
            if (quote != '\0')
            {
                if (character == '`' && index + 1 < commandLine.Length)
                {
                    current.Append(commandLine[++index]);
                    continue;
                }

                if (character == quote)
                {
                    if (index + 1 < commandLine.Length && commandLine[index + 1] == quote)
                    {
                        current.Append(quote);
                        index++;
                        continue;
                    }

                    quote = '\0';
                    continue;
                }

                current.Append(character);
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                AddToken(tokens, current, ref tokenStarted, ref tokenWasQuoted);
                continue;
            }

            if (character is '\'' or '"')
            {
                tokenWasQuoted = !tokenStarted;
                quote = character;
                tokenStarted = true;
                continue;
            }

            if (character is ';' or '&' or '|' or '{' or '}' or '(' or ')')
            {
                AddToken(tokens, current, ref tokenStarted, ref tokenWasQuoted);
                var shellOperator = character.ToString();
                if ((character is '&' or '|') &&
                    index + 1 < commandLine.Length &&
                    commandLine[index + 1] == character)
                {
                    shellOperator += commandLine[++index];
                }

                tokens.Add(new CommandToken(shellOperator, WasQuoted: false));
                continue;
            }

            if (character == '`' && index + 1 < commandLine.Length)
            {
                current.Append(commandLine[++index]);
                tokenStarted = true;
                continue;
            }

            current.Append(character);
            if (tokenWasQuoted)
            {
                tokenWasQuoted = false;
            }
            tokenStarted = true;
        }

        if (quote != '\0')
        {
            tokens = [];
            return false;
        }

        AddToken(tokens, current, ref tokenStarted, ref tokenWasQuoted);
        return true;
    }

    private static void AddToken(
        List<CommandToken> tokens,
        StringBuilder current,
        ref bool tokenStarted,
        ref bool tokenWasQuoted)
    {
        if (!tokenStarted)
        {
            return;
        }

        tokens.Add(new CommandToken(current.ToString(), tokenWasQuoted));
        current.Clear();
        tokenStarted = false;
        tokenWasQuoted = false;
    }
}
